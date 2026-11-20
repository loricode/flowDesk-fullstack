using System.Security.Cryptography;
using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Expedients;
using FlowDesk.Domain.Audit;
using FlowDesk.Domain.Documents;
using FlowDesk.Domain.Expedients;
using FlowDesk.Shared.Contracts;
using FlowDesk.Shared.Documents;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Documents;

public sealed record DocumentContent(Stream Content, string ContentType, string FileName);

public sealed class DocumentService(
    IAppDbContext db,
    IFileStorage storage,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock,
    ExpedientService expedients)
{
    private static readonly HashSet<string> KnownSources =
        [DocumentSources.Web, DocumentSources.DesktopFile, DocumentSources.Scanner];

    /// <summary>
    /// Carga un documento: valida el formato, guarda el archivo en el almacenamiento y registra la
    /// metadata. El procesamiento pesado (hash, duplicados) ocurre después, en el Worker, vía Outbox.
    /// </summary>
    public async Task<DocumentDto> UploadAsync(
        Guid expedientId, string fileName, Stream content, string? source, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("No autenticado.");
        var safeName = Path.GetFileName(fileName);
        var origin = source is not null && KnownSources.Contains(source) ? source : DocumentSources.Web;

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var validation = await FileSignatureValidator.ValidateAsync(safeName, buffer, ct);
        if (!validation.IsValid) throw new ValidationException("file", validation.Error!);

        var expedient = await expedients.LoadAggregateAsync(expedientId, tracking: true, ct);
        if (!expedient.Status.AcceptsDocuments())
            throw new ValidationException("expedient", $"El expediente en estado {expedient.Status} no admite documentos.");

        var key = $"expedients/{expedient.Id:N}/{Guid.NewGuid():N}{Path.GetExtension(safeName).ToLowerInvariant()}";
        await storage.SaveAsync(key, buffer, validation.ContentType!, ct);

        Document document;
        try
        {
            document = expedient.AddDocument(safeName, validation.ContentType!, key, buffer.Length, origin, userId, clock.UtcNow);
            audit.Write(AuditActions.DocumentUploaded, nameof(Document), document.Id,
                newState: $"v{document.CurrentVersion}", details: $"{safeName} ({origin})");
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw new ConcurrencyException();
        }
        catch
        {
            // Compensación: si la metadata no se guarda, el archivo no debe quedar huérfano.
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        var names = await db.NamesAsync(document.Versions.Select(v => v.UploadedById), ct);
        return document.ToDto(names);
    }

    public async Task<DocumentContent> OpenAsync(Guid documentId, int? version, CancellationToken ct)
    {
        var document = await db.Documents.AsNoTracking()
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == documentId, ct) ?? throw new NotFoundException("Documento", documentId);

        var number = version ?? document.CurrentVersion;
        var v = document.Versions.FirstOrDefault(x => x.VersionNumber == number)
            ?? throw new NotFoundException("Versión", number);

        audit.Write(AuditActions.DocumentDownloaded, nameof(Document), document.Id, details: $"v{number}");
        await db.SaveChangesAsync(ct);

        var stream = await storage.OpenReadAsync(v.StorageKey, ct);
        return new DocumentContent(stream, v.ContentType, document.FileName);
    }

    public async Task<DocumentDto> GetAsync(Guid documentId, CancellationToken ct)
    {
        var document = await db.Documents.AsNoTracking()
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == documentId, ct) ?? throw new NotFoundException("Documento", documentId);
        var names = await db.NamesAsync(document.Versions.Select(v => v.UploadedById), ct);
        return document.ToDto(names);
    }
}

public sealed record DocumentProcessingResult(Guid ExpedientId, Guid DocumentId, string FileName, DocumentStatus Status, string? Message);

/// <summary>
/// Procesamiento asíncrono ejecutado por el Worker: calcula el hash, revalida la firma
/// y detecta duplicados dentro del expediente. Es idempotente.
/// </summary>
public sealed class DocumentProcessingService(IAppDbContext db, IFileStorage storage, IAuditWriter audit)
{
    public async Task<DocumentProcessingResult?> ProcessAsync(Guid documentId, int version, CancellationToken ct)
    {
        var document = await db.Documents.Include(d => d.Versions).FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (document is null || document.CurrentVersion != version || document.Status != DocumentStatus.Pending)
            return null; // Mensaje duplicado u obsoleto.

        var current = document.Current;
        string hash;
        string? error;

        await using (var stream = await storage.OpenReadAsync(current.StorageKey, ct))
        await using (var copy = new MemoryStream())
        {
            await stream.CopyToAsync(copy, ct);
            copy.Position = 0;
            hash = Convert.ToHexString(await SHA256.HashDataAsync(copy, ct));
            copy.Position = 0;
            var validation = await FileSignatureValidator.ValidateAsync(document.FileName, copy, ct);
            error = validation.IsValid ? null : validation.Error;
        }

        if (error is null)
        {
            var duplicateOf = await db.DocumentVersions.AsNoTracking()
                .Where(v => v.Sha256 == hash && v.DocumentId != document.Id)
                .Join(db.Documents.Where(d => d.ExpedientId == document.ExpedientId), v => v.DocumentId, d => d.Id, (v, d) => d.FileName)
                .FirstOrDefaultAsync(ct);

            if (duplicateOf is not null)
                error = $"Documento duplicado: el contenido es idéntico a '{duplicateOf}'.";
        }

        if (!document.CompleteProcessing(version, hash, error))
            return null;

        audit.Write(AuditActions.DocumentProcessed, nameof(Document), document.Id,
            DocumentStatus.Pending.ToString(), document.Status.ToString(), error ?? $"SHA-256 {hash[..12]}…");
        await db.SaveChangesAsync(ct);

        return new DocumentProcessingResult(document.ExpedientId, document.Id, document.FileName, document.Status, error);
    }
}
