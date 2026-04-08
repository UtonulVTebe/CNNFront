using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TrainerStudApp.Domain;

namespace TrainerStudApp.Presentation.Controls;

/// <summary>
/// Бланк с интерактивным вводом поверх <see cref="BlankDisplayCanvas"/>.
/// </summary>
public class BlankFillCanvas : BlankDisplayCanvas
{
    private static readonly Brush SubtleFieldBackground = new SolidColorBrush(Color.FromArgb(15, 245, 245, 248));
    private static readonly Brush InkFieldBackground = new SolidColorBrush(Color.FromArgb(12, 245, 245, 248));

    static BlankFillCanvas()
    {
        if (SubtleFieldBackground is SolidColorBrush sb) sb.Freeze();
        if (InkFieldBackground is SolidColorBrush ib) ib.Freeze();
    }

    public static readonly DependencyProperty PageIndexProperty =
        DependencyProperty.Register(nameof(PageIndex), typeof(int), typeof(BlankFillCanvas),
            new PropertyMetadata(0, (d, _) => { if (d is BlankFillCanvas c) c.ScheduleRebuild(); }));

    public static readonly DependencyProperty AnswerSinkProperty =
        DependencyProperty.Register(nameof(AnswerSink), typeof(IZoneAnswerSink), typeof(BlankFillCanvas),
            new PropertyMetadata(null, (d, _) => { if (d is BlankFillCanvas c) c.ScheduleRebuild(); }));

    private readonly Canvas _inputLayer = new() { Background = Brushes.Transparent, IsHitTestVisible = true };
    private INotifyCollectionChanged? _zonesNotify;
    private bool _rebuildScheduled;

    public BlankFillCanvas()
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
                case ZoneFieldType.Drawing when zone.InputMode == ZoneInputMode.Drawing:
                    AddInk(zone, pageIdx, px, py, pw, ph, sink);
                    break;
                case ZoneFieldType.CellGrid when zone.InputMode != ZoneInputMode.Drawing:
                    AddCellGrid(zone, pageIdx, px, py, pw, ph, sink);
                    break;
                default:
                    if (zone.InputMode == ZoneInputMode.Text || zone.InputMode == ZoneInputMode.Drawing)
                    {
                        if (zone.InputMode == ZoneInputMode.Drawing)
                            AddInk(zone, pageIdx, px, py, pw, ph, sink);
                        else
                            AddTextBox(zone, pageIdx, px, py, pw, ph, sink, multiline: zone.FieldType is ZoneFieldType.LongAnswer or ZoneFieldType.FreeForm);
                    }
                    else
                        AddSingleOrRowCells(zone, pageIdx, px, py, pw, ph, sink);
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

