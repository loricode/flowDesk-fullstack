namespace FlowDesk.Shared.Contracts;

public sealed record CreateExpedientRequest(
    string Title,
    string ApplicantName,
    string? Description,
    string Priority,
    Guid? AssignedToId);

public sealed record AssignExpedientRequest(Guid UserId, string ConcurrencyStamp);

public sealed record ExecuteTransitionRequest(string TransitionCode, string? Comment, string ConcurrencyStamp);

public sealed record ExpedientQuery(
    string? Status = null,
    string? Search = null,
    bool AssignedToMe = false,
    string? Priority = null,
    int Page = 1,
    int PageSize = 20);

public sealed record ExpedientSummaryDto(
    Guid Id,
    string Code,
    string Title,
    string ApplicantName,
    string Status,
    string Priority,
    Guid? AssignedToId,
    string? AssignedToName,
    int DocumentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ExpedientDetailDto(
    Guid Id,
    string Code,
    string Title,
    string ApplicantName,
    string? Description,
    string Status,
    string Priority,
    Guid CreatedById,
    string CreatedByName,
    Guid? AssignedToId,
    string? AssignedToName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string ConcurrencyStamp,
    IReadOnlyList<DocumentDto> Documents,
    IReadOnlyList<HistoryEntryDto> History,
    IReadOnlyList<ApprovalDto> Approvals,
    IReadOnlyList<TransitionDto> AvailableTransitions);

public sealed record HistoryEntryDto(
    string? FromStatus,
    string ToStatus,
    string ChangedByName,
    string? Comment,
    DateTimeOffset OccurredAt);

public sealed record ApprovalDto(string Decision, string DecidedByName, string? Comment, DateTimeOffset DecidedAt);

public sealed record TransitionDto(string Code, string Name, string FromStatus, string ToStatus, bool RequiresComment);
