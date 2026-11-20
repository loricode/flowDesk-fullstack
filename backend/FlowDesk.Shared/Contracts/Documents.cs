namespace FlowDesk.Shared.Contracts;

public sealed record DocumentDto(
    Guid Id,
    Guid ExpedientId,
    string FileName,
    string ContentType,
    string Status,
    string? ValidationMessage,
    int CurrentVersion,
    long SizeBytes,
    string Source,
    DateTimeOffset UploadedAt,
    IReadOnlyList<DocumentVersionDto> Versions);

public sealed record DocumentVersionDto(
    int Version,
    long SizeBytes,
    string? Sha256,
    string UploadedByName,
    DateTimeOffset UploadedAt,
    string Source);
