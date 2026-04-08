using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExpertAdminTrainerApp.Presentation.ViewModels;

namespace ExpertAdminTrainerApp;

public partial class LoginWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly AuthViewModel _viewModel;

    public LoginWindow(AuthViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += (_, _) => _viewModel.ApplySessionRestoreHintIfAny();
    }

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        if (FindVisualChild<PasswordBox>(LoginPwdChrome) is { } pb)
            _viewModel.LoginPassword = pb.Password;
        await _viewModel.LoginCommand.ExecuteAsync(null);
    }

    private async void OnConfirmRegisterClick(object sender, RoutedEventArgs e)
    {
        if (FindVisualChild<PasswordBox>(RegisterPwdChrome) is { } pb)
            _viewModel.RegisterPassword = pb.Password;
        await _viewModel.ConfirmRegisterCommand.ExecuteAsync(null);
    }

    private void OnShowRegisterClick(object sender, RoutedEventArgs e) =>
        _viewModel.ShowRegisterCommand.Execute(null);

    private void OnShowLoginClick(object sender, RoutedEventArgs e) =>
        _viewModel.ShowLoginCommand.Execute(null);

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;
            var nested = FindVisualChild<T>(child);
            if (nested is not null)
                return nested;
        }
        return null;
    }
}
