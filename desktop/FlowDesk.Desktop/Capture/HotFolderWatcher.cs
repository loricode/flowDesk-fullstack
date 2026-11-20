using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowDesk.Desktop.Capture;

public sealed class HotFolderOptions
{
    public const string SectionName = "Capture:HotFolder";

    /// <summary>Carpeta vigilada. Vacío = desactivado.</summary>
    public string? Path { get; set; }

    /// <summary>Mover los archivos importados a la subcarpeta "procesados".</summary>
    public bool MoveProcessed { get; set; } = true;
}

/// <summary>
/// Carpeta vigilada ("hot folder"): escáneres de red, multifuncionales o el propio usuario depositan
/// archivos en una carpeta y entran solos a la bandeja de captura. Es la misma ruta de importación
/// (y validación) que usa <see cref="FileCaptureService"/>.
/// </summary>
public sealed class HotFolderWatcher(IOptions<HotFolderOptions> options, ILogger<HotFolderWatcher> logger) : IDisposable
{
    private FileSystemWatcher? _watcher;

    public string? FolderPath => options.Value.Path;
    public bool IsEnabled => !string.IsNullOrWhiteSpace(options.Value.Path);

    /// <summary>Se dispara (en un hilo del sistema de archivos) con la ruta de cada archivo nuevo.</summary>
    public event EventHandler<string>? FileArrived;

    public void Start()
    {
        if (!IsEnabled || _watcher is not null) return;

        var path = options.Value.Path!;
        Directory.CreateDirectory(path);
        _watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Created += (_, e) => _ = OnCreatedAsync(e.FullPath);
        _watcher.Renamed += (_, e) => _ = OnCreatedAsync(e.FullPath);
        logger.LogInformation("Carpeta vigilada activa: {Path}", path);
    }

    private async Task OnCreatedAsync(string path)
    {
        // Los formatos no admitidos también se notifican: la importación mostrará el motivo del rechazo.
        // Esperar a que el productor (escáner, copia) termine de escribir el archivo.
        for (var i = 0; i < 20; i++)
        {
            try
            {
                await using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                break;
            }
            catch (IOException)
            {
                await Task.Delay(250);
            }
        }

        FileArrived?.Invoke(this, path);
    }

    /// <summary>Mueve un archivo ya importado fuera de la carpeta vigilada.</summary>
    public void MarkProcessed(string path)
    {
        if (!options.Value.MoveProcessed || !File.Exists(path)) return;
        try
        {
            var done = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!, "procesados");
            Directory.CreateDirectory(done);
            var target = System.IO.Path.Combine(done, $"{DateTime.Now:yyyyMMdd-HHmmss}-{System.IO.Path.GetFileName(path)}");
            File.Move(path, target);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "No se pudo mover {Path} a procesados.", path);
        }
    }

    public void Dispose() => _watcher?.Dispose();
}
