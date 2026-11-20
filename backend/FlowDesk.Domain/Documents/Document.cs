using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Documents;

public enum DocumentStatus
{
    Pending = 1,
    Validated = 2,
    Invalid = 3,
}

/// <summary>
/// Documento lógico de un expediente. El archivo físico vive en S3 (o almacenamiento local en
/// desarrollo); aquí solo se guarda la referencia, la metadata y el versionamiento.
/// </summary>
public sealed class Document : Entity
{
    private Document() { }

    internal Document(Guid expedientId, string fileName, string contentType, DateTimeOffset now)
    {
        ExpedientId = expedientId;
        FileName = fileName;
        ContentType = contentType;
        Status = DocumentStatus.Pending;
        CreatedAt = now;
    }

    public Guid ExpedientId { get; private set; }
    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public DocumentStatus Status { get; private set; }
    public string? ValidationMessage { get; private set; }
    public int CurrentVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public List<DocumentVersion> Versions { get; private set; } = [];

    public DocumentVersion Current => Versions.Single(v => v.VersionNumber == CurrentVersion);

    internal DocumentVersion AddVersion(string storageKey, long sizeBytes, string contentType, string source, Guid uploadedById, DateTimeOffset now)
    {
        CurrentVersion++;
        ContentType = contentType;
        Status = DocumentStatus.Pending;
        ValidationMessage = null;

        var version = new DocumentVersion(Id, CurrentVersion, storageKey, sizeBytes, contentType, source, uploadedById, now);
        Versions.Add(version);
        return version;
    }

    /// <summary>Resultado del procesamiento asíncrono. Idempotente: ignora versiones obsoletas.</summary>
    public bool CompleteProcessing(int versionNumber, string sha256, string? error)
    {
        if (versionNumber != CurrentVersion || Status != DocumentStatus.Pending)
            return false;

        Versions.Single(v => v.VersionNumber == versionNumber).SetHash(sha256);
        Status = error is null ? DocumentStatus.Validated : DocumentStatus.Invalid;
        ValidationMessage = error;
        return true;
    }
}

public sealed class DocumentVersion : Entity
{
    private DocumentVersion() { }

    internal DocumentVersion(Guid documentId, int versionNumber, string storageKey, long sizeBytes, string contentType, string source, Guid uploadedById, DateTimeOffset now)
    {
        DocumentId = documentId;
        VersionNumber = versionNumber;
        StorageKey = storageKey;
        SizeBytes = sizeBytes;
        ContentType = contentType;
        Source = source;
        UploadedById = uploadedById;
        UploadedAt = now;
    }

    public Guid DocumentId { get; private set; }
    public int VersionNumber { get; private set; }
    public string StorageKey { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public string ContentType { get; private set; } = default!;
    public string? Sha256 { get; private set; }
    public string Source { get; private set; } = default!;
    public Guid UploadedById { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }

    internal void SetHash(string sha256) => Sha256 = sha256;
}
