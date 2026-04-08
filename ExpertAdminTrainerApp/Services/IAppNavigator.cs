namespace ExpertAdminTrainerApp.Services;

/// <summary>Открытие окон входа и главного окна после авторизации или выхода.</summary>
public interface IAppNavigator
{
    Task InitializeAsync();

    void OpenMainAfterSuccessfulLogin();

    void ReturnToLoginAfterLogout();
}
