using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainerStudApp.Domain;
using TrainerStudApp.Presentation.Controls;
using TrainerStudApp.Services;

namespace TrainerStudApp.Presentation.ViewModels;

public partial class ExamSessionViewModel(BlankTemplateService templateService, IApiClient apiClient)
    : BlankViewerViewModel(templateService), IZoneAnswerSink
{
    private readonly Dictionary<string, string> _answers = new(StringComparer.Ordinal);
    private string? _templateJsonMaterialUrl;
    private BlankPageDefinition? _answerSheet2Prototype;
    private int _baseAnswerSheet2Count;
    private bool _extraAnswerSheet2Used;

    /// <summary>Срабатывает после успешной или пустой автопроверки — UI может переключить вкладку «Результаты».</summary>
    public event EventHandler? GradingCompleted;

    [ObservableProperty] private int currentPageIndex;

    [ObservableProperty] private bool hasGraded;

    [ObservableProperty] private string currentStepLabel = string.Empty;

    [ObservableProperty] private string resultSummaryText = string.Empty;

    /// <summary>Масштаб панели бланка (1.0 = 100%).</summary>
    [ObservableProperty] private double blankPanelZoom = 1.75;

    public ObservableCollection<TaskGradeResult> GradingResults { get; } = [];

    public bool CanAddAnswerSheet2 =>
        CurrentTemplate is not null
        && _answerSheet2Prototype is not null
        && !_extraAnswerSheet2Used;

    public void SetTemplate(BlankTemplateDefinition template, string? templateJsonMaterialUrl)
    {
        _templateJsonMaterialUrl = templateJsonMaterialUrl;
        HasGraded = false;
        ResultSummaryText = string.Empty;
        GradingResults.Clear();
        _answers.Clear();
        _extraAnswerSheet2Used = false;

        var working = ExamTemplatePageCloner.CreateExamWorkingCopy(
            template,
            out _answerSheet2Prototype,
            out _baseAnswerSheet2Count);

        CurrentTemplate = working;
        if (working.Pages.Count > 0 && SelectedPage is not null)
            CurrentPageIndex = Pages.IndexOf(SelectedPage);

        UpdateStepLabel();
        OnPropertyChanged(nameof(CanAddAnswerSheet2));
    }

    public void ClearSession()
    {
        _answerSheet2Prototype = null;
        _baseAnswerSheet2Count = 0;
        _extraAnswerSheet2Used = false;
        CurrentTemplate = null;
        _answers.Clear();
        GradingResults.Clear();
        HasGraded = false;
        ResultSummaryText = string.Empty;
        CurrentStepLabel = string.Empty;
        _templateJsonMaterialUrl = null;
        CurrentPageIndex = 0;
        BlankPanelZoom = 1.75;
        OnPropertyChanged(nameof(CanAddAnswerSheet2));
    }

    public string? GetAnswer(string key) =>
        _answers.TryGetValue(key, out var v) ? v : null;

    public void SetAnswer(string key, string? value)
    {
        if (string.IsNullOrEmpty(value))
            _answers.Remove(key);
        else
            _answers[key] = value;
    }

    public IReadOnlyDictionary<string, string> ExportAnswersDictionary() =>
        new Dictionary<string, string>(_answers, StringComparer.Ordinal);

    public string GetSubmissionJson() =>
        ExamSubmissionDocument.Serialize(ExamSubmissionBuilder.Build(this));

    public async Task<string?> UploadSubmissionPackageAsync(CancellationToken ct = default)
    {
        var doc = ExamSubmissionBuilder.Build(this);
        var json = ExamSubmissionDocument.Serialize(doc);
        var path = Path.Combine(Path.GetTempPath(), $"submission_{doc.CnnId}_{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, json, ct);
        try
        {
            var upload = await apiClient.UploadFileAsync(path, "answer", ct);
            var url = upload.Url?.Trim();
            return string.IsNullOrEmpty(url) ? null : url;
        }
        finally
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }

    protected override void OnAfterSelectedPageChanged(BlankPageDefinition? value)
    {
        if (value is not null && Pages.Count > 0)
            CurrentPageIndex = Pages.IndexOf(value);
        else
            CurrentPageIndex = 0;

        UpdateStepLabel();
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanAddAnswerSheet2));
    }

    private void UpdateStepLabel()
    {
        if (SelectedPage is null || Pages.Count == 0)
        {
            CurrentStepLabel = string.Empty;
            return;
        }

        var reg = Pages.Count(p => p.BlankType == BlankType.Registration);
        var a1 = Pages.Count(p => p.BlankType == BlankType.AnswerSheet1);
        var as2List = Pages.Where(p => p.BlankType == BlankType.AnswerSheet2).ToList();
        var sp = SelectedPage;

        if (sp.BlankType == BlankType.Registration)
        {
            var i = Pages.Where(p => p.BlankType == BlankType.Registration).ToList().IndexOf(sp) + 1;
            CurrentStepLabel = $"Регистрация — страница {i} из {reg}";
        }
        else if (sp.BlankType == BlankType.AnswerSheet1)
        {
            var i = Pages.Where(p => p.BlankType == BlankType.AnswerSheet1).ToList().IndexOf(sp) + 1;
            CurrentStepLabel = $"Бланк ответов №1 — страница {i} из {a1}";
        }
        else if (sp.BlankType == BlankType.AnswerSheet2)
        {
            var i = as2List.IndexOf(sp) + 1;
            var extra = i > _baseAnswerSheet2Count ? " (доп.)" : string.Empty;
            CurrentStepLabel = $"Бланк ответов №2 — лист {i} из {as2List.Count}{extra}";
        }
        else
            CurrentStepLabel = $"Страница {Pages.IndexOf(sp) + 1} из {Pages.Count}";
    }

    protected override void OnPageImageRequired(BlankPageDefinition page)
    {
        _ = LoadPageImageAuthenticatedAsync(page);
    }

    private async Task LoadPageImageAuthenticatedAsync(BlankPageDefinition page)
    {
        try
        {
            var resolved = TemplateMediaResolver.Resolve(page.ImagePath, _templateJsonMaterialUrl);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                PageImageSource = null;
                ViewerStatus = "Нет пути к изображению страницы.";
                return;
            }

            if (Uri.TryCreate(resolved, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var bytes = await apiClient.DownloadBytesAsync(resolved).ConfigureAwait(true);
                PageImageSource = BitmapFromBytes(bytes);
                ViewerStatus = string.Empty;
                return;
            }

            if (File.Exists(resolved))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(resolved, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                PageImageSource = bmp;
                ViewerStatus = string.Empty;
            }
            else
            {
                PageImageSource = null;
                ViewerStatus = $"Изображение не найдено: {resolved}";
            }
        }
        catch (Exception ex)
        {
            PageImageSource = null;
            ViewerStatus = $"Ошибка загрузки изображения: {ex.Message}";
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

    public Dictionary<int, string> BuildAnswersByTaskNumber()
    {
        var template = CurrentTemplate;
        if (template is null)
            return new Dictionary<int, string>();

        var byTask = new Dictionary<int, List<(float y, float x, string text)>>();

        for (var pi = 0; pi < template.Pages.Count; pi++)
        {
            var page = template.Pages[pi];
            foreach (var zone in page.Zones)
            {
                if (zone.FieldType is not (ZoneFieldType.ShortAnswer or ZoneFieldType.Correction))
                    continue;

                var sb = new System.Text.StringBuilder();
                foreach (var key in ExamAnswerKeyHelper.EnumerateKeysForZone(zone, pi))
                    sb.Append(GetAnswer(key) ?? string.Empty);

                var text = sb.ToString();
                if (text.Length == 0)
                    continue;

                if (!byTask.TryGetValue(zone.TaskNumber, out var list))
                {
                    list = [];
                    byTask[zone.TaskNumber] = list;
                }

                list.Add((zone.Y, zone.X, text));
            }
        }

        var result = new Dictionary<int, string>();
        foreach (var (taskId, parts) in byTask)
        {
            var ordered = parts.OrderBy(p => p.y).ThenBy(p => p.x).Select(p => p.text);
            result[taskId] = string.Concat(ordered);
        }

        return result;
    }

    public bool CanGoPrevious => SelectedPage is not null && Pages.IndexOf(SelectedPage) > 0;

    public bool CanGoNext =>
        SelectedPage is not null && Pages.IndexOf(SelectedPage) < Pages.Count - 1;

    [RelayCommand]
    private void GoPreviousPage()
    {
        if (SelectedPage is null) return;
        var i = Pages.IndexOf(SelectedPage);
        if (i > 0)
            SelectedPage = Pages[i - 1];
    }

    [RelayCommand]
    private void GoNextPage()
    {
        if (SelectedPage is null) return;
        var i = Pages.IndexOf(SelectedPage);
        if (i < Pages.Count - 1)
            SelectedPage = Pages[i + 1];
    }

    [RelayCommand]
    private void AddAnswerSheet2()
    {
        var proto = _answerSheet2Prototype;
        var tmpl = CurrentTemplate;
        if (proto is null || tmpl is null)
            return;

        var clone = ExamTemplatePageCloner.ClonePageWithNewZoneIds(proto);
        var maxNum = tmpl.Pages.Count > 0
            ? tmpl.Pages.Max(p => p.PageNumber)
            : 0;
        clone.PageNumber = maxNum + 1;

        tmpl.Pages.Add(clone);
        Pages.Add(clone);
        SelectedPage = clone;
        _extraAnswerSheet2Used = true;
        OnPropertyChanged(nameof(CanAddAnswerSheet2));
    }

    [RelayCommand]
    private void FinishAndGrade()
    {
        var template = CurrentTemplate;
        if (template is null || template.AutoAnswers.Count == 0)
        {
            ViewerStatus = "Нет эталонных ответов (autoAnswers) в шаблоне.";
            ResultSummaryText = ViewerStatus;
            HasGraded = true;
            GradingCompleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        var built = BuildAnswersByTaskNumber();
        GradingResults.Clear();
        foreach (var r in ShortAnswerAutoGrader.Grade(template, built))
            GradingResults.Add(r);

        var ok = GradingResults.Count(x => x.IsCorrect);
        ViewerStatus = $"Автопроверка: {ok} из {GradingResults.Count} заданий с эталоном.";
        ResultSummaryText = $"{ViewerStatus} Верно: {ok}, всего с эталоном: {GradingResults.Count}.";
        HasGraded = true;
        GradingCompleted?.Invoke(this, EventArgs.Empty);
    }
}
