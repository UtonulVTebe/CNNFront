using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpertAdminTrainerApp.Domain;
using ExpertAdminTrainerApp.Presentation.Controls;
using ExpertAdminTrainerApp.Services;
using Microsoft.Win32;

namespace ExpertAdminTrainerApp.Presentation.ViewModels;

/// <summary>
/// Admin-only ViewModel for the blank constructor tab.
/// Extends BlankViewerViewModel with zone CRUD, stamp settings, and template management.
/// </summary>
public partial class BlankConstructorViewModel : BlankViewerViewModel
{
    private readonly IApiClient _apiClient;

    // ===== CNN Selection =====
    [ObservableProperty] private CnnListItemDto? selectedCnn;

    // ===== Editor Mode =====
    [ObservableProperty] private ZoneEditorMode editorMode = ZoneEditorMode.Select;
    [ObservableProperty] private ZoneFieldType stampFieldType = ZoneFieldType.ShortAnswer;
    [ObservableProperty] private string stampFieldName = "Answer";
    [ObservableProperty] private int stampTaskNumber = 1;
    [ObservableProperty] private float cellWidth = 2.5f;
    [ObservableProperty] private float cellHeight = 3.0f;
    [ObservableProperty] private int rowCellCount = 1;
    [ObservableProperty] private float cellGap = 0.2f;

    // ===== Collections =====
    public ObservableCollection<CnnListItemDto> Cnns { get; } = [];
    public IReadOnlyList<string> BlankTypeOptions { get; } = Enum.GetNames<BlankType>();
    public IReadOnlyList<string> FieldTypeOptions { get; } = Enum.GetNames<ZoneFieldType>();
    public IReadOnlyList<string> EditorModeOptions { get; } = ["Выбрать", "Ячейка", "Ряд ячеек"];

    // ===== Add Page =====
    [ObservableProperty] private BlankType newPageBlankType = BlankType.Registration;

    public BlankConstructorViewModel(BlankTemplateService templateService, IApiClient apiClient)
        : base(templateService)
    {
        _apiClient = apiClient;
    }

    public bool HasSelectedZone => SelectedZone is not null;

    partial void OnSelectedCnnChanged(CnnListItemDto? value)
    {
        if (value is not null)
            _ = LoadTemplate(value.Id);
        else
            CurrentTemplate = null;
    }

