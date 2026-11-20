using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using FlowDesk.Application.Notifications;
using FlowDesk.Domain.Expedients;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.Infrastructure.Services;
using FlowDesk.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowDesk.Infrastructure.Messaging;

public interface IMessageBus
{
    Task PublishAsync(MessageEnvelope envelope, CancellationToken ct);
}

/// <summary>Bus en proceso: entrega el mensaje directamente al despachador (desarrollo, sin AWS).</summary>
public sealed class InProcessMessageBus(IServiceScopeFactory scopes) : IMessageBus
{
    public async Task PublishAsync(MessageEnvelope envelope, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IntegrationEventDispatcher>().DispatchAsync(envelope, ct);
    }
}

/// <summary>Publica en AWS SQS. El consumo lo realiza <see cref="SqsConsumerService"/>.</summary>
public sealed class SqsMessageBus(IAmazonSQS sqs, IOptions<MessagingOptions> options) : IMessageBus
{
    public async Task PublishAsync(MessageEnvelope envelope, CancellationToken ct)
    {
        var queueUrl = options.Value.QueueUrl ?? throw new InvalidOperationException("Messaging:QueueUrl no configurado.");
        var request = new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = JsonSerializer.Serialize(envelope, EventSerialization.Options),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["type"] = new() { DataType = "String", StringValue = envelope.Type },
            },
        };

        if (queueUrl.EndsWith(".fifo", StringComparison.OrdinalIgnoreCase))
        {
            request.MessageGroupId = "flowdesk";
            request.MessageDeduplicationId = envelope.MessageId.ToString();
        }

        await sqs.SendMessageAsync(request, ct);
    }
}

/// <summary>
/// Traduce un mensaje a la reacción de negocio correspondiente. Registra cada mensaje procesado
/// en ProcessedMessages para ser idempotente.
/// </summary>
public sealed class IntegrationEventDispatcher(
    FlowDeskDbContext db,
    BusinessEventReactions reactions,
    ICurrentUser currentUser,
    IClock clock,
    ILogger<IntegrationEventDispatcher> logger)
{
    private const string Consumer = "business-reactions";

    public async Task DispatchAsync(MessageEnvelope envelope, CancellationToken ct)
    {
        if (currentUser is SystemCurrentUser system) system.CorrelationId = envelope.CorrelationId;

        using var _ = logger.BeginScope(new Dictionary<string, object?>
        {
            ["MessageId"] = envelope.MessageId,
            ["CorrelationId"] = envelope.CorrelationId,
        });

        if (await db.ProcessedMessages.AnyAsync(p => p.MessageId == envelope.MessageId && p.Consumer == Consumer, ct))
        {
            logger.LogInformation("Mensaje {Type} ya procesado; se omite.", envelope.Type);
            return;
        }

        switch (envelope.Type)
        {
            case nameof(ExpedientCreated):
                await reactions.OnExpedientCreatedAsync(Read<ExpedientCreated>(envelope), ct);
                break;
            case nameof(ExpedientAssigned):
                await reactions.OnExpedientAssignedAsync(Read<ExpedientAssigned>(envelope), ct);
                break;
            case nameof(ExpedientStatusChanged):
                await reactions.OnExpedientStatusChangedAsync(Read<ExpedientStatusChanged>(envelope), ct);
                break;
            case nameof(DocumentUploaded):
                await reactions.OnDocumentUploadedAsync(Read<DocumentUploaded>(envelope), ct);
                break;
            default:
                logger.LogWarning("Tipo de mensaje desconocido: {Type}", envelope.Type);
                break;
        }

        db.ProcessedMessages.Add(new ProcessedMessage(envelope.MessageId, Consumer, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Mensaje {Type} procesado.", envelope.Type);
    }

    private static T Read<T>(MessageEnvelope envelope) =>
        JsonSerializer.Deserialize<T>(envelope.Payload, EventSerialization.Options)
        ?? throw new InvalidOperationException($"Payload inválido para {envelope.Type}.");
}
