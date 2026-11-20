using System.IO;
using System.Windows.Media.Imaging;
using FlowDesk.Shared.Contracts;
using FlowDesk.Shared.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowDesk.Desktop.Capture.Twain;

/// <summary>
/// Captura desde escáner TWAIN. Toda la lógica independiente del driver ya está resuelta aquí:
/// configuración, adquisición, ensamblado de páginas en un TIFF multipágina, validación y
/// gestión de errores. Solo falta la sesión nativa (<see cref="ITwainSession"/>) para hardware real.
/// </summary>
public sealed class TwainCaptureService(
    ITwainSession session,
    IOptions<TwainOptions> options,
    ILogger<TwainCaptureService> logger) : IDocumentCaptureService
{
    public string Id => "twain";
    public string DisplayName => "Escáner TWAIN";
    public CaptureSourceKind Kind => CaptureSourceKind.Scanner;
    public bool IsAvailable => session.IsAvailable;
    public string? UnavailableReason => session.UnavailableReason;

    public async Task<DocumentCaptureResult> CaptureAsync(CaptureRequest request, CancellationToken cancellationToken)
    {
        if (!session.IsAvailable)
            return DocumentCaptureResult.Failed(session.UnavailableReason ?? "No hay escáner disponible.");

        var o = options.Value;
        var source = o.PreferredSource ?? session.GetSources().FirstOrDefault();
        var settings = new ScanSettings(source, o.Dpi, o.ColorMode, o.Duplex, o.UseAutomaticFeeder, o.ShowDriverUi);

        IReadOnlyList<BitmapSource> pages;
        try
        {
            logger.LogInformation("Iniciando escaneo en {Source} ({Dpi} DPI, {Mode}).", source, o.Dpi, o.ColorMode);
            pages = await session.AcquireAsync(settings, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return DocumentCaptureResult.Cancelled();
        }
        catch (TwainException ex)
        {
            logger.LogWarning(ex, "Error TWAIN.");
            return DocumentCaptureResult.Failed(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inesperado durante el escaneo.");
            return DocumentCaptureResult.Failed($"Error del escáner: {ex.Message}");
        }

        if (pages.Count == 0)
            return DocumentCaptureResult.Cancelled();

        var content = ScannedDocumentBuilder.BuildMultipageTiff(pages);
        var fileName = $"escaneo-{DateTime.Now:yyyyMMdd-HHmmss}.tiff";

        var validation = FileSignatureValidator.Validate(fileName, content.LongLength, content.AsSpan(0, FileSignatureValidator.HeaderLength));
        if (!validation.IsValid)
            return DocumentCaptureResult.Failed(validation.Error!);

        var document = new CapturedDocument(fileName, validation.ContentType!, content, DocumentSources.Scanner, pages.Count);
        return DocumentCaptureResult.From([document], []);
    }
}

/// <summary>Ensambla las páginas escaneadas en un único documento.</summary>
public static class ScannedDocumentBuilder
{
    /// <summary>
    /// TIFF multipágina con compresión sin pérdida: formato estándar de archivo documental,
    /// generado con los codificadores nativos de WPF (sin dependencias externas).
    /// </summary>
    public static byte[] BuildMultipageTiff(IReadOnlyList<BitmapSource> pages)
    {
        var encoder = new TiffBitmapEncoder { Compression = TiffCompressOption.Lzw };
        foreach (var page in pages)
            encoder.Frames.Add(BitmapFrame.Create(page));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
