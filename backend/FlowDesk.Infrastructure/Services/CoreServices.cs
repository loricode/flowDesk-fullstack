using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Audit;

namespace FlowDesk.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class AuditWriter(IAppDbContext db, ICurrentUser currentUser, IClock clock) : IAuditWriter
{
    public void Write(string action, string entityType, Guid? entityId,
        string? previousState = null, string? newState = null, string? details = null,
        Guid? actorId = null, string? actorName = null)
    {
        var userId = actorId ?? currentUser.UserId;
        db.AuditLogs.Add(new AuditLog(
            clock.UtcNow,
            userId,
            actorName ?? currentUser.FullName ?? (userId is null ? "Sistema" : null),
            action, entityType, entityId,
            Truncate(previousState, 200), Truncate(newState, 200), Truncate(details, 1000),
            currentUser.CorrelationId));
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}

/// <summary>Identidad usada por procesos en segundo plano (Worker).</summary>
public sealed class SystemCurrentUser : ICurrentUser
{
    public Guid? UserId => null;
    public string? FullName => "Sistema";
    public IReadOnlyCollection<string> Permissions => [];
    public string? CorrelationId { get; set; }
}
