using System.IO;
using System.Windows;
using FlowDesk.Desktop.ViewModels;
using Microsoft.Web.WebView2.Wpf;

namespace FlowDesk.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        // Carpeta de datos del visor PDF fuera del directorio de instalación.
        PdfViewer.CreationProperties = new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlowDesk", "WebView2"),
        };

        DataContext = _viewModel = viewModel;
        Loaded += async (_, _) => await _viewModel.LoadExpedientsAsync();
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            await _viewModel.DropFilesAsync(paths);
    }
}
