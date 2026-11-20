using FlowDesk.Domain.Common;
using FlowDesk.Domain.Expedients;

namespace FlowDesk.Domain.Workflow;

/// <summary>
/// Definición de un flujo: el conjunto cerrado de transiciones válidas entre estados.
/// Se persiste en base de datos (WorkflowDefinitions / WorkflowTransitions) para que
/// pueda configurarse sin recompilar.
/// </summary>
public sealed class WorkflowDefinition : AggregateRoot
{
    public const string DefaultCode = "EXPEDIENT_DEFAULT";

    private WorkflowDefinition() { }

    public WorkflowDefinition(string code, string name)
    {
        Code = code;
        Name = name;
        IsActive = true;
    }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public List<WorkflowTransition> Transitions { get; private set; } = [];

    public WorkflowDefinition AddTransition(
        string code, string name, ExpedientStatus from, ExpedientStatus to,
        string requiredPermission, bool requiresComment = false)
    {
        if (Transitions.Any(t => t.Code == code))
            throw new DomainException($"La transición '{code}' ya existe en el flujo.");
        if (from == to)
            throw new DomainException("Una transición debe cambiar de estado.");

        Transitions.Add(new WorkflowTransition(Id, code, name, from, to, requiredPermission, requiresComment, Transitions.Count + 1));
        return this;
    }

    public WorkflowTransition GetTransition(string code) =>
        Transitions.FirstOrDefault(t => t.Code == code)
        ?? throw new DomainException($"La transición '{code}' no existe.", "transition_not_found");

    /// <summary>Transiciones que el usuario puede ejecutar desde el estado actual.</summary>
    public IEnumerable<WorkflowTransition> AvailableFrom(ExpedientStatus status, IReadOnlyCollection<string> permissions) =>
        Transitions
            .Where(t => t.FromStatus == status && permissions.Contains(t.RequiredPermission))
            .OrderBy(t => t.SortOrder);

    /// <summary>Flujo por defecto del MVP.</summary>
    public static WorkflowDefinition CreateDefault(string validate, string approve, string upload)
    {
        var wf = new WorkflowDefinition(DefaultCode, "Flujo estándar de expedientes");
        wf.AddTransition("start-validation", "Iniciar validación", ExpedientStatus.Received, ExpedientStatus.InValidation, validate)
          .AddTransition("send-to-approval", "Enviar a aprobación", ExpedientStatus.InValidation, ExpedientStatus.PendingApproval, validate)
          .AddTransition("request-correction", "Solicitar corrección", ExpedientStatus.InValidation, ExpedientStatus.CorrectionRequested, validate, requiresComment: true)
          .AddTransition("reject-validation", "Rechazar en validación", ExpedientStatus.InValidation, ExpedientStatus.Rejected, validate, requiresComment: true)
          .AddTransition("approve", "Aprobar", ExpedientStatus.PendingApproval, ExpedientStatus.Approved, approve)
          .AddTransition("reject", "Rechazar", ExpedientStatus.PendingApproval, ExpedientStatus.Rejected, approve, requiresComment: true)
          .AddTransition("return-correction", "Devolver para corrección", ExpedientStatus.PendingApproval, ExpedientStatus.CorrectionRequested, approve, requiresComment: true)
          .AddTransition("resubmit", "Reenviar a validación", ExpedientStatus.CorrectionRequested, ExpedientStatus.InValidation, upload);
        return wf;
    }
}

public sealed class WorkflowTransition : Entity
{
    private WorkflowTransition() { }

    internal WorkflowTransition(
        Guid workflowDefinitionId, string code, string name, ExpedientStatus from, ExpedientStatus to,
        string requiredPermission, bool requiresComment, int sortOrder)
    {
        WorkflowDefinitionId = workflowDefinitionId;
        Code = code;
        Name = name;
        FromStatus = from;
        ToStatus = to;
        RequiredPermission = requiredPermission;
        RequiresComment = requiresComment;
        SortOrder = sortOrder;
    }

    public Guid WorkflowDefinitionId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public ExpedientStatus FromStatus { get; private set; }
    public ExpedientStatus ToStatus { get; private set; }
    public string RequiredPermission { get; private set; } = default!;
    public bool RequiresComment { get; private set; }
    public int SortOrder { get; private set; }
}
