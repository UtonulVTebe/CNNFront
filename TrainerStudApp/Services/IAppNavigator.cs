namespace TrainerStudApp.Services;

/// <summary>Окно входа и главное окно после авторизации или выхода.</summary>
public interface IAppNavigator
{
    Task InitializeAsync();

    void OpenMainAfterSuccessfulLogin();

    void ReturnToLoginAfterLogout();
}
