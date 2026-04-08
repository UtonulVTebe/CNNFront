using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainerStudApp.Domain;
using TrainerStudApp.Services;

namespace TrainerStudApp.Presentation.ViewModels;

public partial class StudentMainViewModel(
    IApiClient apiClient,
    ITokenStore tokenStore,
    BlankTemplateSyncService blankSync,
    ExamSessionViewModel examSession) : ObservableObject
{
    [ObservableProperty] private string email = string.Empty;

    [ObservableProperty] private string statusText = "Введите email и пароль.";

    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private bool isAuthenticated;

    [ObservableProperty] private string userDisplay = string.Empty;

    [ObservableProperty] private CnnListItemDto? selectedCnn;

    [ObservableProperty] private CnnDetailsDto? currentDetails;

    public ObservableCollection<CnnListItemDto> Cnns { get; } = [];
    public ObservableCollection<CnnMaterialDto> KimMaterials { get; } = [];
    public ObservableCollection<CnnMaterialDto> CriteriaMaterials { get; } = [];
    public ObservableCollection<CnnMaterialDto> BlanksMaterials { get; } = [];
    public ObservableCollection<CnnMaterialDto> OtherMaterials { get; } = [];

    public ExamSessionViewModel Exam => examSession;

    public bool HasExam => examSession.CurrentTemplate is not null;

    partial void OnSelectedCnnChanged(CnnListItemDto? value) => _ = LoadCnnDetailsAsync();

    partial void OnIsAuthenticatedChanged(bool value)
    {
        if (value)
            _ = RefreshCnnsAsync();
    }

    [RelayCommand]
    private async Task LoginAsync(string? password)
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(password))
        {
            StatusText = "Укажите email и пароль.";
            return;
        }

        IsBusy = true;
        try
        {
            await apiClient.LoginAsync(new LoginDto { Email = Email.Trim(), Password = password }, default);
            IsAuthenticated = true;
            UserDisplay = Email.Trim();
            StatusText = "Вход выполнен.";
            await RefreshCnnsAsync();
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
        tokenStore.Clear();
        IsAuthenticated = false;
        UserDisplay = string.Empty;
        Cnns.Clear();
        ClearMaterialLists();
        SelectedCnn = null;
        CurrentDetails = null;
        examSession.ClearSession();
        OnPropertyChanged(nameof(HasExam));
        StatusText = "Вы вышли.";
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
    }

    [RelayCommand]
    private async Task OpenKimAsync(CnnMaterialDto? material)
    {
        if (material is null || string.IsNullOrWhiteSpace(material.Url)) return;
        IsBusy = true;
        try
        {
            var bytes = await apiClient.DownloadBytesAsync(material.Url.Trim(), default);
            var ext = Path.GetExtension(new Uri(material.Url).AbsolutePath);
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
            StatusText = $"Экзамен: {template.Subject}, вариант {template.Option}. Страниц: {template.Pages.Count}.";
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
}