    private void AddSingleOrRowCells(ZoneDefinition zone, int pageIdx, double px, double py, double pw, double ph,
        IZoneAnswerSink? sink)
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
                var tb = CreateCharBox(zone, subKey, sink, ph, cellW);
                _inputLayer.Children.Add(tb);
                Canvas.SetLeft(tb, px + i * cellW);
                Canvas.SetTop(tb, py);
                tb.Width = cellW;
                tb.Height = ph;
            }
        }
        else
        {
            var tb = CreateCharBox(zone, key, sink, ph, pw);
            _inputLayer.Children.Add(tb);
            Canvas.SetLeft(tb, px);
            Canvas.SetTop(tb, py);
            tb.Width = pw;
            tb.Height = ph;
        }
    }

    private static void ApplyEditableTextMetrics(TextBox tb, double cellHeightPx, double cellWidthPx,
        VerticalAlignment contentV, HorizontalAlignment contentH)
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
        tb.FontSize = fs;
    }

    private TextBox CreateCharBox(ZoneDefinition zone, string key, IZoneAnswerSink? sink, double cellHeightPx,
        double cellWidthPx)
    {
        var tb = new TextBox
        {
            MaxLength = 1,
            Text = sink?.GetAnswer(key) ?? string.Empty,
            BorderThickness = new Thickness(0),
            Background = SubtleFieldBackground,
            Foreground = SystemColors.ControlTextBrush,
            CaretBrush = SystemColors.ControlTextBrush,
            Tag = key,
            TextAlignment = TextAlignment.Center
        };
        ApplyEditableTextMetrics(tb, cellHeightPx, cellWidthPx, VerticalAlignment.Center,
            HorizontalAlignment.Center);
        tb.TextChanged += (_, _) =>
        {
            var t = tb.Text.Replace("\r", "").Replace("\n", "");
            if (t.Length > 1)
                t = t[..1];
            if (tb.Text != t) tb.Text = t;
            sink?.SetAnswer(key, string.IsNullOrEmpty(t) ? null : t);
        };
        tb.PreviewTextInput += (_, e) =>
        {
            if (e.Text.Length == 0) return;
            var ch = e.Text[0];
            if (!IsCharAllowed(ch, zone.Validation, 0))
                e.Handled = true;
        };
        return tb;
    }

    private void AddTextBox(ZoneDefinition zone, int pageIdx, double px, double py, double pw, double ph,
        IZoneAnswerSink? sink, bool multiline)
    {
        var key = AnswerKey(pageIdx, zone.Id);
        var tb = new TextBox
        {
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            AcceptsReturn = multiline,
            VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            Text = sink?.GetAnswer(key) ?? string.Empty,
            BorderThickness = new Thickness(0),
            Background = SubtleFieldBackground,
            Foreground = SystemColors.ControlTextBrush,
            CaretBrush = SystemColors.ControlTextBrush,
            Tag = key
        };
        ApplyEditableTextMetrics(tb, ph, pw,
            multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
            HorizontalAlignment.Left);
        tb.TextChanged += (_, _) => sink?.SetAnswer(key, string.IsNullOrEmpty(tb.Text) ? null : tb.Text);
        _inputLayer.Children.Add(tb);
        Canvas.SetLeft(tb, px);
        Canvas.SetTop(tb, py);
        tb.Width = pw;
        tb.Height = ph;
    }

    private void AddCellGrid(ZoneDefinition zone, int pageIdx, double px, double py, double pw, double ph,
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
                var tb = CreateCharBox(zone, subKey, sink, ch, cw);
                _inputLayer.Children.Add(tb);
                Canvas.SetLeft(tb, px + c * cw);
                Canvas.SetTop(tb, py + r * ch);
                tb.Width = cw;
                tb.Height = ch;
            }
        }
    }

    private void AddInk(ZoneDefinition zone, int pageIdx, double px, double py, double pw, double ph,
        IZoneAnswerSink? sink)
    {
        var key = AnswerKey(pageIdx, zone.Id);
        var ink = new InkCanvas
        {
            Background = InkFieldBackground,
            Tag = key
        };
        _inputLayer.Children.Add(ink);
        Canvas.SetLeft(ink, px);
        Canvas.SetTop(ink, py);
        ink.Width = pw;
        ink.Height = ph;
        ink.StrokeCollected += (_, _) => sink?.SetAnswer(key, "1");
    }

    private static string AnswerKey(int pageIndex, string zoneId) => $"{pageIndex}|{zoneId}";

    private static bool IsCharAllowed(char c, ZoneValidationRules? v, int indexInMask)
    {
        if (v is null) return !char.IsControl(c);
        if (v.DigitsOnly == true && !char.IsDigit(c)) return false;
        if (v.LettersOnly == true && !char.IsLetter(c)) return false;
        if (!string.IsNullOrEmpty(v.Mask) && indexInMask < v.Mask.Length)
        {
            var m = char.ToUpperInvariant(v.Mask[indexInMask]);
            return m switch
            {
                '9' or '#' => char.IsDigit(c),
                'L' => char.IsLetter(c),
                'X' or '*' => true,
                _ => char.ToUpperInvariant(c) == m || m == 'X'
            };
        }

        if (!string.IsNullOrEmpty(v.Pattern))
        {
            try
            {
                if (!Regex.IsMatch(c.ToString(), v.Pattern))
                    return false;
            }
            catch { /* ignore invalid regex */ }
        }

        return !char.IsControl(c);
    }
}
