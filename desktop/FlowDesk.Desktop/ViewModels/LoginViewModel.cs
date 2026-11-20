using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowDesk.Desktop.Services;
using Microsoft.Extensions.Options;

namespace FlowDesk.Desktop.ViewModels;

public sealed partial class LoginViewModel(FlowDeskApiClient api, IOptions<ApiOptions> options) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _email = "operador@flowdesk.local";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _error;

    public string ServerUrl => options.Value.BaseUrl;

    /// <summary>Se dispara al autenticarse correctamente.</summary>
    public event EventHandler? LoggedIn;

    private bool CanLogin() => !IsBusy && !string.IsNullOrWhiteSpace(Email);

    /// <param name="password">La contraseña llega desde el PasswordBox (no se puede enlazar por seguridad).</param>
    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync(string? password)
    {
        Error = null;
        if (string.IsNullOrEmpty(password))
        {
            Error = "Ingrese la contraseña.";
            return;
        }

        IsBusy = true;
        try
        {
            await api.LoginAsync(Email.Trim(), password, CancellationToken.None);
            LoggedIn?.Invoke(this, EventArgs.Empty);
        }
        catch (ApiException ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
