using System.IO;
using FlowDesk.Shared.Contracts;
using FlowDesk.Shared.Documents;
using Microsoft.Win32;

namespace FlowDesk.Desktop.Capture;

/// <summary>Abstracción del diálogo de archivos para poder probar la captura sin UI.</summary>
public interface IFileDialogService
{
    IReadOnlyList<string> PickFiles(string title, string filter, bool multiselect);
}

public sealed class WpfFileDialogService : IFileDialogService
{
    public IReadOnlyList<string> PickFiles(string title, string filter, bool multiselect)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            Multiselect = multiselect,
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }
}

/// <summary>
/// Captura desde archivos locales (PDF e imágenes). Aplica las mismas reglas de formato y tamaño
/// que el backend (FileSignatureValidator compartido) para detectar errores antes de enviar.
/// </summary>
public sealed class FileCaptureService(IFileDialogService dialogs) : IDocumentCaptureService
{
    private const string PdfFilter = "Documentos PDF (*.pdf)|*.pdf";
    private const string ImageFilter = "Imágenes (*.png;*.jpg;*.jpeg;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.tif;*.tiff";
    private const string AnyFilter = "PDF e imágenes|*.pdf;*.png;*.jpg;*.jpeg;*.tif;*.tiff|" + PdfFilter + "|" + ImageFilter;

    public string Id => "file";
    public string DisplayName => "Archivos locales";
    public CaptureSourceKind Kind => CaptureSourceKind.File;
    public bool IsAvailable => true;
    public string? UnavailableReason => null;

    public Task<DocumentCaptureResult> CaptureAsync(CaptureRequest request, CancellationToken cancellationToken)
    {
        var (title, filter) = request.Mode switch
        {
            CaptureMode.Pdf => ("Seleccionar documentos PDF", PdfFilter),
            CaptureMode.Images => ("Seleccionar imágenes", ImageFilter),
            _ => ("Seleccionar documentos", AnyFilter),
        };

        var paths = dialogs.PickFiles(title, filter, request.AllowMultiple);
        return paths.Count == 0
            ? Task.FromResult(DocumentCaptureResult.Cancelled())
            : ImportAsync(paths, cancellationToken);
    }

    /// <summary>Importa rutas concretas (diálogo o arrastrar y soltar).</summary>
    public async Task<DocumentCaptureResult> ImportAsync(IEnumerable<string> paths, CancellationToken cancellationToken)
    {
        var documents = new List<CapturedDocument>();
        var errors = new List<CaptureError>();

        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(path);
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    errors.Add(new CaptureError(name, "El archivo no existe."));
                    continue;
                }

                // Validación previa sin leer todo el archivo: extensión, tamaño y firma binaria.
                var header = new byte[FileSignatureValidator.HeaderLength];
                int read;
                await using (var stream = info.OpenRead())
                    read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);

                var validation = FileSignatureValidator.Validate(name, info.Length, header.AsSpan(0, read));
                if (!validation.IsValid)
                {
                    errors.Add(new CaptureError(name, validation.Error!));
                    continue;
                }

                var content = await File.ReadAllBytesAsync(path, cancellationToken);
                documents.Add(new CapturedDocument(name, validation.ContentType!, content, DocumentSources.DesktopFile, OriginalPath: path));
            }
            catch (IOException ex)
            {
                errors.Add(new CaptureError(name, $"No se pudo leer el archivo: {ex.Message}"));
            }
            catch (UnauthorizedAccessException)
            {
                errors.Add(new CaptureError(name, "Sin permisos para leer el archivo."));
            }
        }

        return DocumentCaptureResult.From(documents, errors);
    }
}
