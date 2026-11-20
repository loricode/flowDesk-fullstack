namespace FlowDesk.Shared.Contracts;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Estados del expediente expuestos a los clientes (React / WPF).</summary>
public static class ExpedientStatuses
{
    public const string Received = "Received";
    public const string InValidation = "InValidation";
    public const string PendingApproval = "PendingApproval";
    public const string CorrectionRequested = "CorrectionRequested";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public static class DocumentStatuses
{
    public const string Pending = "Pending";
    public const string Validated = "Validated";
    public const string Invalid = "Invalid";
}

/// <summary>Origen de la captura de un documento.</summary>
public static class DocumentSources
{
    public const string Web = "Web";
    public const string DesktopFile = "DesktopFile";
    public const string Scanner = "Scanner";
}

public static class ExpedientPriorities
{
    public const string Low = "Low";
    public const string Normal = "Normal";
    public const string High = "High";
    public const string Urgent = "Urgent";

    public static readonly IReadOnlyList<string> All = [Low, Normal, High, Urgent];
}
