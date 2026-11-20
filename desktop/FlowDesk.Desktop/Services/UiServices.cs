using System.IO;
using MaterialDesignThemes.Wpf;

namespace FlowDesk.Desktop.Services;

/// <summary>Mensajes breves (snackbar) desacoplados de las vistas.</summary>
public sealed class NotificationService
{
    public SnackbarMessageQueue Queue { get; } = new(TimeSpan.FromSeconds(4));

    public void Show(string message) => Queue.Enqueue(message);
}

/// <summary>
/// Archivos temporales para la vista previa (WebView2 necesita una URI de archivo para PDF).
/// Se eliminan al cerrar la aplicación.
/// </summary>
public sealed class PreviewFileCache : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "FlowDeskCapture", Guid.NewGuid().ToString("N"));

    public string Write(string fileName, byte[] content)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}");
        File.WriteAllBytes(path, content);
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
        catch (IOException) { /* El visor puede mantener el archivo abierto; el SO lo limpiará. */ }
    }
}
