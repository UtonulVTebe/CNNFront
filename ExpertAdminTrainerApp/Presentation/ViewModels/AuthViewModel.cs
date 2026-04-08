using System.Collections.Generic;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpertAdminTrainerApp.Domain;
using ExpertAdminTrainerApp.Services;

namespace ExpertAdminTrainerApp.Presentation.ViewModels;

public partial class AuthViewModel(IApiClient apiClient, MainViewModel mainViewModel, IAppNavigator appNavigator) : ObservableObject
{
    /// <summary>Показать на форме входа причину, если не сработало восстановление сессии при старте.</summary>
    public void ApplySessionRestoreHintIfAny()
    {
        var hint = mainViewModel.ConsumeLoginScreenHint();
        if (!string.IsNullOrWhiteSpace(hint))
            StatusMessage = hint;
    }

    /// <summary>true — форма входа, false — регистрация.</summary>
    [ObservableProperty] private bool isLoginView = true;

    // Вход
    [ObservableProperty] private string loginEmail = string.Empty;
    [ObservableProperty] private string loginPassword = string.Empty;
    [ObservableProperty] private string statusMessage = "Войдите под учётной записью Expert или Admin.";
    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private bool loginEmailInvalid;
    [ObservableProperty] private bool loginPasswordInvalid;

    // Регистрация (код на email → подтверждение)
    [ObservableProperty] private string registerEmail = string.Empty;
    [ObservableProperty] private string registerCode = string.Empty;
    [ObservableProperty] private string registerName = string.Empty;
    [ObservableProperty] private string registerPassword = string.Empty;
    [ObservableProperty] private bool registerCodeSent;

    [ObservableProperty] private bool registerEmailInvalid;
    [ObservableProperty] private bool registerCodeInvalid;
    [ObservableProperty] private bool registerNameInvalid;
    [ObservableProperty] private bool registerPasswordInvalid;

    public bool IsRegisterView => !IsLoginView;

    /// <summary>Регистрация, шаг 1: запрос кода на email.</summary>
    public bool ShowRegisterStepRequestCode => IsRegisterView && !RegisterCodeSent;

    /// <summary>Регистрация, шаг 2: код из письма, имя, пароль.</summary>
    public bool ShowRegisterStepConfirm => IsRegisterView && RegisterCodeSent;

