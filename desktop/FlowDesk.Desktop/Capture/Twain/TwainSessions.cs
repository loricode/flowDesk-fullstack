using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowDesk.Desktop.Capture.Twain;

/// <summary>Sesión por defecto mientras no haya un escáner configurado.</summary>
public sealed class UnavailableTwainSession(string reason) : ITwainSession
{
    public bool IsAvailable => false;
    public string? UnavailableReason => reason;
    public IReadOnlyList<string> GetSources() => [];

    public Task<IReadOnlyList<BitmapSource>> AcquireAsync(ScanSettings settings, CancellationToken cancellationToken) =>
        throw new TwainException(reason);
}

/// <summary>
/// Simulador de escáner: produce páginas tamaño carta renderizadas con WPF. Permite demostrar y
/// probar el flujo completo (captura → TIFF multipágina → envío → Worker) sin hardware.
/// </summary>
public sealed class SimulatedTwainSession : ITwainSession
{
    private const int PagesPerBatch = 2;

    public bool IsAvailable => true;
    public string? UnavailableReason => null;
    public IReadOnlyList<string> GetSources() => ["FlowDesk Virtual Scanner"];

    public async Task<IReadOnlyList<BitmapSource>> AcquireAsync(ScanSettings settings, CancellationToken cancellationToken)
    {
        var pages = new List<BitmapSource>();
        var count = settings.Duplex ? PagesPerBatch * 2 : PagesPerBatch;
        for (var i = 1; i <= count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(400, cancellationToken); // Simula el avance del alimentador.
            pages.Add(RenderPage(i, count, settings));
        }
        return pages;
    }

    private static BitmapSource RenderPage(int number, int total, ScanSettings settings)
    {
        // Resolución reducida para la simulación; un escáner real entregaría settings.Dpi.
        const double dpi = 100;
        int width = (int)(8.5 * dpi), height = (int)(11 * dpi);
        var typeface = new Typeface("Segoe UI");
        var pixelsPerDip = 1.0;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1B, 0x25, 0x33)), null, new Rect(0, 0, width, 70));

            var title = new FormattedText("FlowDesk · Documento digitalizado (simulación)", CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, typeface, 22, Brushes.White, pixelsPerDip);
            dc.DrawText(title, new Point(40, 20));

            var body = new FormattedText(
                $"Página {number} de {total}\nResolución solicitada: {settings.Dpi} DPI\nModo: {settings.ColorMode}\n" +
                $"Dúplex: {(settings.Duplex ? "sí" : "no")}\nFecha: {DateTime.Now:g}",
                CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, 18,
                new SolidColorBrush(Color.FromRgb(0x2A, 0x36, 0x47)), pixelsPerDip);
            dc.DrawText(body, new Point(40, 120));

            var line = new Pen(new SolidColorBrush(Color.FromRgb(0xD5, 0xD9, 0xE0)), 1);
            for (var y = 320; y < height - 60; y += 28)
                dc.DrawLine(line, new Point(40, y), new Point(width - 40, y));

            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xD9, 0x73, 0x4E)), null, new Rect(0, height - 12, width, 12));
        }

        var bitmap = new RenderTargetBitmap(width, height, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        BitmapSource result = settings.ColorMode == ScanColorMode.Color
            ? bitmap
            : new FormatConvertedBitmap(bitmap, PixelFormats.Gray8, null, 0);
        result.Freeze();
        return result;
    }
}
