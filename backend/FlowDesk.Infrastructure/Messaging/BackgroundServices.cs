using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using FlowDesk.Application.Abstractions;
using FlowDesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowDesk.Infrastructure.Messaging;

/// <summary>
/// Lee los mensajes pendientes del Outbox y los publica en el bus (SQS o en proceso).
/// Los fallos se reintentan hasta MaxAttempts; el error queda registrado en LastError.
/// </summary>
public sealed class OutboxDispatcherService(
    IServiceScopeFactory scopes,
    IOptions<MessagingOptions> options,
    ILogger<OutboxDispatcherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        logger.LogInformation("Outbox dispatcher iniciado (proveedor {Provider}).", o.Provider);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(o.PollingIntervalSeconds));

        do
        {
            try
            {
                await DispatchBatchAsync(o, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error procesando el Outbox.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DispatchBatchAsync(MessagingOptions o, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowDeskDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        // Con varios Workers sobre SQL Server se puede usar UPDLOCK/READPAST; la idempotencia
        // del consumidor cubre igualmente una doble publicación.
        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.Attempts < o.MaxAttempts)
            .OrderBy(m => m.OccurredAt)
            .Take(o.BatchSize)
            .ToListAsync(ct);

        foreach (var message in pending)
        {
            try
            {
                await bus.PublishAsync(message.ToEnvelope(), ct);
                message.MarkProcessed(clock.UtcNow);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Fallo publicando {Type} ({Id}), intento {Attempt}.", message.Type, message.Id, message.Attempts + 1);
                message.MarkFailed(ex.Message);
            }
            await db.SaveChangesAsync(ct);
        }
    }
}

/// <summary>Consume la cola SQS con long polling. Los mensajes fallidos vuelven a la cola y, tras N intentos, a la DLQ.</summary>
public sealed class SqsConsumerService(
    IAmazonSQS sqs,
    IServiceScopeFactory scopes,
    IOptions<MessagingOptions> options,
    ILogger<SqsConsumerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueUrl = options.Value.QueueUrl ?? throw new InvalidOperationException("Messaging:QueueUrl no configurado.");
        logger.LogInformation("Consumidor SQS escuchando {Queue}.", queueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            ReceiveMessageResponse response;
            try
            {
                response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20,
                }, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error recibiendo de SQS.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            foreach (var sqsMessage in response.Messages ?? [])
            {
                try
                {
                    var envelope = JsonSerializer.Deserialize<MessageEnvelope>(sqsMessage.Body, EventSerialization.Options)!;
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IntegrationEventDispatcher>().DispatchAsync(envelope, stoppingToken);
                    await sqs.DeleteMessageAsync(queueUrl, sqsMessage.ReceiptHandle, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error procesando mensaje SQS {MessageId}; se reintentará.", sqsMessage.MessageId);
                }
            }
        }
    }
}
