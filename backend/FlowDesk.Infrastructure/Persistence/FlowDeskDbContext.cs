using System.Text.Json;
using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Audit;
using FlowDesk.Domain.Common;
using FlowDesk.Domain.Documents;
using FlowDesk.Domain.Expedients;
using FlowDesk.Domain.Identity;
using FlowDesk.Domain.Notifications;
using FlowDesk.Domain.Workflow;
using FlowDesk.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FlowDesk.Infrastructure.Persistence;

public sealed class FlowDeskDbContext(DbContextOptions<FlowDeskDbContext> options, ICurrentUser? currentUser = null)
    : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Expedient> Expedients => Set<Expedient>();
    public DbSet<ExpedientHistoryEntry> ExpedientHistory => Set<ExpedientHistoryEntry>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    private bool IsSqlite => Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // SQLite (solo desarrollo/pruebas) no soporta ordenar ni comparar DateTimeOffset.
        // Todas las fechas se guardan en UTC, por lo que la representación binaria conserva el orden.
        if (IsSqlite)
            builder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FlowDeskDbContext).Assembly);

        // Los identificadores se generan en el dominio: EF debe tratarlos como valores asignados.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var id = entity.FindProperty(nameof(Entity.Id));
            if (id is not null && id.ClrType == typeof(Guid))
                id.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
        }
    }

    /// <summary>
    /// Patrón Outbox: los eventos de dominio se serializan en OutboxMessages dentro de la MISMA
    /// transacción que el cambio de negocio. Así nunca se pierde un evento ni se publica uno
    /// de una operación que no llegó a confirmarse.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var aggregates = ChangeTracker.Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                OutboxMessages.Add(new OutboxMessage(
                    domainEvent.EventId,
                    domainEvent.GetType().Name,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), EventSerialization.Options),
                    domainEvent.OccurredAt,
                    currentUser?.CorrelationId));
            }
            aggregate.ClearDomainEvents();
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
