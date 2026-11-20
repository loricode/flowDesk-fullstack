using FlowDesk.Domain.Audit;
using FlowDesk.Domain.Documents;
using FlowDesk.Domain.Expedients;
using FlowDesk.Domain.Identity;
using FlowDesk.Domain.Notifications;
using FlowDesk.Domain.Workflow;
using FlowDesk.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Abstractions;

/// <summary>
/// Puerto de persistencia. Application depende de EF Core (decisión pragmática documentada en
/// docs/decisions/0003-efcore-en-application.md) pero no conoce el proveedor concreto.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<Expedient> Expedients { get; }
    DbSet<ExpedientHistoryEntry> ExpedientHistory { get; }
    DbSet<Approval> Approvals { get; }
    DbSet<Document> Documents { get; }
    DbSet<DocumentVersion> DocumentVersions { get; }
    DbSet<WorkflowDefinition> WorkflowDefinitions { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? FullName { get; }
    IReadOnlyCollection<string> Permissions { get; }
    string? CorrelationId { get; }
    bool IsAuthenticated => UserId.HasValue;
    bool HasPermission(string permission) => Permissions.Contains(permission);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Almacenamiento de archivos físicos (S3 en AWS, disco local en desarrollo).</summary>
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateToken(User user);
}

/// <summary>Publica eventos hacia los clientes conectados (SignalR).</summary>
public interface IRealtimeNotifier
{
    Task PublishAsync(RealtimeEvent realtimeEvent, CancellationToken cancellationToken = default);
}

/// <summary>Agrega registros de auditoría a la unidad de trabajo actual (misma transacción).</summary>
public interface IAuditWriter
{
    /// <param name="actorId">Actor explícito cuando aún no hay usuario autenticado (p. ej. login).</param>
    void Write(string action, string entityType, Guid? entityId,
        string? previousState = null, string? newState = null, string? details = null,
        Guid? actorId = null, string? actorName = null);
}
