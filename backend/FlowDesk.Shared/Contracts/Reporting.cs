namespace FlowDesk.Shared.Contracts;

public sealed record DashboardDto(
    int OpenExpedients,
    int AssignedToMe,
    int PendingApproval,
    int ApprovedLast30Days,
    int RejectedLast30Days,
    double? AverageResolutionHours,
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyList<AuditEntryDto> RecentActivity);

public sealed record AuditEntryDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    string? UserName,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? PreviousState,
    string? NewState,
    string? Details,
    string? CorrelationId);

public sealed record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    Guid? ExpedientId,
    bool IsRead,
    DateTimeOffset CreatedAt);

/// <summary>Mensaje que viaja por SignalR hacia los clientes.</summary>
public sealed record RealtimeEvent(
    string Type,
    Guid? ExpedientId,
    Guid? DocumentId,
    string? Status,
    string Message,
    DateTimeOffset OccurredAt,
    Guid? TargetUserId = null);

public static class RealtimeEventTypes
{
    public const string ExpedientCreated = "expedient.created";
    public const string ExpedientStatusChanged = "expedient.statusChanged";
    public const string ExpedientAssigned = "expedient.assigned";
    public const string DocumentUploaded = "document.uploaded";
    public const string DocumentProcessed = "document.processed";
    public const string Notification = "notification";
}
