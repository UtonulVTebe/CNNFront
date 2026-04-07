using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpertAdminTrainerApp.Domain;
using ExpertAdminTrainerApp.Services;

namespace ExpertAdminTrainerApp.Presentation.ViewModels;

/// <summary>
/// Base ViewModel for displaying blank templates.
/// Reusable on the student client — handles template loading, page navigation, zone display.
/// </summary>
public partial class BlankViewerViewModel(BlankTemplateService templateService) : ObservableObject
{
    [ObservableProperty] private BlankTemplateDefinition? currentTemplate;
    [ObservableProperty] private BlankPageDefinition? selectedPage;
    [ObservableProperty] private BitmapImage? pageImageSource;
    [ObservableProperty] private ZoneDefinition? selectedZone;
    [ObservableProperty] private string viewerStatus = string.Empty;

    public ObservableCollection<BlankPageDefinition> Pages { get; } = [];
    public ObservableCollection<ZoneDefinition> CurrentZones { get; } = [];

    protected BlankTemplateService TemplateService => templateService;

    public bool HasTemplate => CurrentTemplate is not null;
    public bool HasSelectedPage => SelectedPage is not null;

    partial void OnCurrentTemplateChanged(BlankTemplateDefinition? value)
    {
        OnPropertyChanged(nameof(HasTemplate));
        Pages.Clear();
        if (value is not null)
        {
            foreach (var page in value.Pages)
                Pages.Add(page);
            SelectedPage = Pages.Count > 0 ? Pages[0] : null;
        }
        else
        {
            SelectedPage = null;
        }
    }

    partial void OnSelectedPageChanged(BlankPageDefinition? value)
    {
        OnPropertyChanged(nameof(HasSelectedPage));
        CurrentZones.Clear();
        SelectedZone = null;

        if (value is not null)
        {
            foreach (var zone in value.Zones)
                CurrentZones.Add(zone);
            LoadPageImage(value.ImagePath);
        }
        else
        {
            PageImageSource = null;
        }
    }

    [RelayCommand]
    protected virtual async Task LoadTemplate(int cnnId)
    {
        try
        {
            CurrentTemplate = await templateService.LoadTemplateAsync(cnnId);
            ViewerStatus = CurrentTemplate is not null
                ? $"Шаблон загружен: {CurrentTemplate.Subject} (вариант {CurrentTemplate.Option})"
                : "Шаблон не найден.";
        }
        catch (Exception ex)
        {
            ViewerStatus = $"Ошибка загрузки: {ex.Message}";
        }
    }

    protected void LoadPageImage(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            PageImageSource = null;
            return;
        }

        try
        {
            if (Uri.TryCreate(imagePath, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                PageImageSource = new BitmapImage(uri);
            }
            else if (File.Exists(imagePath))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(imagePath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                PageImageSource = bmp;
            }
            else
            {
                PageImageSource = null;
                ViewerStatus = $"Изображение не найдено: {imagePath}";
            }
        }
        catch (Exception ex)
        {
            PageImageSource = null;
            ViewerStatus = $"Ошибка загрузки изображения: {ex.Message}";
        }
    }

    protected void SyncZonesToPage()
    {
        if (SelectedPage is null) return;
        SelectedPage.Zones = [.. CurrentZones];
    }
}
