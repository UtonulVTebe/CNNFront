using System.Windows;
using ExpertAdminTrainerApp.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ExpertAdminTrainerApp.Services;

public sealed class AppSessionNavigator(IServiceProvider services) : IAppNavigator
{
    private Window? _loginWindow;
    private Window? _mainWindow;

    public async Task InitializeAsync()
    {
        var mainVm = services.GetRequiredService<MainViewModel>();
        if (await mainVm.TryRestoreSessionAsync())
            ShowMain();
        else
            ShowLogin();
    }

    public void OpenMainAfterSuccessfulLogin()
    {
        // Нельзя закрывать окно входа первым: при ShutdownMode.OnLastWindowClose
        // на момент между Close() и Show() нет ни одного окна — приложение завершается.
        var loginToClose = _loginWindow;
        ShowMain();
        _mainWindow?.Activate();
        loginToClose?.Close();
    }

    public void ReturnToLoginAfterLogout()
    {
        var mainToClose = _mainWindow;
        ShowLogin();
        _loginWindow?.Activate();
        mainToClose?.Close();
    }

    private void ShowLogin()
    {
        var login = services.GetRequiredService<ExpertAdminTrainerApp.LoginWindow>();
        _loginWindow = login;
        login.Closed += (_, _) =>
        {
            if (ReferenceEquals(_loginWindow, login))
                _loginWindow = null;
        };
        login.Show();
    }

    private void ShowMain()
    {
        var main = services.GetRequiredService<ExpertAdminTrainerApp.MainWindow>();
        _mainWindow = main;
        main.Closed += (_, _) =>
        {
            if (ReferenceEquals(_mainWindow, main))
                _mainWindow = null;
        };
        main.Show();
    }
}