    public void OnZoneSelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelectedZone));
        if (SelectedZone is not null)
        {
            StampFieldName = SelectedZone.FieldName;
            StampFieldType = SelectedZone.FieldType;
            StampTaskNumber = SelectedZone.TaskNumber;
        }
    }

    // ===== Load CNN List =====

    [RelayCommand]
    private async Task LoadCnnList()
    {
        try
        {
            Cnns.Clear();
            var list = await _apiClient.GetCnnsAsync();
            foreach (var item in list) Cnns.Add(item);
            ViewerStatus = $"Загружено вариантов: {Cnns.Count}";
        }
        catch (Exception ex)
        {
            ViewerStatus = $"Ошибка загрузки каталога: {ex.Message}";
        }
    }

    // ===== Template Override =====

    protected override async Task LoadTemplate(int cnnId)
    {
        await base.LoadTemplate(cnnId);
        if (CurrentTemplate is null && SelectedCnn is not null)
        {
            CurrentTemplate = new BlankTemplateDefinition
            {
                CnnId = SelectedCnn.Id,
                Subject = SelectedCnn.Subject,
                Option = SelectedCnn.Option
            };
            ViewerStatus = "Новый шаблон создан. Добавьте страницы бланков.";
        }
    }

    // ===== Page Management =====

    [RelayCommand]
    private void AddPage()
    {
        if (CurrentTemplate is null) return;

        var existingCount = CurrentTemplate.Pages.Count(p => p.BlankType == NewPageBlankType);
        var page = new BlankPageDefinition
        {
            BlankType = NewPageBlankType,
            PageNumber = existingCount + 1
        };

        CurrentTemplate.Pages.Add(page);
        Pages.Add(page);
        SelectedPage = page;
        ViewerStatus = $"Добавлена страница: {GetBlankTypeLabel(NewPageBlankType)} (стр. {page.PageNumber})";
    }

    [RelayCommand]
    private void RemovePage()
    {
        if (SelectedPage is null || CurrentTemplate is null) return;

        var page = SelectedPage;
        CurrentTemplate.Pages.Remove(page);
        Pages.Remove(page);
        SelectedPage = Pages.Count > 0 ? Pages[0] : null;
        ViewerStatus = "Страница удалена.";
    }

    [RelayCommand]
    private void LoadPageImage()
    {
        if (SelectedPage is null) return;

        var dlg = new OpenFileDialog
        {
            Title = "Выберите изображение бланка",
            Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp|Все файлы|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        SelectedPage.ImagePath = dlg.FileName;
        base.LoadPageImage(dlg.FileName);
        ViewerStatus = $"Изображение загружено: {System.IO.Path.GetFileName(dlg.FileName)}";
    }

    // ===== Editor Mode Shortcuts =====

    [RelayCommand]
    private void SetModeSelect() => EditorMode = ZoneEditorMode.Select;

    [RelayCommand]
    private void SetModeStampSingle() => EditorMode = ZoneEditorMode.StampSingle;

    [RelayCommand]
    private void SetModeStampRow() => EditorMode = ZoneEditorMode.StampRow;

    // ===== Zone CRUD =====

    public void OnZoneAdded()
    {
        SyncZonesToPage();
        RefreshCurrentZones();
    }

    public void OnZoneSelected(ZoneDefinition? zone)
    {
        SelectedZone = zone;
        OnZoneSelectionChanged();
    }

    [RelayCommand]
    private void DeleteSelectedZone()
    {
        if (SelectedZone is null) return;

        CurrentZones.Remove(SelectedZone);
        SelectedPage?.Zones.Remove(SelectedZone);
        SelectedZone = null;
        ViewerStatus = "Зона удалена.";
    }

    [RelayCommand]
    private void UpdateSelectedZone()
    {
        if (SelectedZone is null) return;

        SelectedZone.FieldName = StampFieldName;
        SelectedZone.FieldType = StampFieldType;
        SelectedZone.TaskNumber = StampTaskNumber;

        SyncZonesToPage();
        RefreshCurrentZones();
        ViewerStatus = "Зона обновлена.";
    }

    // ===== Save / Load / Export / Import =====

    [RelayCommand]
    private async Task SaveTemplate()
    {
        if (CurrentTemplate is null) return;
        try
        {
            SyncZonesToPage();
            await TemplateService.SaveTemplateAsync(CurrentTemplate);
            ViewerStatus = $"Шаблон сохранен (CNN #{CurrentTemplate.CnnId}).";
        }
        catch (Exception ex)
        {
            ViewerStatus = $"Ошибка сохранения: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportTemplate()
    {
        if (CurrentTemplate is null) return;

        var dlg = new SaveFileDialog
        {
            Title = "Экспорт шаблона",
            Filter = "JSON|*.json",
            FileName = $"template_{CurrentTemplate.CnnId}.json"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            SyncZonesToPage();
            await TemplateService.SaveTemplateAsync(CurrentTemplate);
            await TemplateService.ExportTemplateAsync(CurrentTemplate.CnnId, dlg.FileName);
            ViewerStatus = $"Шаблон экспортирован: {dlg.FileName}";
        }
        catch (Exception ex)
        {
            ViewerStatus = $"Ошибка экспорта: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportTemplate()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Импорт шаблона",
            Filter = "JSON|*.json"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var template = await TemplateService.ImportTemplateAsync(dlg.FileName);
            if (template is not null)
            {
                CurrentTemplate = template;
                ViewerStatus = $"Шаблон импортирован: {template.Subject} (вариант {template.Option})";
            }
        }
        catch (Exception ex)
        {
            ViewerStatus = $"Ошибка импорта: {ex.Message}";
        }
    }

    // ===== Helpers =====

    private void RefreshCurrentZones()
    {
        if (SelectedPage is null) return;
        CurrentZones.Clear();
        foreach (var z in SelectedPage.Zones)
            CurrentZones.Add(z);
    }

    public static string GetBlankTypeLabel(BlankType type) => type switch
    {
        BlankType.Registration => "Бланк регистрации",
        BlankType.AnswerSheet1 => "Бланк ответов №1",
        BlankType.AnswerSheet2 => "Бланк ответов №2",
        _ => type.ToString()
    };

    public static string GetPageDisplayName(BlankPageDefinition page) =>
        $"{GetBlankTypeLabel(page.BlankType)} (стр. {page.PageNumber})";
}
