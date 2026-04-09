using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using ExpertAdminTrainerApp.Domain;

namespace ExpertAdminTrainerApp.Presentation.Controls;

/// <summary>Просмотр заполненного бланка из <see cref="ExamSubmissionDocument"/> (только чтение).</summary>
public class BlankSubmissionReviewCanvas : BlankDisplayCanvas
{
    private static readonly Brush SubtleFieldBackground = new SolidColorBrush(Color.FromArgb(15, 245, 245, 248));
    private static readonly Brush InkFieldBackground = new SolidColorBrush(Color.FromArgb(12, 245, 245, 248));

    static BlankSubmissionReviewCanvas()
    {
        if (SubtleFieldBackground is SolidColorBrush sb) sb.Freeze();
        if (InkFieldBackground is SolidColorBrush ib) ib.Freeze();
    }

    public static readonly DependencyProperty PageIndexProperty =
        DependencyProperty.Register(nameof(PageIndex), typeof(int), typeof(BlankSubmissionReviewCanvas),
            new PropertyMetadata(0, (d, _) =>
            {
                if (d is BlankSubmissionReviewCanvas c) c.ScheduleRebuild();
            }));

    public static readonly DependencyProperty AnswerSinkProperty =
        DependencyProperty.Register(nameof(AnswerSink), typeof(IZoneAnswerSink), typeof(BlankSubmissionReviewCanvas),
            new PropertyMetadata(null, (d, _) =>
            {
                if (d is BlankSubmissionReviewCanvas c) c.ScheduleRebuild();
            }));

    private readonly Canvas _inputLayer = new() { Background = Brushes.Transparent, IsHitTestVisible = false };
    private INotifyCollectionChanged? _zonesNotify;
    private bool _rebuildScheduled;

    public BlankSubmissionReviewCanvas()
    {
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        Panel.SetZIndex(_inputLayer, 1000);
        Children.Add(_inputLayer);
        Loaded += (_, _) => ScheduleRebuild();
    }

    public int PageIndex
    {
        get => (int)GetValue(PageIndexProperty);
        set => SetValue(PageIndexProperty, value);
    }

