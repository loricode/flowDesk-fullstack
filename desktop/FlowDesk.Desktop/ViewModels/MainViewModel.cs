using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowDesk.Desktop.Capture;
using FlowDesk.Desktop.Services;
using FlowDesk.Shared.Contracts;

namespace FlowDesk.Desktop.ViewModels;

/// <summary>
/// Pantalla principal del módulo DocumentCapture: elegir expediente, capturar (archivos o escáner),
/// revisar, enviar y seguir el resultado del procesamiento.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1.5);
    private const int MaxPolls = 40;

    private readonly FlowDeskApiClient _api;
    private readonly SessionContext _session;
    private readonly FileCaptureService _fileCapture;
    private readonly IDocumentCaptureService? _scanner;
    private readonly PreviewFileCache _previewCache;
    private readonly HotFolderWatcher _hotFolder;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private CancellationTokenSource? _searchDebounce;

    public MainViewModel(
        FlowDeskApiClient api,
        SessionContext session,
        IEnumerable<IDocumentCaptureService> captureServices,
        PreviewFileCache previewCache,
        NotificationService notifications,
        HotFolderWatcher hotFolder)
    {
        _api = api;
        _session = session;
        _previewCache = previewCache;
        _hotFolder = hotFolder;
        Notifications = notifications;

        _hotFolder.FileArrived += OnHotFolderFile;
        _hotFolder.Start();

        var services = captureServices.ToList();
        _fileCapture = services.OfType<FileCaptureService>().Single();
        _scanner = services.FirstOrDefault(s => s.Kind == CaptureSourceKind.Scanner);

        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(PendingCount));
            UploadCommand.NotifyCanExecuteChanged();
            ClearCommand.NotifyCanExecuteChanged();
        };
    }

    public NotificationService Notifications { get; }

    public string UserName => _session.User?.FullName ?? "";
    public string UserRoles => string.Join(" · ", _session.User?.Roles ?? []);
    public string UserInitials =>
        string.Concat(UserName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));

    public bool CanCreateExpedient => _session.Can(FlowDeskPermissions.ExpedientsCreate);
    public bool CanUploadDocuments => _session.Can(FlowDeskPermissions.DocumentsUpload);

    public bool IsHotFolderEnabled => _hotFolder.IsEnabled;
    public string HotFolderLabel => $"Carpeta vigilada: {_hotFolder.FolderPath}";

    /// <summary>Archivo depositado en la carpeta vigilada: se importa en el hilo de UI.</summary>
    private void OnHotFolderFile(object? sender, string path)
    {
        void Import() => _ = ImportFromHotFolderAsync(path);
        if (_ui is null) Import(); else _ui.Post(_ => Import(), null);
    }

    private async Task ImportFromHotFolderAsync(string path)
    {
        await DropFilesAsync([path]);
        _hotFolder.MarkProcessed(path);
    }

    public bool IsScannerAvailable => _scanner?.IsAvailable == true;
    public string ScannerHint => _scanner switch
    {
        null => "Captura por escáner no instalada.",
        { IsAvailable: true } s => $"Digitalizar con {s.DisplayName}",
        var s => s.UnavailableReason ?? "Escáner no disponible.",
    };

    public event EventHandler? LoggedOut;

    // ---------------- Expedientes ----------------

    public ObservableCollection<ExpedientSummaryDto> Expedients { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelectedExpedient))]
    private ExpedientSummaryDto? _selectedExpedient;

    public bool HasSelectedExpedient => SelectedExpedient is not null;

    [ObservableProperty]
    private string _search = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoExpedients))]
    private bool _isLoadingExpedients;

    public bool ShowNoExpedients => !IsLoadingExpedients && Expedients.Count == 0;

    partial void OnSearchChanged(string value)
    {
        _searchDebounce?.Cancel();
        _searchDebounce = new CancellationTokenSource();
        var token = _searchDebounce.Token;
        _ = Task.Delay(350, token).ContinueWith(_ => LoadExpedientsAsync(),
            token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());
    }

    [RelayCommand]
    public async Task LoadExpedientsAsync()
    {
        IsLoadingExpedients = true;
        try
        {
            var selectedId = SelectedExpedient?.Id;
            var result = await _api.SearchExpedientsAsync(Search, CancellationToken.None);
            Expedients.Clear();
            foreach (var e in result.Items) Expedients.Add(e);
            SelectedExpedient = Expedients.FirstOrDefault(e => e.Id == selectedId);
        }
        catch (ApiException ex)
        {
            Notifications.Show(ex.Message);
        }
        finally
        {
            IsLoadingExpedients = false;
        }
    }

    // ---------------- Nuevo expediente ----------------

    public IReadOnlyList<string> Priorities { get; } = ExpedientPriorities.All;
    public ObservableCollection<UserLookupDto> AssignableUsers { get; } = [];

    [ObservableProperty] private bool _isNewExpedientOpen;
    [ObservableProperty] private string _newTitle = "";
    [ObservableProperty] private string _newApplicant = "";
    [ObservableProperty] private string? _newDescription;
    [ObservableProperty] private string _newPriority = ExpedientPriorities.Normal;
    [ObservableProperty] private UserLookupDto? _newAssignee;
    [ObservableProperty] private string? _newExpedientError;
    [ObservableProperty] private bool _isCreatingExpedient;

    [RelayCommand]
    private async Task OpenNewExpedientAsync()
    {
        NewTitle = NewApplicant = "";
        NewDescription = NewExpedientError = null;
        NewPriority = ExpedientPriorities.Normal;
        NewAssignee = null;
        IsNewExpedientOpen = true;

        if (AssignableUsers.Count == 0)
        {
            try
            {
                foreach (var u in await _api.GetAssignableUsersAsync(CancellationToken.None)) AssignableUsers.Add(u);
            }
            catch (ApiException ex)
            {
                NewExpedientError = ex.Message;
            }
        }
    }

    [RelayCommand]
    private void CancelNewExpedient() => IsNewExpedientOpen = false;

    [RelayCommand]
    private async Task CreateExpedientAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTitle) || string.IsNullOrWhiteSpace(NewApplicant))
        {
            NewExpedientError = "Título y solicitante son obligatorios.";
            return;
        }

        IsCreatingExpedient = true;
        NewExpedientError = null;
        try
        {
            var created = await _api.CreateExpedientAsync(
                new CreateExpedientRequest(NewTitle.Trim(), NewApplicant.Trim(), NewDescription, NewPriority, NewAssignee?.Id),
                CancellationToken.None);

            IsNewExpedientOpen = false;
            Search = "";
            await LoadExpedientsAsync();
            SelectedExpedient = Expedients.FirstOrDefault(e => e.Id == created.Id);
            Notifications.Show($"Expediente {created.Code} creado.");
        }
        catch (ApiException ex)
        {
            NewExpedientError = ex.Message;
        }
        finally
        {
            IsCreatingExpedient = false;
        }
    }

    // ---------------- Captura ----------------

    public ObservableCollection<CapturedItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPdfPreview), nameof(HasImagePreview), nameof(PreviewUri))]
    private CapturedItemViewModel? _selectedItem;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand), nameof(ScanCommand))]
    private bool _isCapturing;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand), nameof(ClearCommand))]
    private bool _isUploading;

    public bool HasItems => Items.Count > 0;
    public int PendingCount => Items.Count(i => i.IsPending);
    public bool HasPdfPreview => SelectedItem?.IsPdf == true;
    public bool HasImagePreview => SelectedItem?.IsImage == true;

    public Uri? PreviewUri
    {
        get
        {
            if (SelectedItem is not { IsPdf: true } item) return null;
            item.PreviewPath ??= _previewCache.Write(item.FileName, item.Document.Content);
            return new Uri(item.PreviewPath);
        }
    }

    [RelayCommand] private Task LoadPdfAsync() => CaptureAsync(_fileCapture, new CaptureRequest(CaptureMode.Pdf));
    [RelayCommand] private Task LoadImagesAsync() => CaptureAsync(_fileCapture, new CaptureRequest(CaptureMode.Images));
    [RelayCommand] private Task SelectDocumentsAsync() => CaptureAsync(_fileCapture, new CaptureRequest());

    private bool CanScan() => IsScannerAvailable && !IsCapturing;

    [RelayCommand(CanExecute = nameof(CanScan))]
    private Task ScanAsync() => CaptureAsync(_scanner!, new CaptureRequest(CaptureMode.Images, AllowMultiple: false));

    /// <summary>Arrastrar y soltar archivos sobre la zona de captura.</summary>
    public async Task DropFilesAsync(IEnumerable<string> paths) =>
        ApplyResult(await _fileCapture.ImportAsync(paths, CancellationToken.None));

    private async Task CaptureAsync(IDocumentCaptureService service, CaptureRequest request)
    {
        IsCapturing = true;
        try
        {
            ApplyResult(await service.CaptureAsync(request, CancellationToken.None));
        }
        finally
        {
            IsCapturing = false;
        }
    }

    private void ApplyResult(DocumentCaptureResult result)
    {
        foreach (var document in result.Documents)
        {
            var item = new CapturedItemViewModel(document);
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CapturedItemViewModel.State))
                {
                    OnPropertyChanged(nameof(PendingCount));
                    UploadCommand.NotifyCanExecuteChanged();
                }
            };
            Items.Add(item);
        }

        if (result.Documents.Count > 0) SelectedItem = Items[^1];

        if (result.Errors.Count > 0)
        {
            var first = result.Errors[0];
            var more = result.Errors.Count > 1 ? $" (+{result.Errors.Count - 1} más)" : "";
            Notifications.Show($"{first.Item}: {first.Message}{more}");
        }
        else if (result.Documents.Count > 0)
        {
            Notifications.Show(result.Documents.Count == 1
                ? $"«{result.Documents[0].FileName}» listo para enviar."
                : $"{result.Documents.Count} documentos listos para enviar.");
        }
    }

    [RelayCommand]
    private void RemoveItem(CapturedItemViewModel? item)
    {
        if (item is null || !item.CanRemove) return;
        Items.Remove(item);
        if (SelectedItem == item) SelectedItem = Items.LastOrDefault();
    }

    private bool CanClear() => HasItems && !IsUploading;

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        foreach (var item in Items.Where(i => i.CanRemove).ToList()) Items.Remove(item);
        SelectedItem = Items.LastOrDefault();
    }

    // ---------------- Envío ----------------

    private bool CanUpload() =>
        CanUploadDocuments && SelectedExpedient is not null && !IsUploading && !IsCapturing && Items.Any(i => i.IsPending);

    [RelayCommand(CanExecute = nameof(CanUpload))]
    private async Task UploadAsync()
    {
        var expedient = SelectedExpedient!;
        IsUploading = true;
        var sent = 0;

        try
        {
            foreach (var item in Items.Where(i => i.IsPending).ToList())
            {
                item.State = ItemState.Uploading;
                item.Message = null;
                item.Progress = 0;
                try
                {
                    var progress = new Progress<double>(p => item.Progress = p * 100);
                    var dto = await _api.UploadDocumentAsync(expedient.Id, item.Document, progress, CancellationToken.None);
                    item.DocumentId = dto.Id;
                    item.Message = dto.CurrentVersion > 1 ? $"Registrado como versión {dto.CurrentVersion}." : null;
                    item.State = ItemState.Processing;
                    sent++;
                    _ = TrackProcessingAsync(item);
                }
                catch (ApiException ex)
                {
                    item.State = ItemState.Failed;
                    item.Message = ex.Message;
                }
            }
        }
        finally
        {
            IsUploading = false;
        }

        if (sent > 0)
        {
            Notifications.Show($"{sent} documento(s) enviados a {expedient.Code}.");
            await LoadExpedientsAsync();
        }
    }

    /// <summary>Consulta el resultado del procesamiento asíncrono realizado por el Worker.</summary>
    private async Task TrackProcessingAsync(CapturedItemViewModel item)
    {
        for (var i = 0; i < MaxPolls && item.DocumentId is { } id; i++)
        {
            await Task.Delay(PollInterval);
            try
            {
                var doc = await _api.GetDocumentAsync(id, CancellationToken.None);
                if (doc.Status == DocumentStatuses.Pending) continue;

                item.State = doc.Status == DocumentStatuses.Validated ? ItemState.Validated : ItemState.Invalid;
                item.Message = doc.ValidationMessage ?? (item.State == ItemState.Validated ? "Hash verificado, sin duplicados." : null);
                return;
            }
            catch (ApiException)
            {
                // Error transitorio: se reintenta en la siguiente vuelta.
            }
        }

        if (item.State == ItemState.Processing)
            item.Message = "El servidor sigue procesando. Consulte el estado en la aplicación web.";
    }

    // ---------------- Sesión ----------------

    [RelayCommand]
    private void Logout()
    {
        _hotFolder.FileArrived -= OnHotFolderFile;
        _session.End();
        LoggedOut?.Invoke(this, EventArgs.Empty);
    }
}
