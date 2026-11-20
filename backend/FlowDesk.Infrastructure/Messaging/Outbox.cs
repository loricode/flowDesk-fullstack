using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowDesk.Infrastructure.Messaging;

public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public OutboxMessage(Guid id, string type, string payload, DateTimeOffset occurredAt, string? correlationId)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredAt = occurredAt;
        CorrelationId = correlationId;
    }

    public Guid Id { get; private set; }
    public string Type { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public string? CorrelationId { get; private set; }

    public MessageEnvelope ToEnvelope() => new(Id, Type, Payload, CorrelationId, OccurredAt);

    public void MarkProcessed(DateTimeOffset now)
    {
        ProcessedAt = now;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = error.Length > 2000 ? error[..2000] : error;
    }
}

/// <summary>Registro de mensajes ya consumidos: garantiza idempotencia ante entregas repetidas (SQS es at-least-once).</summary>
public sealed class ProcessedMessage
{
    private ProcessedMessage() { }

    public ProcessedMessage(Guid messageId, string consumer, DateTimeOffset processedAt)
    {
        MessageId = messageId;
        Consumer = consumer;
        ProcessedAt = processedAt;
    }

    public Guid MessageId { get; private set; }
    public string Consumer { get; private set; } = default!;
    public DateTimeOffset ProcessedAt { get; private set; }
}

public sealed record MessageEnvelope(Guid MessageId, string Type, string Payload, string? CorrelationId, DateTimeOffset OccurredAt);

public static class EventSerialization
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    /// <summary>"InProcess" (el Worker procesa directamente desde el Outbox) o "Sqs".</summary>
    public string Provider { get; set; } = "InProcess";
    public string? QueueUrl { get; set; }
    public int PollingIntervalSeconds { get; set; } = 2;
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 5;
}