    public IZoneAnswerSink? AnswerSink
    {
        get => (IZoneAnswerSink?)GetValue(AnswerSinkProperty);
        set => SetValue(AnswerSinkProperty, value);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ZonesProperty)
        {
            if (_zonesNotify is not null)
            {
                _zonesNotify.CollectionChanged -= OnZonesCollectionChanged;
                _zonesNotify = null;
            }

            if (e.NewValue is INotifyCollectionChanged n)
            {
                _zonesNotify = n;
                n.CollectionChanged += OnZonesCollectionChanged;
            }

            ScheduleRebuild();
        }
    }

    private void OnZonesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleRebuild();

    private void ScheduleRebuild()
    {
        if (_rebuildScheduled) return;
        _rebuildScheduled = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            _rebuildScheduled = false;
            RebuildInputLayer();
        });
    }

    protected override void Redraw()
    {
        base.Redraw();
        ScheduleRebuild();
    }

    private void RebuildInputLayer()
    {
        _inputLayer.Children.Clear();
        var zones = Zones;
        if (zones is null || ImageRect.Width <= 0 || ImageRect.Height <= 0)
            return;

        var sink = AnswerSink;
        var pageIdx = PageIndex;

        foreach (var zone in zones)
        {
            if (!WantsInteractiveInput(zone))
                continue;

            var (px, py, pw, ph) = ZoneToPixels(zone);

            switch (zone.FieldType)
            {
                case ZoneFieldType.LongAnswer when zone.InputMode == ZoneInputMode.TextAndDrawing:
                    AddTextAndDrawingReadOnly(zone, pageIdx, px, py, pw, ph, sink);
                    break;
                case ZoneFieldType.Drawing when zone.InputMode == ZoneInputMode.Drawing:
                    AddInkReadOnly(zone, pageIdx, px, py, pw, ph, sink);
                    break;
                case ZoneFieldType.CellGrid when zone.InputMode != ZoneInputMode.Drawing:
                    AddCellGridReadOnly(zone, pageIdx, px, py, pw, ph, sink);
                    break;
                default:
                    if (zone.InputMode == ZoneInputMode.Text || zone.InputMode == ZoneInputMode.Drawing)
                    {
                        if (zone.InputMode == ZoneInputMode.Drawing)
                            AddInkReadOnly(zone, pageIdx, px, py, pw, ph, sink);
                        else
                            AddTextBoxReadOnly(zone, pageIdx, px, py, pw, ph, sink,
                                multiline: zone.FieldType is ZoneFieldType.LongAnswer or ZoneFieldType.FreeForm);
                    }
                    else
                        AddSingleOrRowCellsReadOnly(zone, pageIdx, px, py, pw, ph, sink);
                    break;
            }
        }
    }

    private static bool WantsInteractiveInput(ZoneDefinition z) =>
        z.FieldType is ZoneFieldType.Header
            or ZoneFieldType.ShortAnswer
            or ZoneFieldType.LongAnswer
            or ZoneFieldType.FreeForm
            or ZoneFieldType.Correction
            or ZoneFieldType.CellGrid
            or ZoneFieldType.Drawing;

    private static void ApplyReadOnlyTextMetrics(TextBox tb, double cellHeightPx, double cellWidthPx,
        VerticalAlignment contentV, HorizontalAlignment contentH, double? maxFontSize = null)
    {
        tb.MinHeight = 0;
        tb.Padding = new Thickness(0);
        tb.VerticalAlignment = VerticalAlignment.Top;
        tb.HorizontalAlignment = HorizontalAlignment.Left;
        tb.VerticalContentAlignment = contentV;
        tb.HorizontalContentAlignment = contentH;
        TextOptions.SetTextFormattingMode(tb, TextFormattingMode.Display);
        TextOptions.SetTextHintingMode(tb, TextHintingMode.Fixed);
        SpellCheck.SetIsEnabled(tb, false);
        tb.SnapsToDevicePixels = true;
        var h = Math.Max(4, cellHeightPx);
        var w = Math.Max(4, cellWidthPx);
        var fs = Math.Clamp(Math.Min(h * 0.72, w * 0.95), 8, Math.Max(8, h - 3));
        if (maxFontSize is { } cap)
            fs = Math.Min(fs, cap);
        tb.FontSize = fs;
    }

    private void AddSingleOrRowCellsReadOnly(ZoneDefinition zone, int pageIdx, double px, double py, double pw,
        double ph, IZoneAnswerSink? sink)
    {
        var key = AnswerKey(pageIdx, zone.Id);
        if (zone.InputMode == ZoneInputMode.Cell && zone.Width >= zone.Height * 1.4f)
        {
            var cellWPct = zone.Height * 0.85f;
            var n = Math.Max(1, (int)Math.Floor(zone.Width / cellWPct));
            var cellW = pw / n;
            for (var i = 0; i < n; i++)
            {
                var subKey = $"{key}|{i}";
                var tb = CreateCharBoxReadOnly(zone, subKey, sink, ph, cellW);
                _inputLayer.Children.Add(tb);
                Canvas.SetLeft(tb, px + i * cellW);
                Canvas.SetTop(tb, py);
                tb.Width = cellW;
                tb.Height = ph;
            }
        }
        else
        {
            var tb = CreateCharBoxReadOnly(zone, key, sink, ph, pw);
            _inputLayer.Children.Add(tb);
            Canvas.SetLeft(tb, px);
            Canvas.SetTop(tb, py);
            tb.Width = pw;
            tb.Height = ph;
        }
    }

    private TextBox CreateCharBoxReadOnly(ZoneDefinition zone, string key, IZoneAnswerSink? sink, double cellHeightPx,
        double cellWidthPx)
    {
        var tb = new TextBox
        {
            MaxLength = 1,
            Text = sink?.GetAnswer(key) ?? string.Empty,
            BorderThickness = new Thickness(0),
            Background = SubtleFieldBackground,
            Foreground = SystemColors.ControlTextBrush,
            IsReadOnly = true,
            IsTabStop = false,
            Focusable = false,
            TextAlignment = TextAlignment.Center
        };
        ApplyReadOnlyTextMetrics(tb, cellHeightPx, cellWidthPx, VerticalAlignment.Center,
            HorizontalAlignment.Center);
        return tb;
    }

    private void AddTextBoxReadOnly(ZoneDefinition zone, int pageIdx, double px, double py, double pw, double ph,
        IZoneAnswerSink? sink, bool multiline)
    {
        var key = AnswerKey(pageIdx, zone.Id);
        var raw = sink?.GetAnswer(key) ?? string.Empty;
        var display = raw;
        if (raw.TrimStart().StartsWith('{') && ZoneInkAnswerCodec.TryParse(raw, out var tx, out _))
            display = tx ?? string.Empty;

        var tb = new TextBox
        {
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            AcceptsReturn = false,
            VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            Text = display,
            BorderThickness = new Thickness(0),
            Background = SubtleFieldBackground,
            Foreground = SystemColors.ControlTextBrush,
            IsReadOnly = true,
            IsTabStop = false,
            Focusable = false
        };
        if (multiline)
            ApplyReadOnlyTextMetrics(tb, 12, pw, VerticalAlignment.Top, HorizontalAlignment.Left, 9);
        else
            ApplyReadOnlyTextMetrics(tb, ph, pw, VerticalAlignment.Center, HorizontalAlignment.Left);
        _inputLayer.Children.Add(tb);
        Canvas.SetLeft(tb, px);
        Canvas.SetTop(tb, py);
        tb.Width = pw;
        tb.Height = ph;
    }

    private void AddCellGridReadOnly(ZoneDefinition zone, int pageIdx, double px, double py, double pw, double ph,
        IZoneAnswerSink? sink)
    {
        var cellPct = Math.Max(0.5f, Math.Min(zone.Width, zone.Height) / 8f);
        var cols = Math.Max(1, (int)Math.Floor(zone.Width / cellPct));
        var rows = Math.Max(1, (int)Math.Floor(zone.Height / cellPct));
        var cw = pw / cols;
        var ch = ph / rows;
        var baseKey = AnswerKey(pageIdx, zone.Id);
        var idx = 0;
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var subKey = $"{baseKey}|{idx++}";
                var tb = CreateCharBoxReadOnly(zone, subKey, sink, ch, cw);
                _inputLayer.Children.Add(tb);
                Canvas.SetLeft(tb, px + c * cw);
                Canvas.SetTop(tb, py + r * ch);
                tb.Width = cw;
                tb.Height = ch;
            }
        }
    }

    private void AddInkReadOnly(ZoneDefinition zone, int pageIdx, double px, double py, double pw, double ph,
        IZoneAnswerSink? sink)
    {
        var key = AnswerKey(pageIdx, zone.Id);
        var raw = sink?.GetAnswer(key);
        ZoneInkAnswerCodec.TryParse(raw, out _, out var initialStrokes);

        var ink = new InkCanvas
        {
            Background = InkFieldBackground,
            IsEnabled = false,
            ClipToBounds = true
        };
        if (initialStrokes is { Count: > 0 })
            ink.Strokes = initialStrokes;

        _inputLayer.Children.Add(ink);
        Canvas.SetLeft(ink, px);
        Canvas.SetTop(ink, py);
        ink.Width = pw;
        ink.Height = ph;
    }

    private void AddTextAndDrawingReadOnly(ZoneDefinition zone, int pageIdx, double px, double py, double pw, double ph,
        IZoneAnswerSink? sink)
    {
        var key = AnswerKey(pageIdx, zone.Id);
        var raw = sink?.GetAnswer(key);
        ZoneInkAnswerCodec.TryParse(raw, out var initialText, out var initialStrokes);

        var root = new Grid { ClipToBounds = true };

        var tb = new TextBox
        {
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = initialText ?? string.Empty,
            BorderThickness = new Thickness(0),
            Background = SubtleFieldBackground,
            Foreground = SystemColors.ControlTextBrush,
            IsReadOnly = true,
            IsTabStop = false,
            Focusable = false,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        const double longAnswerMaxFont = 9;
        ApplyReadOnlyTextMetrics(tb, 12, pw, VerticalAlignment.Top, HorizontalAlignment.Left, longAnswerMaxFont);

        var ink = new InkCanvas
        {
            Background = Brushes.Transparent,
            IsEnabled = false,
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        if (initialStrokes is { Count: > 0 })
            ink.Strokes = initialStrokes;

        Panel.SetZIndex(tb, 0);
        Panel.SetZIndex(ink, 1);

        root.Children.Add(tb);
        root.Children.Add(ink);

        _inputLayer.Children.Add(root);
        Canvas.SetLeft(root, px);
        Canvas.SetTop(root, py);
        root.Width = pw;
        root.Height = ph;
    }

    private static string AnswerKey(int pageIndex, string zoneId) => $"{pageIndex}|{zoneId}";
}
