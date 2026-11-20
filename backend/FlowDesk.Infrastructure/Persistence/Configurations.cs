using FlowDesk.Domain.Audit;
using FlowDesk.Domain.Documents;
using FlowDesk.Domain.Expedients;
using FlowDesk.Domain.Identity;
using FlowDesk.Domain.Notifications;
using FlowDesk.Domain.Workflow;
using FlowDesk.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowDesk.Infrastructure.Persistence;

// Los nombres de tablas, columnas e índices coinciden con los scripts versionados de database/Scripts.

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.NormalizedEmail).HasMaxLength(256).IsRequired();
        b.HasIndex(x => x.NormalizedEmail).IsUnique().HasDatabaseName("UX_Users_NormalizedEmail");
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        b.Ignore(x => x.DomainEvents);
        b.Ignore(x => x.PermissionCodes);

        b.HasMany(x => x.Roles).WithMany(r => r.Users).UsingEntity<Dictionary<string, object>>(
            "UserRoles",
            r => r.HasOne<Role>().WithMany().HasForeignKey("RoleId").OnDelete(DeleteBehavior.Cascade),
            u => u.HasOne<User>().WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade),
            j => j.HasKey("UserId", "RoleId"));
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Roles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Name).IsUnique().HasDatabaseName("UX_Roles_Name");
        b.Property(x => x.Description).HasMaxLength(250).IsRequired();

        b.HasMany(x => x.Permissions).WithMany(p => p.Roles).UsingEntity<Dictionary<string, object>>(
            "RolePermissions",
            p => p.HasOne<Permission>().WithMany().HasForeignKey("PermissionId").OnDelete(DeleteBehavior.Cascade),
            r => r.HasOne<Role>().WithMany().HasForeignKey("RoleId").OnDelete(DeleteBehavior.Cascade),
            j => j.HasKey("RoleId", "PermissionId"));
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("Permissions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_Permissions_Code");
        b.Property(x => x.Description).HasMaxLength(250).IsRequired();
    }
}

internal sealed class ExpedientConfiguration : IEntityTypeConfiguration<Expedient>
{
    public void Configure(EntityTypeBuilder<Expedient> b)
    {
        b.ToTable("Expedients");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_Expedients_Code");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.ApplicantName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        b.Ignore(x => x.DomainEvents);

        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedToId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Documents).WithOne().HasForeignKey(d => d.ExpedientId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.History).WithOne().HasForeignKey(h => h.ExpedientId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Approvals).WithOne().HasForeignKey(a => a.ExpedientId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.Status, x.Priority, x.UpdatedAt }).HasDatabaseName("IX_Expedients_Status_Priority_UpdatedAt");
        b.HasIndex(x => new { x.AssignedToId, x.Status }).HasDatabaseName("IX_Expedients_AssignedTo_Status");
    }
}

internal sealed class ExpedientHistoryConfiguration : IEntityTypeConfiguration<ExpedientHistoryEntry>
{
    public void Configure(EntityTypeBuilder<ExpedientHistoryEntry> b)
    {
        b.ToTable("ExpedientHistory");
        b.HasKey(x => x.Id);
        b.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Comment).HasMaxLength(1000);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedById).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ExpedientId, x.OccurredAt }).HasDatabaseName("IX_ExpedientHistory_Expedient_OccurredAt");
    }
}

internal sealed class ApprovalConfiguration : IEntityTypeConfiguration<Approval>
{
    public void Configure(EntityTypeBuilder<Approval> b)
    {
        b.ToTable("Approvals");
        b.HasKey(x => x.Id);
        b.Property(x => x.Decision).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Comment).HasMaxLength(1000);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.DecidedById).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.ExpedientId).HasDatabaseName("IX_Approvals_ExpedientId");
    }
}

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.ToTable("Documents");
        b.HasKey(x => x.Id);
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ValidationMessage).HasMaxLength(500);
        b.Ignore(x => x.Current);
        b.HasMany(x => x.Versions).WithOne().HasForeignKey(v => v.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ExpedientId, x.FileName }).IsUnique().HasDatabaseName("UX_Documents_Expedient_FileName");
    }
}

internal sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> b)
    {
        b.ToTable("DocumentVersions");
        b.HasKey(x => x.Id);
        b.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength();
        b.Property(x => x.Source).HasMaxLength(20).IsRequired();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.DocumentId, x.VersionNumber }).IsUnique().HasDatabaseName("UX_DocumentVersions_Document_Version");
        b.HasIndex(x => x.Sha256).HasDatabaseName("IX_DocumentVersions_Sha256");
    }
}

internal sealed class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> b)
    {
        b.ToTable("WorkflowDefinitions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_WorkflowDefinitions_Code");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Ignore(x => x.DomainEvents);
        b.HasMany(x => x.Transitions).WithOne().HasForeignKey(t => t.WorkflowDefinitionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> b)
    {
        b.ToTable("WorkflowTransitions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.RequiredPermission).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.WorkflowDefinitionId, x.Code }).IsUnique().HasDatabaseName("UX_WorkflowTransitions_Definition_Code");
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Message).HasMaxLength(500).IsRequired();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt }).HasDatabaseName("IX_Notifications_User_IsRead_CreatedAt");
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserName).HasMaxLength(150);
        b.Property(x => x.Action).HasMaxLength(60).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(60).IsRequired();
        b.Property(x => x.PreviousState).HasMaxLength(200);
        b.Property(x => x.NewState).HasMaxLength(200);
        b.Property(x => x.Details).HasMaxLength(1000);
        b.Property(x => x.CorrelationId).HasMaxLength(64);
        b.HasIndex(x => x.OccurredAt).HasDatabaseName("IX_AuditLogs_OccurredAt");
        b.HasIndex(x => new { x.EntityId, x.OccurredAt }).HasDatabaseName("IX_AuditLogs_Entity_OccurredAt");
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("OutboxMessages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Type).HasMaxLength(200).IsRequired();
        b.Property(x => x.Payload).IsRequired();
        b.Property(x => x.LastError).HasMaxLength(2000);
        b.Property(x => x.CorrelationId).HasMaxLength(64);
        b.HasIndex(x => new { x.ProcessedAt, x.OccurredAt }).HasDatabaseName("IX_OutboxMessages_Pending");
    }
}

internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> b)
    {
        b.ToTable("ProcessedMessages");
        b.HasKey(x => new { x.MessageId, x.Consumer });
        b.Property(x => x.Consumer).HasMaxLength(100);
    }
}
