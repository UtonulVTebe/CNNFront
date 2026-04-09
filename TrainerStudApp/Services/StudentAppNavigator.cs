using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TrainerStudApp.Presentation.ViewModels;

namespace TrainerStudApp.Services;

public sealed class StudentAppNavigator(IServiceProvider services) : IAppNavigator
{
    private Window? _loginWindow;
    private Window? _mainWindow;

    public async Task InitializeAsync()
    {
        var mainVm = services.GetRequiredService<StudentMainViewModel>();
        if (await mainVm.TryRestoreSessionAsync())
            ShowMain();
        else
            ShowLogin();
    }

    public void OpenMainAfterSuccessfulLogin()
    {
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
        var login = services.GetRequiredService<LoginWindow>();
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
        var main = services.GetRequiredService<MainWindow>();
        _mainWindow = main;
        main.Closed += (_, _) =>
        {
            if (ReferenceEquals(_mainWindow, main))
                _mainWindow = null;
        };
        main.Show();
    }
}
