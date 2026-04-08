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

    [ObservableProperty] private int currentPageIndex;

    [ObservableProperty] private bool hasGraded;

    public ObservableCollection<TaskGradeResult> GradingResults { get; } = [];

    public void SetTemplate(BlankTemplateDefinition template, string? templateJsonMaterialUrl)
    {
        _templateJsonMaterialUrl = templateJsonMaterialUrl;
        HasGraded = false;
        GradingResults.Clear();
        _answers.Clear();
        CurrentTemplate = template;
        if (template.Pages.Count > 0 && SelectedPage is not null)
            CurrentPageIndex = Pages.IndexOf(SelectedPage);
    }

    public void ClearSession()
    {
        CurrentTemplate = null;
        _answers.Clear();
        GradingResults.Clear();
        HasGraded = false;
        _templateJsonMaterialUrl = null;
        CurrentPageIndex = 0;
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

    protected override void OnAfterSelectedPageChanged(BlankPageDefinition? value)
    {
        if (value is not null && Pages.Count > 0)
            CurrentPageIndex = Pages.IndexOf(value);
        else
            CurrentPageIndex = 0;

        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
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
    private void FinishAndGrade()
    {
        var template = CurrentTemplate;
        if (template is null || template.AutoAnswers.Count == 0)
        {
            ViewerStatus = "Нет эталонных ответов (autoAnswers) в шаблоне.";
            HasGraded = true;
            return;
        }

        var built = BuildAnswersByTaskNumber();
        GradingResults.Clear();
        foreach (var r in ShortAnswerAutoGrader.Grade(template, built))
            GradingResults.Add(r);

        var ok = GradingResults.Count(x => x.IsCorrect);
        ViewerStatus = $"Автопроверка: {ok} из {GradingResults.Count} заданий с эталоном.";
        HasGraded = true;
    }
}
