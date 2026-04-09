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
    IAppNavigator appNavigator,
    BlankTemplateSyncService blankSync,
    ExamSessionViewModel examSession,
    StudentOrdersViewModel orders) : ObservableObject
{
    private string? _loginScreenHint;
    private int _cnnDetailsLoadSerial;

    [ObservableProperty] private string statusText = string.Empty;

    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private bool isAuthenticated;

    [ObservableProperty] private string userDisplay = string.Empty;

    [ObservableProperty] private CnnListItemDto? selectedCnn;

    [ObservableProperty] private CnnDetailsDto? currentDetails;

    [ObservableProperty] private CnnMaterialDto? selectedKimPreviewMaterial;

    [ObservableProperty] private BitmapImage? kimPreviewImage;

    [ObservableProperty] private bool kimPreviewIsPdf;

    [ObservableProperty] private string kimPreviewHint = "Выберите КИМ в списке для просмотра изображения.";

    [ObservableProperty] private double kimPanelZoom = 1.0;

    /// <summary>0 Каталог, 1 Экзамен, 2 Результаты, 3 Проверки (профиль — внизу бокового меню).</summary>
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

    public string? ConsumeLoginScreenHint()
    {
        var t = _loginScreenHint;
        _loginScreenHint = null;
        return t;
    }

    public void ApplyLoggedInState(string displayEmail, string statusMessage)
    {
        IsAuthenticated = true;
        UserDisplay = displayEmail;
        StatusText = statusMessage;
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(tokenStore.RefreshToken)
            && string.IsNullOrWhiteSpace(tokenStore.AccessToken))
            return false;

        IsBusy = true;
        try
        {
            var ok = await apiClient.TryRestoreSessionAsync(default);
            if (!ok)
            {
                _loginScreenHint = "Войдите снова.";
                return false;
            }

            IsAuthenticated = true;
            UserDisplay = tokenStore.AccountEmail ?? string.Empty;
            StatusText = "Сессия восстановлена.";
            return true;
        }
        catch (Exception ex)
        {
            _loginScreenHint = $"Не удалось восстановить сессию: {ex.Message}";
            tokenStore.Clear();
            IsAuthenticated = false;
            return false;
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
        StatusText = string.Empty;
        appNavigator.ReturnToLoginAfterLogout();
    }

    [RelayCommand]
    private async Task RefreshCnnsAsync()
    {
        if (!IsAuthenticated) return;
        var keepCnnId = SelectedCnn?.Id;
        try
        {
            var list = await apiClient.GetCnnsAsync(default);
            Cnns.Clear();
            foreach (var c in list.OrderBy(x => x.Subject).ThenBy(x => x.Option))
                Cnns.Add(c);
            SelectedCnn = keepCnnId is null ? null : Cnns.FirstOrDefault(x => x.Id == keepCnnId);
            StatusText = $"Загружено вариантов: {Cnns.Count}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка каталога: {ex.Message}";
        }
    }

    private async Task LoadCnnDetailsAsync()
    {
        ClearMaterialLists();
        CurrentDetails = null;
        if (SelectedCnn is null)
            return;

        var cnnId = SelectedCnn.Id;
        var serial = unchecked(++_cnnDetailsLoadSerial);
        try
        {
            var d = await apiClient.GetCnnDetailsAsync(cnnId, default);
            if (serial != _cnnDetailsLoadSerial || SelectedCnn?.Id != cnnId)
                return;

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
            if (serial == _cnnDetailsLoadSerial && SelectedCnn?.Id == cnnId)
                StatusText = $"Ошибка загрузки варианта: {ex.Message}";
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
            var cnnId = examSession.CurrentTemplate!.CnnId;
            await orders.PrepareAfterSubmissionUploadAsync(cnnId);
            SelectedNavIndex = 3;
            StatusText =
                "1) Пакет загружен на сервер. 2) Открыта вкладка «Проверки», форма «Новый заказ» — вариант подставлен. 3) Нажмите «Создать» (галочка «Сразу в очередь» — по желанию).";
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
