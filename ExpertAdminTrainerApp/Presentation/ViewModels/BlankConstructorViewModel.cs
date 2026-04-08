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
    private const int MaxUndoDepth = 80;

    private readonly IApiClient _apiClient;
    private readonly BlankTemplateSyncService _blankTemplateSync;
    private readonly List<List<ZoneDefinition>> _undo = [];
    private readonly List<List<ZoneDefinition>> _redo = [];

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

    /// <summary>Следующий клик по бланку вставит этот пресет (сбрасывается после вставки).</summary>
    [ObservableProperty] private ZonePresetTemplate? pendingPresetTemplate;

    [ObservableProperty] private string? stampFieldRole;
    [ObservableProperty] private ZoneInputMode stampInputMode = ZoneInputMode.Cell;

    // ===== Collections =====
    public ObservableCollection<ZoneDefinition> SelectedZones { get; } = [];
    public ObservableCollection<AutoAnswerEntry> AutoAnswersEdit { get; } = [];

    public ObservableCollection<CnnListItemDto> Cnns { get; } = [];
    public IReadOnlyList<string> BlankTypeOptions { get; } = Enum.GetNames<BlankType>();
    public IReadOnlyList<ZoneFieldType> FieldTypes { get; } = Enum.GetValues<ZoneFieldType>().ToArray();
    public IReadOnlyList<ZoneInputMode> InputModes { get; } = Enum.GetValues<ZoneInputMode>().ToArray();
    public IReadOnlyList<string> EditorModeOptions { get; } = ["Выбрать", "Ячейка", "Ряд ячеек", "Прямоугольник"];
    public IReadOnlyList<ZonePresetTemplate> ZonePresets { get; } = ZonePresetTemplate.Catalog;

    // ===== Add Page =====
    [ObservableProperty] private BlankType newPageBlankType = BlankType.Registration;

    public BlankConstructorViewModel(BlankTemplateService templateService, IApiClient apiClient, BlankTemplateSyncService blankTemplateSync)
        : base(templateService)
    {
        _apiClient = apiClient;
        _blankTemplateSync = blankTemplateSync;
    }

    public bool HasSelectedZone => SelectedZone is not null;
    public bool HasMultiSelection => SelectedZones.Count > 1;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    partial void OnSelectedCnnChanged(CnnListItemDto? value)
    {
        if (value is not null)
            _ = LoadTemplate(value.Id);
        else
            CurrentTemplate = null;
    }

    protected override void OnAfterSelectedPageChanged(BlankPageDefinition? value)
    {
        SelectedZones.Clear();
        _undo.Clear();
        _redo.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    /// <summary>Канва уже обновила <see cref="SelectedZones"/>; фиксируем в VM активную зону и панель свойств.</summary>
    public void ApplyCanvasSelection(ZoneDefinition? primary)
    {
        SelectedZone = primary;
        OnZoneSelectionChanged();
    }

    /// <summary>Выбор одной зоны из списка/таблицы сбрасывает мультивыбор (не вызывать при программной синхронизации списка).</summary>
    public void SelectZoneFromSidebar(ZoneDefinition z)
    {
        SelectedZones.Clear();
        SelectedZones.Add(z);
        SelectedZone = z;
        OnZoneSelectionChanged();
    }

    public void OnZoneSelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelectedZone));
        OnPropertyChanged(nameof(HasMultiSelection));
        if (SelectedZone is not null)
        {
            StampFieldName = SelectedZone.FieldName;
            StampFieldType = SelectedZone.FieldType;
            StampTaskNumber = SelectedZone.TaskNumber;
            StampFieldRole = SelectedZone.FieldRole;
            StampInputMode = SelectedZone.InputMode;
        }
    }

    /// <summary>Снимок зон текущей страницы до операции (вызывается из представления по событию канвы).</summary>
    public void PushUndoSnapshot()
    {
        if (SelectedPage is null) return;
        _redo.Clear();
        _undo.Add(ZoneDefinitionCopy.CloneList(SelectedPage.Zones));
        while (_undo.Count > MaxUndoDepth)
            _undo.RemoveAt(0);

        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    [RelayCommand]
    private void Undo()
    {
        if (SelectedPage is null || _undo.Count == 0) return;

        _redo.Add(ZoneDefinitionCopy.CloneList(SelectedPage.Zones));
        var prev = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        ApplyZonesSnapshot(prev);
        ViewerStatus = "Отмена.";
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    [RelayCommand]
    private void Redo()
    {
        if (SelectedPage is null || _redo.Count == 0) return;

        _undo.Add(ZoneDefinitionCopy.CloneList(SelectedPage.Zones));
        var next = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        ApplyZonesSnapshot(next);
        ViewerStatus = "Вернуть.";
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private void ApplyZonesSnapshot(List<ZoneDefinition> snapshot)
    {
        if (SelectedPage is null) return;
        SelectedPage.Zones = snapshot;
        SelectedZone = null;
        SelectedZones.Clear();
        RefreshCurrentZones();
    }

    public void ClearPendingPreset() => PendingPresetTemplate = null;

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

        SyncAutoAnswersFromTemplate();
    }

    private void SyncAutoAnswersFromTemplate()
    {
        AutoAnswersEdit.Clear();
        if (CurrentTemplate is null) return;
        foreach (var e in CurrentTemplate.AutoAnswers)
            AutoAnswersEdit.Add(new AutoAnswerEntry { TaskId = e.TaskId, Answer = e.Answer });
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

    [RelayCommand]
    private void SetModeDrawRectangle() => EditorMode = ZoneEditorMode.DrawRectangle;

    [RelayCommand]
    private void SelectPreset(ZonePresetTemplate? preset)
    {
        if (preset is null) return;
        PendingPresetTemplate = preset;
        ViewerStatus = $"Пресет «{preset.DisplayName}»: кликните по бланку для вставки.";
    }

    // ===== Zone CRUD =====

    public void OnZoneAdded()
    {
        // Только синхронизируем модель страницы; CurrentZones уже источник правды (та же коллекция, что на канве).
        // RefreshCurrentZones() здесь давал Clear()+перезаливку после каждого клика/перетаскивания — мигание бланка и сбой UI.
        SyncZonesToPage();
    }

    [RelayCommand]
    private void DeleteSelectedZone()
    {
        if (SelectedPage is null) return;

        var toRemove = SelectedZones.Count > 0
            ? SelectedZones.ToList()
            : SelectedZone is not null ? [SelectedZone] : [];

        if (toRemove.Count == 0) return;

        PushUndoSnapshot();
        foreach (var z in toRemove)
        {
            CurrentZones.Remove(z);
            SelectedPage.Zones.Remove(z);
        }

        SelectedZones.Clear();
        SelectedZone = null;
        ViewerStatus = toRemove.Count > 1 ? $"Удалено зон: {toRemove.Count}." : "Зона удалена.";
    }

    [RelayCommand]
    private void DeleteSelectedGroup()
    {
        if (SelectedZone is null || string.IsNullOrEmpty(SelectedZone.GroupId) || SelectedPage is null)
        {
            ViewerStatus = "У выбранной зоны нет группы.";
            return;
        }

        var gid = SelectedZone.GroupId;
        var toRemove = CurrentZones.Where(z => z.GroupId == gid).ToList();
        if (toRemove.Count == 0) return;

        PushUndoSnapshot();
        foreach (var z in toRemove)
        {
            CurrentZones.Remove(z);
            SelectedPage.Zones.Remove(z);
        }

        SelectedZones.Clear();
        SelectedZone = null;
        ViewerStatus = $"Удалена группа ({toRemove.Count} зон).";
    }

    [RelayCommand]
    private void ApplyGroupGap()
    {
        if (SelectedZone is null || string.IsNullOrEmpty(SelectedZone.GroupId) || SelectedPage is null)
        {
            ViewerStatus = "Выберите зону из группы с общим groupId.";
            return;
        }

        var group = CurrentZones.Where(z => z.GroupId == SelectedZone.GroupId).OrderBy(z => z.X).ToList();
        if (group.Count < 2)
        {
            ViewerStatus = "В группе меньше двух зон.";
            return;
        }

        PushUndoSnapshot();
        float x0 = group[0].X;
        float w = group[0].Width;
        for (var i = 0; i < group.Count; i++)
            group[i].X = Math.Clamp(x0 + i * (w + CellGap), 0, 100 - group[i].Width);

        SyncZonesToPage();
        RefreshCurrentZones();
        ViewerStatus = "Зазор группы применён.";
    }

    [RelayCommand]
    private void GroupSelectedZones()
    {
        if (SelectedZones.Count < 2)
        {
            ViewerStatus = "Выберите минимум две зоны (Ctrl+клик на бланке).";
            return;
        }

        PushUndoSnapshot();
        var gid = Guid.NewGuid().ToString("N");
        foreach (var z in SelectedZones)
            z.GroupId = gid;

        SyncZonesToPage();
        RefreshCurrentZones();
        ViewerStatus = "Зоны объединены в группу.";
    }

    [RelayCommand]
    private void UngroupSelected()
    {
        if (SelectedZones.Count == 0 && SelectedZone is null)
            return;

        PushUndoSnapshot();
        foreach (var z in SelectedZones.Count > 0 ? SelectedZones.ToList() : [SelectedZone!])
            z.GroupId = null;

        SyncZonesToPage();
        RefreshCurrentZones();
        ViewerStatus = "Группа снята.";
    }

    [RelayCommand]
    private void UpdateSelectedZone()
    {
        if (SelectedZone is null) return;

        if (SelectedZones.Count > 1)
        {
            PushUndoSnapshot();
            foreach (var z in SelectedZones)
            {
                z.FieldName = StampFieldName;
                z.FieldType = StampFieldType;
                z.TaskNumber = StampTaskNumber;
                z.FieldRole = string.IsNullOrWhiteSpace(StampFieldRole) ? null : StampFieldRole.Trim();
                z.InputMode = StampInputMode;
            }

            SyncZonesToPage();
            RefreshCurrentZones();
            ViewerStatus = $"Обновлено зон: {SelectedZones.Count}.";
            return;
        }

        SelectedZone.FieldName = StampFieldName;
        SelectedZone.FieldType = StampFieldType;
        SelectedZone.TaskNumber = StampTaskNumber;
        SelectedZone.FieldRole = string.IsNullOrWhiteSpace(StampFieldRole) ? null : StampFieldRole.Trim();
        SelectedZone.InputMode = StampInputMode;

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
            CurrentTemplate.AutoAnswers = [.. AutoAnswersEdit];
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
            CurrentTemplate.AutoAnswers = [.. AutoAnswersEdit];
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
                SyncAutoAnswersFromTemplate();
                ViewerStatus = $"Шаблон импортирован: {template.Subject} (вариант {template.Option})";
            }
        }
        catch (Exception ex)
        {
            ViewerStatus = $"Ошибка импорта: {ex.Message}";
        }
    }

    /// <summary>Загрузка JSON разметки на сервер (файл + материал «Бланки» с фиксированным заголовком).</summary>
    [RelayCommand]
    private async Task PushBlankTemplateToServer()
    {
        if (CurrentTemplate is null)
        {
            ViewerStatus = "Нет шаблона для отправки.";
            return;
        }

        if (SelectedCnn is not null && SelectedCnn.Id != CurrentTemplate.CnnId)
        {
            ViewerStatus = "Выбран другой вариант КИМ в списке — выберите тот же, что и в шаблоне, или сохраните шаблон под нужным CNN.";
            return;
        }

        try
        {
            SyncZonesToPage();
            CurrentTemplate.AutoAnswers = [.. AutoAnswersEdit];
            await _blankTemplateSync.PushAsync(CurrentTemplate);
            await TemplateService.SaveTemplateAsync(CurrentTemplate);
            ViewerStatus = $"Шаблон отправлен на сервер (CNN #{CurrentTemplate.CnnId}, материал «{BlankTemplateSyncService.RemoteMaterialTitle}»).";
        }
        catch (Exception ex)
        {
            ViewerStatus = $"Ошибка отправки на сервер: {ex.Message}";
        }
    }

    /// <summary>Скачивание JSON разметки с сервера по выбранному КИМ.</summary>
    [RelayCommand]
    private async Task PullBlankTemplateFromServer()
    {
        if (SelectedCnn is null)
        {
            ViewerStatus = "Выберите вариант КИМ в списке.";
            return;
        }

        try
        {
            var template = await _blankTemplateSync.PullAsync(SelectedCnn.Id);
            if (template is null)
            {
                ViewerStatus =
                    $"На сервере нет материала «{BlankTemplateSyncService.RemoteMaterialTitle}» (вид «Бланки») для CNN #{SelectedCnn.Id}. Сначала отправьте шаблон с этой машины или создайте материал вручную.";
                return;
            }

            CurrentTemplate = template;
            SyncAutoAnswersFromTemplate();
            await TemplateService.SaveTemplateAsync(template);
            ViewerStatus = $"Шаблон загружен с сервера: {template.Subject} (вариант {template.Option}).";
        }
        catch (Exception ex)
        {
            ViewerStatus = $"Ошибка загрузки с сервера: {ex.Message}";
        }
    }

    [RelayCommand]
    private void AddAutoAnswerRow()
    {
        AutoAnswersEdit.Add(new AutoAnswerEntry { TaskId = 1, Answer = string.Empty });
    }

    [RelayCommand]
    private void RemoveAutoAnswerRow(AutoAnswerEntry? row)
    {
        if (row is not null)
            AutoAnswersEdit.Remove(row);
    }

    // ===== Helpers =====

    private void RefreshCurrentZones()
    {
        if (SelectedPage is null) return;
        RaiseZonesListRefreshStarting();
        try
        {
            CurrentZones.Clear();
            foreach (var z in SelectedPage.Zones)
                CurrentZones.Add(z);
        }
        finally
        {
            RaiseZonesListRefreshCompleted();
        }
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
