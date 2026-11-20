using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Audit;

/// <summary>
/// Registro de auditoría de solo inserción. No expone métodos de modificación y la base de datos
/// impide UPDATE/DELETE mediante trigger (ver database/Scripts/008_CreateAuditLogs.sql).
/// </summary>
public sealed class AuditLog : Entity
{
    private AuditLog() { }

    public AuditLog(
        DateTimeOffset occurredAt, Guid? userId, string? userName, string action, string entityType,
        Guid? entityId, string? previousState, string? newState, string? details, string? correlationId)
    {
        OccurredAt = occurredAt;
        UserId = userId;
        UserName = userName;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        PreviousState = previousState;
        NewState = newState;
        Details = details;
        CorrelationId = correlationId;
    }

    public DateTimeOffset OccurredAt { get; private set; }
    public Guid? UserId { get; private set; }
    public string? UserName { get; private set; }
    public string Action { get; private set; } = default!;
    public string EntityType { get; private set; } = default!;
    public Guid? EntityId { get; private set; }
    public string? PreviousState { get; private set; }
    public string? NewState { get; private set; }
    public string? Details { get; private set; }
    public string? CorrelationId { get; private set; }
}

public static class AuditActions
{
    public const string Login = "auth.login";
    public const string LoginFailed = "auth.loginFailed";
    public const string ExpedientCreated = "expedient.created";
    public const string ExpedientAssigned = "expedient.assigned";
    public const string ExpedientTransition = "expedient.transition";
    public const string DocumentUploaded = "document.uploaded";
    public const string DocumentDownloaded = "document.downloaded";
    public const string DocumentProcessed = "document.processed";
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
}
