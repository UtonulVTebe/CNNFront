using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainerStudApp.Domain;
using TrainerStudApp.Services;

namespace TrainerStudApp.Presentation.ViewModels;

public partial class StudentMainViewModel(
    IApiClient apiClient,
    ITokenStore tokenStore,
    BlankTemplateSyncService blankSync,
    ExamSessionViewModel examSession,
    StudentOrdersViewModel orders) : ObservableObject
{
    [ObservableProperty] private string email = string.Empty;

    [ObservableProperty] private string statusText = "Откройте вкладку «Профиль» для входа или регистрации.";

    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private bool isAuthenticated;

    [ObservableProperty] private string userDisplay = string.Empty;

    [ObservableProperty] private string registerEmail = string.Empty;

    [ObservableProperty] private string registerCode = string.Empty;

    [ObservableProperty] private string registerName = string.Empty;

    [ObservableProperty] private bool registerAwaitingCode;

    [ObservableProperty] private string resetEmail = string.Empty;

    [ObservableProperty] private string resetCode = string.Empty;

    [ObservableProperty] private bool resetAwaitingCode;

    [ObservableProperty] private CnnListItemDto? selectedCnn;

    [ObservableProperty] private CnnDetailsDto? currentDetails;

    [ObservableProperty] private CnnMaterialDto? selectedKimPreviewMaterial;

    [ObservableProperty] private BitmapImage? kimPreviewImage;

    [ObservableProperty] private bool kimPreviewIsPdf;

    [ObservableProperty] private string kimPreviewHint = "Выберите КИМ в списке для просмотра изображения.";

    [ObservableProperty] private double kimPanelZoom = 1.0;

    /// <summary>0 Профиль, 1 Каталог, 2 Экзамен, 3 Результаты, 4 Проверки.</summary>
    [ObservableProperty] private int selectedNavIndex;

    public ObservableCollection<CnnListItemDto> Cnns { get; } = [];
    public ObservableCollection<CnnMaterialDto> KimMaterials { get; } = [];
    public ObservableCollection<CnnMaterialDto> CriteriaMaterials { get; } = [];
    public ObservableCollection<CnnMaterialDto> BlanksMaterials { get; } = [];
    public ObservableCollection<CnnMaterialDto> OtherMaterials { get; } = [];

    public ExamSessionViewModel Exam => examSession;

    public StudentOrdersViewModel Orders => orders;

    public bool HasExam => examSession.CurrentTemplate is not null;

    partial void OnSelectedCnnChanged(CnnListItemDto? value) => _ = LoadCnnDetailsAsync();

    partial void OnSelectedKimPreviewMaterialChanged(CnnMaterialDto? value) => _ = LoadKimPreviewAsync(value);

    partial void OnIsAuthenticatedChanged(bool value)
    {
        if (value)
            _ = RefreshCnnsAsync();
        else
            SelectedNavIndex = 0;
    }

    [RelayCommand]
    private async Task RestoreSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(tokenStore.RefreshToken)
            && string.IsNullOrWhiteSpace(tokenStore.AccessToken))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var ok = await apiClient.TryRestoreSessionAsync(default);
            if (!ok)
            {
                StatusText = "Откройте «Профиль» для входа или регистрации.";
                return;
            }

            IsAuthenticated = true;
            UserDisplay = tokenStore.AccountEmail ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(UserDisplay))
                Email = UserDisplay;
            StatusText = "Сессия восстановлена.";
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось восстановить сессию: {ex.Message}";
            tokenStore.Clear();
            IsAuthenticated = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoginAsync(string? password)
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(password))
        {
            StatusText = "Укажите email и пароль.";
            return;
        }

        await LoginCoreAsync(Email.Trim(), password, "Вход выполнен.");
    }

    private async Task LoginCoreAsync(string email, string password, string successMessage)
    {
        IsBusy = true;
        try
        {
            await apiClient.LoginAsync(new LoginDto { Email = email, Password = password }, default);
            IsAuthenticated = true;
            UserDisplay = email;
            Email = email;
            StatusText = successMessage;
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка входа: {ex.Message}";
            IsAuthenticated = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Logout()
    {
        orders.Reset();
        tokenStore.Clear();
        SelectedNavIndex = 0;
        IsAuthenticated = false;
        UserDisplay = string.Empty;
        RegisterAwaitingCode = false;
        ResetAwaitingCode = false;
        RegisterCode = string.Empty;
        RegisterName = string.Empty;
        ResetCode = string.Empty;
        Cnns.Clear();
        ClearMaterialLists();
        SelectedCnn = null;
        CurrentDetails = null;
        examSession.ClearSession();
        SelectedKimPreviewMaterial = null;
        KimPreviewImage = null;
        KimPreviewIsPdf = false;
        KimPanelZoom = 1.0;
        OnPropertyChanged(nameof(HasExam));
        StatusText = "Вы вышли.";
    }

    [RelayCommand]
    private async Task RegisterSendCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(RegisterEmail))
        {
            StatusText = "Укажите email для регистрации.";
            return;
        }

        IsBusy = true;
        try
        {
            await apiClient.RegisterRequestCodeAsync(
                new RegisterRequestCodeDto { Email = RegisterEmail.Trim() }, default);
            RegisterAwaitingCode = true;
            StatusText = "Если почта доступна, на неё отправлен код. Введите код, имя и пароль ниже.";
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось отправить код: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RegisterConfirmAsync(string? password)
    {
        if (string.IsNullOrWhiteSpace(RegisterEmail)
            || string.IsNullOrWhiteSpace(RegisterCode)
            || string.IsNullOrWhiteSpace(RegisterName)
            || string.IsNullOrWhiteSpace(password))
        {
            StatusText = "Заполните email, код из письма, имя и пароль.";
            return;
        }

        IsBusy = true;
        try
        {
            var email = RegisterEmail.Trim();
            await apiClient.RegisterConfirmAsync(
                new RegisterConfirmDto
                {
                    Email = email,
                    Code = RegisterCode.Trim(),
                    Name = RegisterName.Trim(),
                    Password = password
                },
                default);

            try
            {
                await apiClient.LoginAsync(new LoginDto { Email = email, Password = password }, default);
                IsAuthenticated = true;
                UserDisplay = email;
                Email = email;
                RegisterAwaitingCode = false;
                RegisterCode = string.Empty;
                RegisterName = string.Empty;
                StatusText = "Регистрация завершена, вы вошли.";
            }
            catch (Exception loginEx)
            {
                StatusText =
                    $"Аккаунт создан, но вход не выполнен: {loginEx.Message}. Войдите вручную после подтверждения почты.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Подтверждение регистрации не удалось: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PasswordResetSendCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(ResetEmail))
        {
            StatusText = "Укажите email для сброса пароля.";
            return;
        }

        IsBusy = true;
        try
        {
            await apiClient.PasswordResetRequestAsync(
                new PasswordResetRequestCodeDto { Email = ResetEmail.Trim() }, default);
            ResetAwaitingCode = true;
            StatusText = "Если почта найдена, на неё отправлен код. Введите код и новый пароль.";
        }
        catch (Exception ex)
        {
            StatusText = $"Запрос кода не выполнен: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PasswordResetConfirmAsync(string? newPassword)
    {
        if (string.IsNullOrWhiteSpace(ResetEmail)
            || string.IsNullOrWhiteSpace(ResetCode)
            || string.IsNullOrWhiteSpace(newPassword))
        {
            StatusText = "Укажите email, код и новый пароль.";
            return;
        }

        IsBusy = true;
        try
        {
            await apiClient.PasswordResetConfirmAsync(
                new PasswordResetConfirmDto
                {
                    Email = ResetEmail.Trim(),
                    Code = ResetCode.Trim(),
                    NewPassword = newPassword
                },
                default);
            ResetAwaitingCode = false;
            ResetCode = string.Empty;
            Email = ResetEmail.Trim();
            StatusText = "Пароль обновлён. Войдите с новым паролём.";
        }
        catch (Exception ex)
        {
            StatusText = $"Сброс пароля не выполнен: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshCnnsAsync()
    {
        if (!IsAuthenticated) return;
        IsBusy = true;
        try
        {
            var list = await apiClient.GetCnnsAsync(default);
            Cnns.Clear();
            foreach (var c in list.OrderBy(x => x.Subject).ThenBy(x => x.Option))
                Cnns.Add(c);
            StatusText = $"Загружено вариантов: {Cnns.Count}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка каталога: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCnnDetailsAsync()
    {
        ClearMaterialLists();
        CurrentDetails = null;
        if (SelectedCnn is null)
            return;

        IsBusy = true;
        try
        {
            var d = await apiClient.GetCnnDetailsAsync(SelectedCnn.Id, default);
            CurrentDetails = d;
            foreach (var m in d.Materials.OrderBy(x => x.SortOrder).ThenBy(x => x.Id))
            {
                switch (m.Kind)
                {
                    case MaterialKind.Kim: KimMaterials.Add(m); break;
                    case MaterialKind.Criteria: CriteriaMaterials.Add(m); break;
                    case MaterialKind.Blanks: BlanksMaterials.Add(m); break;
                    default: OtherMaterials.Add(m); break;
                }
            }

            StatusText = $"Материалы: КИМ {KimMaterials.Count}, бланки {BlanksMaterials.Count}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка загрузки варианта: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearMaterialLists()
    {
        KimMaterials.Clear();
        CriteriaMaterials.Clear();
        BlanksMaterials.Clear();
        OtherMaterials.Clear();
        SelectedKimPreviewMaterial = null;
        KimPreviewImage = null;
        KimPreviewIsPdf = false;
        KimPreviewHint = "Выберите КИМ в списке для просмотра изображения.";
    }

    private async Task LoadKimPreviewAsync(CnnMaterialDto? material)
    {
        KimPreviewImage = null;
        KimPreviewIsPdf = false;
        if (material is null || string.IsNullOrWhiteSpace(material.Url))
        {
            KimPreviewHint = "Выберите КИМ в списке для просмотра изображения.";
            return;
        }

        var url = material.Url.Trim();
        Uri absolute;
        try
        {
            absolute = apiClient.ResolveToAbsoluteUri(url);
        }
        catch (Exception ex)
        {
            KimPreviewHint = $"Некорректный URL: {ex.Message}";
            return;
        }

        var path = absolute.AbsolutePath.ToLowerInvariant();

        if (path.EndsWith(".pdf", StringComparison.Ordinal))
        {
            KimPreviewIsPdf = true;
            KimPreviewHint = "PDF открывается кнопкой «Во внешней программе».";
            return;
        }

        IsBusy = true;
        try
        {
            var bytes = await apiClient.DownloadBytesAsync(url, default);
            KimPreviewImage = BitmapFromBytes(bytes);
            KimPreviewHint = string.Empty;
        }
        catch (Exception ex)
        {
            KimPreviewHint = $"Не удалось показать превью: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static BitmapImage BitmapFromBytes(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    [RelayCommand]
    private async Task OpenKimAsync(CnnMaterialDto? material)
    {
        if (material is null || string.IsNullOrWhiteSpace(material.Url)) return;
        IsBusy = true;
        try
        {
            var url = material.Url.Trim();
            var ext = Path.GetExtension(apiClient.ResolveToAbsoluteUri(url).AbsolutePath);
            var bytes = await apiClient.DownloadBytesAsync(url, default);
            if (string.IsNullOrEmpty(ext) || ext.Length > 5)
                ext = ".bin";
            var path = Path.Combine(Path.GetTempPath(), $"kim_{material.Id}{ext}");
            await File.WriteAllBytesAsync(path, bytes);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось открыть файл: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StartExamAsync()
    {
        if (SelectedCnn is null)
        {
            StatusText = "Выберите вариант в списке.";
            return;
        }

        IsBusy = true;
        try
        {
            var jsonUrl = await blankSync.GetTemplateJsonMaterialUrlAsync(SelectedCnn.Id, default);
            var template = await blankSync.PullAsync(SelectedCnn.Id, default);
            if (template is null)
            {
                StatusText =
                    $"Нет материала «{BlankTemplateSyncService.RemoteMaterialTitle}» (вид {nameof(MaterialKind.Blanks)}) для этого варианта.";
                examSession.ClearSession();
                OnPropertyChanged(nameof(HasExam));
                return;
            }

            examSession.SetTemplate(template, jsonUrl);
            OnPropertyChanged(nameof(HasExam));
            var t = examSession.CurrentTemplate;
            StatusText =
                $"Экзамен: {t?.Subject}, вариант {t?.Option}. Страниц в сессии: {t?.Pages.Count ?? 0} (порядок: регистрация → №1 → №2).";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка загрузки шаблона: {ex.Message}";
            examSession.ClearSession();
            OnPropertyChanged(nameof(HasExam));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UploadSubmissionForReviewAsync()
    {
        if (!IsAuthenticated)
        {
            StatusText = "Войдите в профиль.";
            return;
        }

        if (!HasExam)
        {
            StatusText = "Сначала начните экзамен и заполните бланки.";
            return;
        }

        IsBusy = true;
        try
        {
            var url = await examSession.UploadSubmissionPackageAsync(default);
            if (string.IsNullOrEmpty(url))
            {
                StatusText = "Сервер не вернул URL загруженного файла.";
                return;
            }

            orders.NewOrderAnswerUrl = url;
            SelectedNavIndex = 4;
            StatusText =
                "Пакет ответа загружен на сервер. На вкладке «Проверки» подставлена ссылка — создайте заказ или обновите существующий.";
        }
        catch (Exception ex)
        {
            StatusText = $"Загрузка пакета не удалась: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveSubmissionLocalAsync()
    {
        if (!HasExam)
        {
            StatusText = "Нет активной сессии экзамена.";
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = $"exam_submission_{examSession.CurrentTemplate?.CnnId ?? 0}.json"
        };

        if (dlg.ShowDialog() != true)
            return;

        IsBusy = true;
        try
        {
            await File.WriteAllTextAsync(dlg.FileName, examSession.GetSubmissionJson());
            StatusText = $"Пакет сохранён: {dlg.FileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось сохранить файл: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
