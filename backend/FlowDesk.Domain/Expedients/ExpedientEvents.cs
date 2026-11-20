using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Expedients;

public sealed record ExpedientCreated(Guid ExpedientId, string Code, Guid CreatedById, Guid? AssignedToId) : DomainEvent;

public sealed record ExpedientAssigned(Guid ExpedientId, Guid? PreviousAssigneeId, Guid AssignedToId, Guid AssignedById) : DomainEvent;

public sealed record ExpedientStatusChanged(
    Guid ExpedientId,
    string Code,
    ExpedientStatus FromStatus,
    ExpedientStatus ToStatus,
    Guid ChangedById,
    Guid? AssignedToId,
    Guid CreatedById,
    string? Comment) : DomainEvent;

public sealed record DocumentUploaded(Guid ExpedientId, Guid DocumentId, int Version, Guid UploadedById) : DomainEvent;
