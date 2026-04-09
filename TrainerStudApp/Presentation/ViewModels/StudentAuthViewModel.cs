using System.Net;
using System.Net.Http;
using System.Net.Mail;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainerStudApp.Domain;
using TrainerStudApp.Services;

namespace TrainerStudApp.Presentation.ViewModels;

public enum StudentAuthSection
{
    Login,
    Register,
    PasswordReset
}

public partial class StudentAuthViewModel(
    IApiClient apiClient,
    StudentMainViewModel mainViewModel,
    IAppNavigator appNavigator) : ObservableObject
{
    private const int MinPasswordLength = 8;
    private const int MinNameLength = 2;
    private const int MaxCodeLength = 32;

    [ObservableProperty] private StudentAuthSection authSection = StudentAuthSection.Login;

    [ObservableProperty] private string statusMessage = "Войдите или зарегистрируйтесь.";

    [ObservableProperty] private bool isBusy;

    // Вход
    [ObservableProperty] private string loginEmail = string.Empty;

    [ObservableProperty] private bool loginEmailInvalid;

    [ObservableProperty] private bool loginPasswordInvalid;

    [ObservableProperty] private string loginPassword = string.Empty;

    // Регистрация
    [ObservableProperty] private string registerEmail = string.Empty;

    [ObservableProperty] private string registerCode = string.Empty;

    [ObservableProperty] private string registerName = string.Empty;

    [ObservableProperty] private bool registerAwaitingCode;

    [ObservableProperty] private bool registerEmailInvalid;

    [ObservableProperty] private bool registerCodeInvalid;

    [ObservableProperty] private bool registerNameInvalid;

    [ObservableProperty] private bool registerPasswordInvalid;

    [ObservableProperty] private string registerPassword = string.Empty;

    // Сброс пароля
    [ObservableProperty] private string resetEmail = string.Empty;

    [ObservableProperty] private string resetCode = string.Empty;

    [ObservableProperty] private bool resetAwaitingCode;

    [ObservableProperty] private bool resetEmailInvalid;

    [ObservableProperty] private bool resetCodeInvalid;

    [ObservableProperty] private bool resetPasswordInvalid;

    [ObservableProperty] private string resetNewPassword = string.Empty;

    public bool IsLoginView => AuthSection == StudentAuthSection.Login;

    public bool IsRegisterView => AuthSection == StudentAuthSection.Register;

    public bool IsPasswordResetView => AuthSection == StudentAuthSection.PasswordReset;

    public bool ShowRegisterStepRequestCode =>
        AuthSection == StudentAuthSection.Register && !RegisterAwaitingCode;

    public bool ShowRegisterStepConfirm =>
        AuthSection == StudentAuthSection.Register && RegisterAwaitingCode;

    public bool ShowPasswordResetStepRequest =>
        AuthSection == StudentAuthSection.PasswordReset && !ResetAwaitingCode;

    public bool ShowPasswordResetStepConfirm =>
        AuthSection == StudentAuthSection.PasswordReset && ResetAwaitingCode;

    public void ApplySessionRestoreHintIfAny()
    {
        var hint = mainViewModel.ConsumeLoginScreenHint();
        if (!string.IsNullOrWhiteSpace(hint))
            StatusMessage = hint;
    }

    partial void OnAuthSectionChanged(StudentAuthSection value)
    {
        ClearValidationFlags();
        ClearPasswordFields();
        if (value == StudentAuthSection.Register)
            RegisterAwaitingCode = false;
        if (value == StudentAuthSection.PasswordReset)
            ResetAwaitingCode = false;
        NotifyAuthViewFlags();
    }

    partial void OnRegisterAwaitingCodeChanged(bool value) => NotifyRegisterStepFlags();

    partial void OnResetAwaitingCodeChanged(bool value) => NotifyPasswordResetStepFlags();

    private void NotifyAuthViewFlags()
    {
        OnPropertyChanged(nameof(IsLoginView));
        OnPropertyChanged(nameof(IsRegisterView));
        OnPropertyChanged(nameof(IsPasswordResetView));
        NotifyRegisterStepFlags();
        NotifyPasswordResetStepFlags();
    }

    private void NotifyRegisterStepFlags()
    {
        OnPropertyChanged(nameof(ShowRegisterStepRequestCode));
        OnPropertyChanged(nameof(ShowRegisterStepConfirm));
    }

    private void NotifyPasswordResetStepFlags()
    {
        OnPropertyChanged(nameof(ShowPasswordResetStepRequest));
        OnPropertyChanged(nameof(ShowPasswordResetStepConfirm));
    }

    partial void OnLoginEmailChanged(string value) => LoginEmailInvalid = false;

    partial void OnLoginPasswordChanged(string value) => LoginPasswordInvalid = false;

    partial void OnRegisterEmailChanged(string value) => RegisterEmailInvalid = false;

    partial void OnRegisterCodeChanged(string value) => RegisterCodeInvalid = false;

    partial void OnRegisterNameChanged(string value) => RegisterNameInvalid = false;

    partial void OnResetEmailChanged(string value) => ResetEmailInvalid = false;

    partial void OnResetCodeChanged(string value) => ResetCodeInvalid = false;

    partial void OnRegisterPasswordChanged(string value) => RegisterPasswordInvalid = false;

    partial void OnResetNewPasswordChanged(string value) => ResetPasswordInvalid = false;

    private void ClearPasswordFields()
    {
        LoginPassword = string.Empty;
        RegisterPassword = string.Empty;
        ResetNewPassword = string.Empty;
    }

    private void ClearValidationFlags()
    {
        LoginEmailInvalid = false;
        LoginPasswordInvalid = false;
        RegisterEmailInvalid = false;
        RegisterCodeInvalid = false;
        RegisterNameInvalid = false;
        RegisterPasswordInvalid = false;
        ResetEmailInvalid = false;
        ResetCodeInvalid = false;
        ResetPasswordInvalid = false;
    }

    [RelayCommand]
    private void ShowLogin() => AuthSection = StudentAuthSection.Login;

    [RelayCommand]
    private void ShowRegister() => AuthSection = StudentAuthSection.Register;

    [RelayCommand]
    private void ShowPasswordReset() => AuthSection = StudentAuthSection.PasswordReset;

    [RelayCommand]
    private async Task LoginAsync()
    {
        ClearValidationFlags();
        var email = (LoginEmail ?? string.Empty).Trim();
        var password = LoginPassword ?? string.Empty;
        if (string.IsNullOrEmpty(email))
        {
            LoginEmailInvalid = true;
            StatusMessage = "Введите email.";
            return;
        }

        if (!IsValidEmailFormat(email))
        {
            LoginEmailInvalid = true;
            StatusMessage = "Проверьте формат email.";
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            LoginPasswordInvalid = true;
            StatusMessage = "Введите пароль.";
            return;
        }

        IsBusy = true;
        try
        {
            await apiClient.LoginAsync(new LoginDto { Email = email, Password = password }, default);
            LoginPassword = string.Empty;
            mainViewModel.ApplyLoggedInState(email, "Вход выполнен.");
            appNavigator.OpenMainAfterSuccessfulLogin();
        }
        catch (Exception ex)
        {
            StatusMessage = FormatAuthApiError(ex, fallbackPrefix: "Вход не выполнен");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RegisterSendCodeAsync()
    {
        ClearValidationFlags();
        var email = (RegisterEmail ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(email))
        {
            RegisterEmailInvalid = true;
            StatusMessage = "Укажите email для регистрации.";
            return;
        }

        if (!IsValidEmailFormat(email))
        {
            RegisterEmailInvalid = true;
            StatusMessage = "Проверьте формат email.";
            return;
        }

        IsBusy = true;
        try
        {
            await apiClient.RegisterRequestCodeAsync(
                new RegisterRequestCodeDto { Email = email }, default);
            RegisterAwaitingCode = true;
            StatusMessage = "Если почта доступна, на неё отправлен код. Введите код, имя и пароль ниже.";
        }
        catch (Exception ex)
        {
            StatusMessage = FormatAuthApiError(ex, fallbackPrefix: "Не удалось отправить код");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RegisterConfirmAsync()
    {
        ClearValidationFlags();
        var email = (RegisterEmail ?? string.Empty).Trim();
        var code = (RegisterCode ?? string.Empty).Trim();
        var name = (RegisterName ?? string.Empty).Trim();
        var password = RegisterPassword ?? string.Empty;
        var ok = true;
        if (string.IsNullOrEmpty(email) || !IsValidEmailFormat(email))
        {
            RegisterEmailInvalid = true;
            ok = false;
        }

        if (string.IsNullOrEmpty(code) || code.Length > MaxCodeLength)
        {
            RegisterCodeInvalid = true;
            ok = false;
        }

        if (name.Length < MinNameLength)
        {
            RegisterNameInvalid = true;
            ok = false;
        }

        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
        {
            RegisterPasswordInvalid = true;
            ok = false;
        }

        if (!ok)
        {
            StatusMessage =
                $"Заполните поля: email, код (до {MaxCodeLength} символов), имя не короче {MinNameLength} символов, пароль не короче {MinPasswordLength}.";
            return;
        }

        IsBusy = true;
        try
        {
            await apiClient.RegisterConfirmAsync(
                new RegisterConfirmDto
                {
                    Email = email,
                    Code = code,
                    Name = name,
                    Password = password
                },
                default);

            try
            {
                await apiClient.LoginAsync(new LoginDto { Email = email, Password = password }, default);
                mainViewModel.ApplyLoggedInState(email, "Регистрация завершена, вы вошли.");
                RegisterAwaitingCode = false;
                RegisterCode = string.Empty;
                RegisterName = string.Empty;
                RegisterPassword = string.Empty;
                appNavigator.OpenMainAfterSuccessfulLogin();
            }
            catch (Exception loginEx)
            {
                StatusMessage =
                    $"Аккаунт создан, но вход не выполнен: {FormatAuthApiError(loginEx, "Ошибка входа")}. Войдите вручную.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = FormatAuthApiError(ex, fallbackPrefix: "Подтверждение регистрации не удалось");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PasswordResetSendCodeAsync()
    {
        ClearValidationFlags();
        var email = (ResetEmail ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(email))
        {
            ResetEmailInvalid = true;
            StatusMessage = "Укажите email для сброса пароля.";
            return;
        }

        if (!IsValidEmailFormat(email))
        {
            ResetEmailInvalid = true;
            StatusMessage = "Проверьте формат email.";
            return;
        }

        IsBusy = true;
        try
        {
            await apiClient.PasswordResetRequestAsync(
                new PasswordResetRequestCodeDto { Email = email }, default);
            ResetAwaitingCode = true;
            StatusMessage = "Если почта найдена, на неё отправлен код. Введите код и новый пароль.";
        }
        catch (Exception ex)
        {
            StatusMessage = FormatAuthApiError(ex, fallbackPrefix: "Запрос кода не выполнен");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PasswordResetConfirmAsync()
    {
        ClearValidationFlags();
        var email = (ResetEmail ?? string.Empty).Trim();
        var code = (ResetCode ?? string.Empty).Trim();
        var newPassword = ResetNewPassword ?? string.Empty;
        var ok = true;
        if (string.IsNullOrEmpty(email) || !IsValidEmailFormat(email))
        {
            ResetEmailInvalid = true;
            ok = false;
        }

        if (string.IsNullOrEmpty(code) || code.Length > MaxCodeLength)
        {
            ResetCodeInvalid = true;
            ok = false;
        }

        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < MinPasswordLength)
        {
            ResetPasswordInvalid = true;
            ok = false;
        }

        if (!ok)
        {
            StatusMessage =
                $"Укажите корректный email, код (до {MaxCodeLength} символов) и новый пароль не короче {MinPasswordLength} символов.";
            return;
        }

        IsBusy = true;
        try
        {
            await apiClient.PasswordResetConfirmAsync(
                new PasswordResetConfirmDto
                {
                    Email = email,
                    Code = code,
                    NewPassword = newPassword
                },
                default);
            ResetAwaitingCode = false;
            ResetCode = string.Empty;
            ResetNewPassword = string.Empty;
            LoginEmail = email;
            ClearValidationFlags();
            StatusMessage = "Пароль обновлён. Войдите с новым паролём.";
            AuthSection = StudentAuthSection.Login;
        }
        catch (Exception ex)
        {
            StatusMessage = FormatAuthApiError(ex, fallbackPrefix: "Сброс пароля не выполнен");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool IsValidEmailFormat(string email)
    {
        email = email.Trim();
        if (email.Length < 5 || email.Length > 254)
            return false;
        try
        {
            var addr = new MailAddress(email);
            return string.Equals(addr.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Сообщение для строки статуса: тело API без дублирования сырого JSON из <see cref="HttpApiException"/>.</summary>
    private static string FormatAuthApiError(Exception ex, string fallbackPrefix)
    {
        if (ex is HttpApiException http)
        {
            var raw = http.Message;
            var shortMsg = raw.Contains(" | ", StringComparison.Ordinal)
                ? raw.Split(" | ", 2, StringSplitOptions.TrimEntries)[0]
                : raw.Trim();

            if (string.IsNullOrWhiteSpace(shortMsg))
                shortMsg = "Ошибка сервера.";

            var code = (int)http.StatusCode;
            return http.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Неверный email или пароль.",
                HttpStatusCode.Forbidden => "Доступ запрещён.",
                HttpStatusCode.NotFound => "Ресурс не найден.",
                HttpStatusCode.Conflict => shortMsg,
                HttpStatusCode.BadRequest => shortMsg,
                HttpStatusCode.UnprocessableEntity => shortMsg,
                HttpStatusCode.TooManyRequests => "Слишком много запросов. Подождите немного.",
                _ => code >= (int)HttpStatusCode.InternalServerError
                    ? "На стороне сервера произошла ошибка. Попробуйте позже."
                    : $"{fallbackPrefix}: {shortMsg}"
            };
        }

        if (ex is HttpRequestException)
            return "Не удалось связаться с сервером. Проверьте адрес API в appsettings и подключение к сети.";

        if (ex is TaskCanceledException)
            return "Превышено время ожидания ответа сервера.";

        return $"{fallbackPrefix}: {ex.Message}";
    }
}
