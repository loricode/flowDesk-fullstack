using System.Windows;
using FlowDesk.Desktop.ViewModels;

namespace FlowDesk.Desktop.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        Loaded += (_, _) => PasswordInput.Focus();
    }

    // El PasswordBox no expone la contraseña por binding (por diseño de WPF): se entrega al comando.
    // IsDefault="True" en el botón hace que Enter también envíe el formulario.
    private void OnLoginClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.LoginCommand.CanExecute(PasswordInput.Password))
            _viewModel.LoginCommand.Execute(PasswordInput.Password);
    }
}