    partial void OnIsLoginViewChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRegisterView));
        OnPropertyChanged(nameof(ShowRegisterStepRequestCode));
        OnPropertyChanged(nameof(ShowRegisterStepConfirm));
        ClearAllFieldValidation();
    }

    partial void OnRegisterCodeSentChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowRegisterStepRequestCode));
        OnPropertyChanged(nameof(ShowRegisterStepConfirm));
    }

    partial void OnLoginEmailChanged(string value) => LoginEmailInvalid = false;

    partial void OnLoginPasswordChanged(string value) => LoginPasswordInvalid = false;

    partial void OnRegisterEmailChanged(string value) => RegisterEmailInvalid = false;

    partial void OnRegisterCodeChanged(string value) => RegisterCodeInvalid = false;

    partial void OnRegisterNameChanged(string value) => RegisterNameInvalid = false;

    partial void OnRegisterPasswordChanged(string value) => RegisterPasswordInvalid = false;

    [RelayCommand]
    private void ShowRegister()
    {
        ClearAllFieldValidation();
        IsLoginView = false;
    }

    [RelayCommand]
    private void ShowLogin()
    {
        ClearAllFieldValidation();
        IsLoginView = true;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (!TryValidateLoginForm()) return;
        try
        {
            IsBusy = true;
            var auth = await apiClient.LoginAsync(new LoginDto { Email = LoginEmail.Trim(), Password = LoginPassword });
            if (!await mainViewModel.ApplyLoginResponseAsync(auth.AccessToken))
                StatusMessage = mainViewModel.StatusText;
            else
            {
                ClearAllFieldValidation();
                appNavigator.OpenMainAfterSuccessfulLogin();
            }
        }
        catch (Exception ex) { StatusMessage = FormatLoginError(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RequestRegisterCodeAsync()
    {
        if (!TryValidateRegisterStep1()) return;
        try
        {
            IsBusy = true;
            await apiClient.RegisterRequestCodeAsync(new RegisterRequestCodeDto { Email = RegisterEmail.Trim() });
            RegisterCodeSent = true;
            StatusMessage = "Проверьте почту и введите код, имя и пароль.";
        }
        catch (Exception ex) { StatusMessage = FormatRegisterRequestError(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ConfirmRegisterAsync()
    {
        if (!TryValidateRegisterStep2()) return;
        try
        {
            IsBusy = true;
            await apiClient.RegisterConfirmAsync(new RegisterConfirmDto
            {
                Email = RegisterEmail.Trim(),
                Code = RegisterCode.Trim(),
                Name = RegisterName.Trim(),
                Password = RegisterPassword
            });
            LoginEmail = RegisterEmail.Trim();
            LoginPassword = string.Empty;
            RegisterPassword = string.Empty;
            RegisterCode = string.Empty;
            RegisterCodeSent = false;
            IsLoginView = true;
            ClearAllFieldValidation();
            StatusMessage = "Регистрация завершена. Войдите с этим email и паролем.";
        }
        catch (Exception ex) { StatusMessage = FormatRegisterConfirmError(ex); }
        finally { IsBusy = false; }
    }

    private void ClearAllFieldValidation()
    {
        ClearLoginFieldValidation();
        ClearRegisterFieldValidation();
    }

    private void ClearLoginFieldValidation()
    {
        LoginEmailInvalid = false;
        LoginPasswordInvalid = false;
    }

    private void ClearRegisterFieldValidation()
    {
        RegisterEmailInvalid = false;
        RegisterCodeInvalid = false;
        RegisterNameInvalid = false;
        RegisterPasswordInvalid = false;
    }

    private bool TryValidateLoginForm()
    {
        ClearLoginFieldValidation();
        var parts = new List<string>();

        if (string.IsNullOrWhiteSpace(LoginEmail))
        {
            LoginEmailInvalid = true;
            parts.Add("укажите email");
        }
        else if (!IsValidEmailFormat(LoginEmail))
        {
            LoginEmailInvalid = true;
            parts.Add("неверный формат email (нужны «@» и домен с точкой)");
        }

        if (string.IsNullOrWhiteSpace(LoginPassword))
        {
            LoginPasswordInvalid = true;
            parts.Add("укажите пароль");
        }
        else if (LoginPassword.Length < MinPasswordLength)
        {
            LoginPasswordInvalid = true;
            parts.Add($"пароль не короче {MinPasswordLength} символов");
        }

        if (parts.Count == 0) return true;
        StatusMessage = "Проверьте поля с красной рамкой: " + string.Join("; ", parts) + ".";
        return false;
    }

    private bool TryValidateRegisterStep1()
    {
        ClearRegisterFieldValidation();
        if (string.IsNullOrWhiteSpace(RegisterEmail))
        {
            RegisterEmailInvalid = true;
            StatusMessage = "Проверьте поля с красной рамкой: укажите email.";
            return false;
        }
        if (!IsValidEmailFormat(RegisterEmail))
        {
            RegisterEmailInvalid = true;
            StatusMessage = "Проверьте поля с красной рамкой: неверный формат email (нужны «@» и домен с точкой).";
            return false;
        }
        return true;
    }

    private bool TryValidateRegisterStep2()
    {
        ClearRegisterFieldValidation();
        var parts = new List<string>();

        if (string.IsNullOrWhiteSpace(RegisterEmail))
        {
            RegisterEmailInvalid = true;
            parts.Add("отсутствует email (вернитесь к шагу 1)");
        }
        else if (!IsValidEmailFormat(RegisterEmail))
        {
            RegisterEmailInvalid = true;
            parts.Add("неверный формат email");
        }

        if (string.IsNullOrWhiteSpace(RegisterCode))
        {
            RegisterCodeInvalid = true;
            parts.Add("введите код из письма");
        }

        if (string.IsNullOrWhiteSpace(RegisterName))
        {
            RegisterNameInvalid = true;
            parts.Add("укажите имя");
        }
        else if (RegisterName.Trim().Length < MinNameLength)
        {
            RegisterNameInvalid = true;
            parts.Add($"имя не короче {MinNameLength} символов");
        }

        if (string.IsNullOrWhiteSpace(RegisterPassword))
        {
            RegisterPasswordInvalid = true;
            parts.Add("укажите пароль");
        }
        else if (RegisterPassword.Length < MinPasswordLength)
        {
            RegisterPasswordInvalid = true;
            parts.Add($"пароль не короче {MinPasswordLength} символов");
        }

        if (parts.Count == 0) return true;
        StatusMessage = "Проверьте поля с красной рамкой: " + string.Join("; ", parts) + ".";
        return false;
    }

    private const int MinPasswordLength = 8;
    private const int MinNameLength = 2;

    private static string FormatLoginError(Exception ex) => ex switch
    {
        ApiException { StatusCode: 401 } => "Неверный email или пароль.",
        ApiException api => $"Вход не выполнен. {api.Message}",
        HttpRequestException => "Вход не выполнен. Нет связи с сервером. Проверьте интернет и адрес API в appsettings.json.",
        TaskCanceledException => "Вход не выполнен. Превышено время ожидания. Проверьте сеть и попробуйте снова.",
        OperationCanceledException => "Вход не выполнен. Запрос отменён.",
        InvalidOperationException { Message: var m } when m.Contains("Empty API payload", StringComparison.Ordinal) =>
            "Вход не выполнен. Ответ сервера пустой или повреждён.",
        _ => "Вход не выполнен. Попробуйте позже."
    };

    private static string FormatRegisterRequestError(Exception ex) => ex switch
    {
        ApiException { StatusCode: 409 } => "Этот email уже используется. Войдите или укажите другой адрес.",
        ApiException api => $"Не удалось отправить код. {api.Message}",
        HttpRequestException => "Не удалось отправить код. Нет связи с сервером. Проверьте интернет и адрес API в appsettings.json.",
        TaskCanceledException => "Не удалось отправить код. Превышено время ожидания. Попробуйте снова.",
        OperationCanceledException => "Не удалось отправить код. Запрос отменён.",
        _ => "Не удалось отправить код. Попробуйте позже."
    };

    private static string FormatRegisterConfirmError(Exception ex) => ex switch
    {
        ApiException { StatusCode: 409 } => "Этот email уже зарегистрирован. Войдите с теми же данными.",
        ApiException api => $"Регистрация не завершена. {api.Message}",
        HttpRequestException => "Регистрация не завершена. Нет связи с сервером. Проверьте интернет и адрес API.",
        TaskCanceledException => "Регистрация не завершена. Превышено время ожидания. Попробуйте снова.",
        OperationCanceledException => "Регистрация не завершена. Запрос отменён.",
        _ => "Регистрация не завершена. Попробуйте позже."
    };

    /// <summary>Проверка: есть локальная часть, «@», домен с точкой и зона после точки.</summary>
    private static bool IsValidEmailFormat(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        email = email.Trim();
        var at = email.IndexOf('@');
        if (at <= 0 || at == email.Length - 1) return false;
        var local = email[..at];
        if (string.IsNullOrWhiteSpace(local)) return false;
        var domain = email[(at + 1)..];
        if (string.IsNullOrWhiteSpace(domain) || !domain.Contains('.')) return false;
        if (domain.StartsWith('.') || domain.EndsWith('.') || domain.Contains(' ', StringComparison.Ordinal)) return false;
        var lastDot = domain.LastIndexOf('.');
        if (lastDot < 0 || lastDot >= domain.Length - 1) return false;
        return true;
    }
}
