namespace FlowDesk.Desktop.Capture;

/// <summary>Tipo de origen de captura. La UI lo usa para agrupar y elegir íconos.</summary>
public enum CaptureSourceKind
{
    File,
    Scanner,
}

/// <summary>Qué desea capturar el usuario. Cada implementación interpreta lo que le aplica.</summary>
public enum CaptureMode
{
    Any,
    Pdf,
    Images,
}

public sealed record CaptureRequest(CaptureMode Mode = CaptureMode.Any, bool AllowMultiple = true);

/// <summary>Documento listo para enviar al backend, independiente de cómo se obtuvo.</summary>
public sealed record CapturedDocument(
    string FileName,
    string ContentType,
    byte[] Content,
    string Source,
    int PageCount = 1,
    string? OriginalPath = null)
{
    public long SizeBytes => Content.LongLength;
}

public sealed record CaptureError(string Item, string Message);

public enum CaptureStatus
{
    Success,
    PartialSuccess,
    Cancelled,
    Failed,
}

public sealed record DocumentCaptureResult(
    CaptureStatus Status,
    IReadOnlyList<CapturedDocument> Documents,
    IReadOnlyList<CaptureError> Errors)
{
    public static DocumentCaptureResult Cancelled() => new(CaptureStatus.Cancelled, [], []);

    public static DocumentCaptureResult Failed(string message) =>
        new(CaptureStatus.Failed, [], [new CaptureError("captura", message)]);

    public static DocumentCaptureResult From(IReadOnlyList<CapturedDocument> documents, IReadOnlyList<CaptureError> errors) =>
        new(documents.Count switch
        {
            0 when errors.Count == 0 => CaptureStatus.Cancelled,
            0 => CaptureStatus.Failed,
            _ when errors.Count > 0 => CaptureStatus.PartialSuccess,
            _ => CaptureStatus.Success,
        }, documents, errors);
}

/// <summary>
/// Punto de extensión de la captura documental. El resto de la aplicación (ViewModels, envío a la
/// API) solo conoce esta interfaz, por lo que agregar TWAIN, WIA o un escáner de red no requiere
/// modificar la lógica de negocio.
/// </summary>
public interface IDocumentCaptureService
{
    /// <summary>Identificador estable (p. ej. "file", "twain").</summary>
    string Id { get; }

    string DisplayName { get; }

    CaptureSourceKind Kind { get; }

    /// <summary>Indica si el origen puede usarse ahora (driver instalado, configuración activa…).</summary>
    bool IsAvailable { get; }

    /// <summary>Motivo por el que no está disponible, para mostrarlo al usuario.</summary>
    string? UnavailableReason { get; }

    Task<DocumentCaptureResult> CaptureAsync(CaptureRequest request, CancellationToken cancellationToken);
}
