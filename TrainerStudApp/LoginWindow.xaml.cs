using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TrainerStudApp.Presentation.ViewModels;
using UiPasswordBox = Wpf.Ui.Controls.PasswordBox;

namespace TrainerStudApp;

public partial class LoginWindow
{
    private readonly StudentAuthViewModel _viewModel;

    public LoginWindow(StudentAuthViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += (_, _) => _viewModel.ApplySessionRestoreHintIfAny();
    }

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        SyncPasswordFromChrome(LoginPwdChrome, pwd => _viewModel.LoginPassword = pwd);
        await _viewModel.LoginCommand.ExecuteAsync(null);
    }

    private async void OnConfirmRegisterClick(object sender, RoutedEventArgs e)
    {
        SyncPasswordFromChrome(RegisterPwdChrome, pwd => _viewModel.RegisterPassword = pwd);
        await _viewModel.RegisterConfirmCommand.ExecuteAsync(null);
    }

    private async void OnPasswordResetConfirmClick(object sender, RoutedEventArgs e)
    {
        SyncPasswordFromChrome(ResetPwdChrome, pwd => _viewModel.ResetNewPassword = pwd);
        await _viewModel.PasswordResetConfirmCommand.ExecuteAsync(null);
    }

    private void OnShowRegisterClick(object sender, RoutedEventArgs e) =>
        _viewModel.ShowRegisterCommand.Execute(null);

    private void OnShowLoginClick(object sender, RoutedEventArgs e) =>
        _viewModel.ShowLoginCommand.Execute(null);

    private void OnShowPasswordResetClick(object sender, RoutedEventArgs e) =>
        _viewModel.ShowPasswordResetCommand.Execute(null);

    private void OnShowLoginFromResetClick(object sender, RoutedEventArgs e) =>
        _viewModel.ShowLoginCommand.Execute(null);

    private static void SyncPasswordFromChrome(DependencyObject chrome, Action<string> apply)
    {
        if (FindVisualChild<System.Windows.Controls.PasswordBox>(chrome) is { } legacy)
        {
            apply(legacy.Password);
            return;
        }

        if (FindVisualChild<UiPasswordBox>(chrome) is { } ui)
        {
            apply(ui.Password);
            return;
        }
    }

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
