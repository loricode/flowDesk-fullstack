using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using FlowDesk.Desktop.Capture;
using FlowDesk.Shared.Contracts;
using FlowDesk.Shared.Documents;

namespace FlowDesk.Desktop.ViewModels;

public enum ItemState
{
    Ready,
    Uploading,
    Processing,
    Validated,
    Invalid,
    Failed,
}

/// <summary>Documento capturado en la bandeja local, con su ciclo de envío y procesamiento.</summary>
public sealed partial class CapturedItemViewModel : ObservableObject
{
    private ImageSource? _thumbnail;
    private ImageSource? _previewImage;

    public CapturedItemViewModel(CapturedDocument document) => Document = document;

    public CapturedDocument Document { get; }
    public string FileName => Document.FileName;
    public bool IsImage => FileSignatureValidator.IsImage(Document.ContentType);
    public bool IsPdf => Document.ContentType == "application/pdf";

    public string Details =>
        $"{TypeLabel} · {FormatSize(Document.SizeBytes)}" + (Document.PageCount > 1 ? $" · {Document.PageCount} páginas" : "");

    public string TypeLabel => Document.ContentType switch
    {
        "application/pdf" => "PDF",
        "image/png" => "PNG",
        "image/jpeg" => "JPG",
        "image/tiff" => "TIFF",
        _ => Document.ContentType,
    };

    public string SourceLabel => Document.Source == DocumentSources.Scanner ? "Escáner" : "Archivo";

    public string IconKind => IsPdf ? "FilePdfBox" : Document.Source == DocumentSources.Scanner ? "Scanner" : "FileImage";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateLabel), nameof(CanRemove), nameof(IsBusy), nameof(IsPending), nameof(IsProcessing))]
    private ItemState _state = ItemState.Ready;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string? _message;

    public Guid? DocumentId { get; set; }

    /// <summary>Ruta temporal usada por el visor PDF.</summary>
    public string? PreviewPath { get; set; }

    public bool CanRemove => State is ItemState.Ready or ItemState.Failed or ItemState.Validated or ItemState.Invalid;
    public bool IsBusy => State is ItemState.Uploading or ItemState.Processing;
    public bool IsProcessing => State == ItemState.Processing;
    public bool IsPending => State is ItemState.Ready or ItemState.Failed;

    public string StateLabel => State switch
    {
        ItemState.Ready => "Listo para enviar",
        ItemState.Uploading => "Enviando…",
        ItemState.Processing => "Procesando en servidor…",
        ItemState.Validated => "Validado",
        ItemState.Invalid => "Rechazado por validación",
        ItemState.Failed => "Error de envío",
        _ => State.ToString(),
    };

    public ImageSource? Thumbnail => _thumbnail ??= IsImage ? Decode(240) : null;

    public ImageSource? PreviewImage => _previewImage ??= IsImage ? Decode(1400) : null;

    private BitmapImage? Decode(int width)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = width;
            image.StreamSource = new MemoryStream(Document.Content);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            return null; // Imagen dañada: el backend la rechazará; aquí solo se omite la miniatura.
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / 1024.0 / 1024.0:0.##} MB",
    };
}
