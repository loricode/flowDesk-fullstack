using FlowDesk.Domain.Common;
using FlowDesk.Domain.Documents;
using FlowDesk.Domain.Workflow;

namespace FlowDesk.Domain.Expedients;

public enum ExpedientStatus
{
    Received = 1,
    InValidation = 2,
    PendingApproval = 3,
    CorrectionRequested = 4,
    Approved = 5,
    Rejected = 6,
}

public enum ExpedientPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4,
}

public static class ExpedientStatusExtensions
{
    public static bool IsFinal(this ExpedientStatus status) =>
        status is ExpedientStatus.Approved or ExpedientStatus.Rejected;

    /// <summary>Estados en los que se permite incorporar o versionar documentos.</summary>
    public static bool AcceptsDocuments(this ExpedientStatus status) =>
        status is ExpedientStatus.Received or ExpedientStatus.InValidation or ExpedientStatus.CorrectionRequested;
}

/// <summary>
/// Expediente: agregado principal del negocio. Su estado solo cambia mediante transiciones
/// definidas en un <see cref="WorkflowDefinition"/>; nunca por asignación directa.
/// </summary>
public sealed class Expedient : AggregateRoot
{
    private Expedient() { }

    public string Code { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string ApplicantName { get; private set; } = default!;
    public string? Description { get; private set; }
    public ExpedientPriority Priority { get; private set; }
    public ExpedientStatus Status { get; private set; }
    public Guid CreatedById { get; private set; }
    public Guid? AssignedToId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>Token de concurrencia optimista: cambia en cada modificación.</summary>
    public Guid ConcurrencyStamp { get; private set; } = Guid.NewGuid();

    public List<Document> Documents { get; private set; } = [];
    public List<ExpedientHistoryEntry> History { get; private set; } = [];
    public List<Approval> Approvals { get; private set; } = [];

    public static Expedient Create(
        string code, string title, string applicantName, string? description,
        ExpedientPriority priority, Guid createdById, Guid? assignedToId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("El título es obligatorio.");
        if (string.IsNullOrWhiteSpace(applicantName)) throw new DomainException("El solicitante es obligatorio.");
        if (title.Length > 200) throw new DomainException("El título no puede superar 200 caracteres.");

        var expedient = new Expedient
        {
            Code = code,
            Title = title.Trim(),
            ApplicantName = applicantName.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Priority = priority,
            Status = ExpedientStatus.Received,
            CreatedById = createdById,
            AssignedToId = assignedToId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        expedient.History.Add(new ExpedientHistoryEntry(expedient.Id, null, ExpedientStatus.Received, createdById, "Expediente creado", now));
        expedient.Raise(new ExpedientCreated(expedient.Id, expedient.Code, createdById, assignedToId));
        return expedient;
    }

    /// <summary>
    /// Agrega un documento. Si ya existe uno con el mismo nombre, se crea una nueva versión
    /// en lugar de duplicarlo.
    /// </summary>
    public Document AddDocument(
        string fileName, string contentType, string storageKey, long sizeBytes,
        string source, Guid uploadedById, DateTimeOffset now)
    {
        if (!Status.AcceptsDocuments())
            throw new DomainException($"No se pueden agregar documentos en estado {Status}.", "invalid_state");

        var document = Documents.FirstOrDefault(d => string.Equals(d.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        if (document is null)
        {
            document = new Document(Id, fileName, contentType, now);
            Documents.Add(document);
        }

        var version = document.AddVersion(storageKey, sizeBytes, contentType, source, uploadedById, now);
        Touch(now);
        Raise(new DocumentUploaded(Id, document.Id, version.VersionNumber, uploadedById));
        return document;
    }

    public void Assign(Guid userId, Guid assignedById, DateTimeOffset now)
    {
        if (Status.IsFinal()) throw new DomainException("No se puede reasignar un expediente cerrado.", "invalid_state");
        if (AssignedToId == userId) return;

        var previous = AssignedToId;
        AssignedToId = userId;
        Touch(now);
        Raise(new ExpedientAssigned(Id, previous, userId, assignedById));
    }

    /// <summary>Ejecuta una transición del motor de workflow aplicando todas las reglas de negocio.</summary>
    public void ApplyTransition(
        WorkflowTransition transition, Guid userId, IReadOnlyCollection<string> userPermissions,
        string? comment, DateTimeOffset now)
    {
        if (transition.FromStatus != Status)
            throw new DomainException(
                $"La transición '{transition.Name}' no es válida desde el estado {Status}.", "invalid_transition");

        if (!userPermissions.Contains(transition.RequiredPermission))
            throw new DomainForbiddenException($"No tiene permiso para '{transition.Name}'.");

        if (transition.RequiresComment && string.IsNullOrWhiteSpace(comment))
            throw new DomainException($"La transición '{transition.Name}' requiere una observación.", "comment_required");

        EnsureConditions(transition.ToStatus);

        var from = Status;
        Status = transition.ToStatus;
        History.Add(new ExpedientHistoryEntry(Id, from, Status, userId, comment?.Trim(), now));

        if (from == ExpedientStatus.PendingApproval)
            Approvals.Add(new Approval(Id, ToDecision(Status), userId, comment?.Trim(), now));

        if (Status.IsFinal()) ClosedAt = now;

        Touch(now);
        Raise(new ExpedientStatusChanged(Id, Code, from, Status, userId, AssignedToId, CreatedById, comment));
    }

    private void EnsureConditions(ExpedientStatus target)
    {
        switch (target)
        {
            case ExpedientStatus.InValidation when Documents.Count == 0:
                throw new DomainException("El expediente debe tener al menos un documento para validarse.", "documents_required");

            case ExpedientStatus.PendingApproval when Documents.Count == 0:
                throw new DomainException("No hay documentos para aprobar.", "documents_required");

            case ExpedientStatus.PendingApproval when Documents.Any(d => d.Status == DocumentStatus.Pending):
                throw new DomainException("Hay documentos que aún se están procesando.", "documents_processing");

            case ExpedientStatus.PendingApproval when Documents.Any(d => d.Status == DocumentStatus.Invalid):
                throw new DomainException("Hay documentos inválidos. Solicite una corrección.", "documents_invalid");
        }
    }

    private static ApprovalDecision ToDecision(ExpedientStatus status) => status switch
    {
        ExpedientStatus.Approved => ApprovalDecision.Approved,
        ExpedientStatus.Rejected => ApprovalDecision.Rejected,
        _ => ApprovalDecision.CorrectionRequested,
    };

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        ConcurrencyStamp = Guid.NewGuid();
    }
}

public sealed class ExpedientHistoryEntry : Entity
{
    private ExpedientHistoryEntry() { }

    internal ExpedientHistoryEntry(Guid expedientId, ExpedientStatus? from, ExpedientStatus to, Guid changedById, string? comment, DateTimeOffset now)
    {
        ExpedientId = expedientId;
        FromStatus = from;
        ToStatus = to;
        ChangedById = changedById;
        Comment = comment;
        OccurredAt = now;
    }

    public Guid ExpedientId { get; private set; }
    public ExpedientStatus? FromStatus { get; private set; }
    public ExpedientStatus ToStatus { get; private set; }
    public Guid ChangedById { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}

public enum ApprovalDecision
{
    Approved = 1,
    Rejected = 2,
    CorrectionRequested = 3,
}

public sealed class Approval : Entity
{
    private Approval() { }

    internal Approval(Guid expedientId, ApprovalDecision decision, Guid decidedById, string? comment, DateTimeOffset now)
    {
        ExpedientId = expedientId;
        Decision = decision;
        DecidedById = decidedById;
        Comment = comment;
        DecidedAt = now;
    }

    public Guid ExpedientId { get; private set; }
    public ApprovalDecision Decision { get; private set; }
    public Guid DecidedById { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }
}
