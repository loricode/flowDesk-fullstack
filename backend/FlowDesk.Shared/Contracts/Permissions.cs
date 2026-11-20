namespace FlowDesk.Shared.Contracts;

/// <summary>
/// Catálogo de permisos. El backend valida cada uno; los clientes solo los usan para adaptar la UI.
/// </summary>
public static class FlowDeskPermissions
{
    public const string ExpedientsRead = "expedients.read";
    public const string ExpedientsCreate = "expedients.create";
    public const string ExpedientsAssign = "expedients.assign";
    public const string DocumentsUpload = "documents.upload";
    public const string WorkflowValidate = "workflow.validate";
    public const string WorkflowApprove = "workflow.approve";
    public const string AuditRead = "audit.read";
    public const string UsersManage = "users.manage";

    public static readonly IReadOnlyList<string> All =
    [
        ExpedientsRead, ExpedientsCreate, ExpedientsAssign, DocumentsUpload,
        WorkflowValidate, WorkflowApprove, AuditRead, UsersManage
    ];
}

public static class FlowDeskRoles
{
    public const string Administrator = "Administrador";
    public const string Operator = "Operador";
    public const string Validator = "Validador";
    public const string Approver = "Aprobador";
    public const string Auditor = "Auditor";
}
