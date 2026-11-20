using System.Windows.Media.Imaging;

namespace FlowDesk.Desktop.Capture.Twain;

public enum ScanColorMode
{
    BlackWhite,
    Grayscale,
    Color,
}

/// <summary>Controlador TWAIN a utilizar. Se elige en appsettings.json → Capture:Twain:Driver.</summary>
public enum TwainDriver
{
    /// <summary>Sin escáner: la opción aparece deshabilitada (configuración actual).</summary>
    None,

    /// <summary>Simulador: genera páginas sintéticas para demostrar el flujo completo sin hardware.</summary>
    Simulated,

    /// <summary>Escáner físico a través del Data Source Manager de TWAIN (ver README de esta carpeta).</summary>
    Native,
}

public sealed class TwainOptions
{
    public const string SectionName = "Capture:Twain";

    public TwainDriver Driver { get; set; } = TwainDriver.None;
    public string? PreferredSource { get; set; }
    public int Dpi { get; set; } = 300;
    public ScanColorMode ColorMode { get; set; } = ScanColorMode.Color;
    public bool Duplex { get; set; }
    public bool UseAutomaticFeeder { get; set; } = true;

    /// <summary>Mostrar la interfaz propia del fabricante antes de escanear.</summary>
    public bool ShowDriverUi { get; set; }
}

public sealed record ScanSettings(
    string? Source,
    int Dpi,
    ScanColorMode ColorMode,
    bool Duplex,
    bool UseAutomaticFeeder,
    bool ShowDriverUi);

public sealed class TwainException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Sesión con el Data Source Manager (DSM) de TWAIN. Aísla todos los detalles del protocolo
/// (estados 1-7, mensajes DG/DAT/MSG, transferencia nativa o por memoria) del resto de la app.
///
/// Para conectar un escáner real basta con implementar esta interfaz (p. ej. con la librería
/// NTwain o llamando a twaindsm.dll vía P/Invoke) y registrarla para <see cref="TwainDriver.Native"/>.
/// </summary>
public interface ITwainSession
{
    /// <summary>Verdadero si el DSM está instalado y hay al menos un origen.</summary>
    bool IsAvailable { get; }

    string? UnavailableReason { get; }

    /// <summary>Orígenes (escáneres) registrados en el DSM.</summary>
    IReadOnlyList<string> GetSources();

    /// <summary>
    /// Adquiere todas las páginas del alimentador. TWAIN exige el hilo de UI (STA) y un bucle de
    /// mensajes: las implementaciones deben invocarse desde el Dispatcher de WPF.
    /// Devuelve una lista vacía si el usuario cancela en la interfaz del driver.
    /// </summary>
    Task<IReadOnlyList<BitmapSource>> AcquireAsync(ScanSettings settings, CancellationToken cancellationToken);
}
