using System.Windows;
using System.Windows.Threading;
using FlowDesk.Desktop.Capture;
using FlowDesk.Desktop.Capture.Twain;
using FlowDesk.Desktop.Services;
using FlowDesk.Desktop.ViewModels;
using FlowDesk.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowDesk.Desktop;

public partial class App : Application
{
    private IHost _host = default!;
    private string[] _startupArgs = [];

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _startupArgs = e.Args;

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = e.Args.Where(a => a.StartsWith("--")).ToArray(), // Las rutas de archivo no son configuración.
            ContentRootPath = AppContext.BaseDirectory,
        });

        ConfigureServices(builder.Services, builder.Configuration);
        _host = builder.Build();
        await _host.StartAsync();

        DispatcherUnhandledException += OnUnhandledException;
        ShowLogin();
    }

    private static void ConfigureServices(IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<ApiOptions>(configuration.GetSection(ApiOptions.SectionName));
        services.Configure<TwainOptions>(configuration.GetSection(TwainOptions.SectionName));

        services.AddSingleton<SessionContext>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<PreviewFileCache>();
        services.AddHttpClient<FlowDeskApiClient>((sp, http) =>
        {
            var api = sp.GetRequiredService<IOptions<ApiOptions>>().Value;
            http.BaseAddress = new Uri(api.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromMinutes(2);
        });

        // ---- Módulo DocumentCapture: cada origen es una implementación de IDocumentCaptureService ----
        services.Configure<HotFolderOptions>(configuration.GetSection(HotFolderOptions.SectionName));
        services.AddSingleton<HotFolderWatcher>();
        services.AddSingleton<IFileDialogService, WpfFileDialogService>();
        services.AddSingleton<FileCaptureService>();
        services.AddSingleton<IDocumentCaptureService>(sp => sp.GetRequiredService<FileCaptureService>());

        services.AddSingleton(CreateTwainSession);
        services.AddSingleton<IDocumentCaptureService, TwainCaptureService>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainViewModel>();
    }

    /// <summary>Selecciona la sesión TWAIN según configuración (Capture:Twain:Driver).</summary>
    private static ITwainSession CreateTwainSession(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<TwainOptions>>().Value;
        return options.Driver switch
        {
            TwainDriver.Simulated => new SimulatedTwainSession(),
            // TwainDriver.Native => new NativeTwainSession(...),  ← ver Capture/Twain/README.md
            TwainDriver.Native => new UnavailableTwainSession("El controlador TWAIN nativo aún no está instalado en esta versión."),
            _ => new UnavailableTwainSession("No hay un escáner TWAIN configurado. Por ahora cargue PDF o imágenes."),
        };
    }

    private void ShowLogin()
    {
        var vm = _host.Services.GetRequiredService<LoginViewModel>();
        var window = new LoginWindow(vm);
        vm.LoggedIn += (_, _) =>
        {
            ShowMain();
            window.Close();
        };
        window.Closed += (_, _) =>
        {
            if (Windows.Count == 0) Shutdown();
        };
        window.Show();
    }

    private void ShowMain()
    {
        var vm = _host.Services.GetRequiredService<MainViewModel>();
        var window = new MainWindow(vm);

        // "Abrir con" / "Enviar a" desde el Explorador: los archivos recibidos como argumentos
        // se cargan en la bandeja (una sola vez, en el primer inicio de sesión).
        var startupFiles = _startupArgs.Where(System.IO.File.Exists).ToList();
        _startupArgs = [];
        if (startupFiles.Count > 0)
            window.Loaded += async (_, _) => await vm.DropFilesAsync(startupFiles);

        vm.LoggedOut += (_, _) =>
        {
            ShowLogin();
            window.Close();
        };
        window.Closed += (_, _) =>
        {
            if (Windows.Count == 0) Shutdown();
        };
        window.Show();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _host.Services.GetRequiredService<ILogger<App>>().LogError(e.Exception, "Error no controlado.");
        _host.Services.GetRequiredService<NotificationService>().Show($"Error inesperado: {e.Exception.Message}");
        e.Handled = true;
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _host.Services.GetRequiredService<PreviewFileCache>().Dispose();
        await _host.StopAsync(TimeSpan.FromSeconds(2));
        _host.Dispose();
        base.OnExit(e);
    }
}
