using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ExpertAdminTrainerApp.Domain;

namespace ExpertAdminTrainerApp.Presentation.Controls;

/// <summary>
/// Read-only canvas that renders a blank image with colored zone overlays.
/// Reusable on the student client — no editing logic here.
/// </summary>
public class BlankDisplayCanvas : Canvas
{
    public static readonly DependencyProperty ImageSourceProperty =
        DependencyProperty.Register(nameof(ImageSource), typeof(ImageSource), typeof(BlankDisplayCanvas),
            new PropertyMetadata(null, OnImageSourceChanged));

    public static readonly DependencyProperty ZonesProperty =
        DependencyProperty.Register(nameof(Zones), typeof(IReadOnlyList<ZoneDefinition>), typeof(BlankDisplayCanvas),
            new PropertyMetadata(null, OnZonesChanged));

    public static readonly DependencyProperty SelectedZoneProperty =
        DependencyProperty.Register(nameof(SelectedZone), typeof(ZoneDefinition), typeof(BlankDisplayCanvas),
            new PropertyMetadata(null, OnSelectedZoneChanged));

    public static readonly DependencyProperty HighlightedZonesProperty =
        DependencyProperty.Register(nameof(HighlightedZones), typeof(IEnumerable), typeof(BlankDisplayCanvas),
            new PropertyMetadata(null, OnHighlightedZonesChanged));

    private readonly Image _backgroundImage = new() { Stretch = Stretch.Uniform, IsHitTestVisible = false };
    private int _suppressRedraw;
    private bool _redrawPending;
    private INotifyCollectionChanged? _zonesCollectionNotify;

    public BlankDisplayCanvas()
    {
        ClipToBounds = true;
        Background = Brushes.Transparent;
        Children.Add(_backgroundImage);

        SizeChanged += (_, _) => Redraw();
    }

    public void BeginBatchUpdate() => _suppressRedraw++;

    public void EndBatchUpdate()
    {
        if (--_suppressRedraw <= 0)
        {
            _suppressRedraw = 0;
            if (_redrawPending)
            {
                _redrawPending = false;
                Redraw();
            }
        }
    }

    public ImageSource? ImageSource
    {
        get => (ImageSource?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public IReadOnlyList<ZoneDefinition>? Zones
    {
        get => (IReadOnlyList<ZoneDefinition>?)GetValue(ZonesProperty);
        set => SetValue(ZonesProperty, value);
    }

    public ZoneDefinition? SelectedZone
    {
        get => (ZoneDefinition?)GetValue(SelectedZoneProperty);
        set => SetValue(SelectedZoneProperty, value);
    }

    /// <summary>Дополнительная подсветка (мультивыбор). Элементы — <see cref="ZoneDefinition"/>.</summary>
    public IEnumerable? HighlightedZones
    {
        get => (IEnumerable?)GetValue(HighlightedZonesProperty);
        set => SetValue(HighlightedZonesProperty, value);
    }

    protected Rect ImageRect { get; private set; }

    private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BlankDisplayCanvas c)
        {
            c._backgroundImage.Source = e.NewValue as ImageSource;
            c.Redraw();
        }
    }

    private static void OnZonesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not BlankDisplayCanvas c)
            return;

        if (c._zonesCollectionNotify is not null)
        {
            c._zonesCollectionNotify.CollectionChanged -= c.OnZonesCollectionChanged;
            c._zonesCollectionNotify = null;
        }

        if (e.NewValue is INotifyCollectionChanged n)
        {
            c._zonesCollectionNotify = n;
            n.CollectionChanged += c.OnZonesCollectionChanged;
        }

        c.Redraw();
    }

    private void OnZonesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_suppressRedraw > 0)
        {
            _redrawPending = true;
            return;
        }

        Redraw();
    }

    private static void OnSelectedZoneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BlankDisplayCanvas c)
            c.RefreshZoneHighlightStyles();
    }

    private static void OnHighlightedZonesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BlankDisplayCanvas c)
            c.RefreshZoneHighlightStyles();
    }

    /// <summary>Полная перерисовка фона и зон. Подавляется внутри BeginBatchUpdate/EndBatchUpdate.</summary>
    protected virtual void Redraw()
    {
        if (_suppressRedraw > 0)
        {
            _redrawPending = true;
            return;
        }

        if (!RecalcImageRect())
            return;

        ClearZoneRectangles();

        var zones = Zones;
        if (zones is null)
            return;

        foreach (var zone in zones)
            AddZoneRectangle(zone);
    }

    /// <summary>Только позиции/размеры прямоугольников зон — без удаления всех детей (нет мигания при перетаскивании).</summary>
    protected void UpdateZoneLayoutVisuals()
    {
        if (ImageRect is { Width: <= 0 } or { Height: <= 0 })
            return;

        var zones = Zones;
        if (zones is null)
            return;

        var alive = new HashSet<string>();
        foreach (var zone in zones)
            alive.Add(zone.Id);

        for (var i = Children.Count - 1; i >= 0; i--)
        {
            if (Children[i] is not Rectangle r || r.Tag is not ZoneDefinition z)
                continue;
            if (!alive.Contains(z.Id))
                Children.RemoveAt(i);
        }

        foreach (var zone in zones)
        {
            var rect = FindRectangleForZone(zone.Id);
            if (rect is null)
            {
                AddZoneRectangle(zone);
                continue;
            }

            ApplyRectangleLayout(rect, zone);
            ApplyRectangleHighlight(rect, zone);
        }
    }

    protected void RefreshZoneHighlightStyles()
    {
        var zones = Zones;
        if (zones is null)
            return;

        foreach (var zone in zones)
        {
            var rect = FindRectangleForZone(zone.Id);
            if (rect is not null)
                ApplyRectangleHighlight(rect, zone);
        }
    }

    private Rectangle? FindRectangleForZone(string zoneId)
    {
        foreach (UIElement child in Children)
        {
            if (child is Rectangle r && r.Tag is ZoneDefinition z && z.Id == zoneId)
                return r;
        }

        return null;
    }

    private void ApplyRectangleLayout(Rectangle rect, ZoneDefinition zone)
    {
        var (px, py, pw, ph) = ZoneToPixels(zone);
        rect.Width = pw;
        rect.Height = ph;
        SetLeft(rect, px);
        SetTop(rect, py);
    }

    private void ApplyRectangleHighlight(Rectangle rect, ZoneDefinition zone)
    {
        bool isSelected = IsZoneHighlighted(zone);
        var color = GetZoneColor(zone.FieldType);
        rect.Fill = new SolidColorBrush(Color.FromArgb(50, color.R, color.G, color.B));
        rect.Stroke = new SolidColorBrush(isSelected
            ? Color.FromRgb(255, 60, 60)
            : Color.FromArgb(180, color.R, color.G, color.B));
        rect.StrokeThickness = isSelected ? 2.5 : 1.5;
    }

    /// <summary>Пересчёт позиции фона и <see cref="ImageRect"/>. Возвращает false, если зоны рисовать пока нельзя.</summary>
    private bool RecalcImageRect()
    {
        var src = _backgroundImage.Source as BitmapSource;
        if (src is null)
        {
            ImageRect = Rect.Empty;
            _backgroundImage.Width = 0;
            _backgroundImage.Height = 0;
            return false;
        }

        if (ActualWidth <= 0 || ActualHeight <= 0)
            return ImageRect.Width > 0 && ImageRect.Height > 0;

        double imgW = src.PixelWidth;
        double imgH = src.PixelHeight;
        double scale = Math.Min(ActualWidth / imgW, ActualHeight / imgH);
        double w = imgW * scale;
        double h = imgH * scale;
        double x = (ActualWidth - w) / 2;
        double y = (ActualHeight - h) / 2;

        _backgroundImage.Width = w;
        _backgroundImage.Height = h;
        SetLeft(_backgroundImage, x);
        SetTop(_backgroundImage, y);

        ImageRect = new Rect(x, y, w, h);
        return true;
    }

    protected void AddZoneRectangle(ZoneDefinition zone)
    {
        var rect = CreateRectangle(zone);
        ApplyRectangleLayout(rect, zone);

        Children.Add(rect);
    }

    private void ClearZoneRectangles()
    {
        for (var i = Children.Count - 1; i >= 0; i--)
        {
            if (Children[i] is Rectangle)
                Children.RemoveAt(i);
        }
    }

    protected virtual Rectangle CreateRectangle(ZoneDefinition zone)
    {
        var color = GetZoneColor(zone.FieldType);
        bool isSelected = IsZoneHighlighted(zone);

        return new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(50, color.R, color.G, color.B)),
            Stroke = new SolidColorBrush(isSelected
                ? Color.FromRgb(255, 60, 60)
                : Color.FromArgb(180, color.R, color.G, color.B)),
            StrokeThickness = isSelected ? 2.5 : 1.5,
            RadiusX = 2,
            RadiusY = 2,
            Tag = zone,
            IsHitTestVisible = true
        };
    }

    protected bool IsZoneHighlighted(ZoneDefinition zone)
    {
        if (SelectedZone is not null && SelectedZone.Id == zone.Id)
            return true;
        var hz = HighlightedZones;
        if (hz is null)
            return false;
        foreach (var item in hz)
        {
            if (item is ZoneDefinition z && z.Id == zone.Id)
                return true;
        }

        return false;
    }

    protected (double x, double y, double w, double h) ZoneToPixels(ZoneDefinition zone)
    {
        double x = ImageRect.X + zone.X / 100.0 * ImageRect.Width;
        double y = ImageRect.Y + zone.Y / 100.0 * ImageRect.Height;
        double w = zone.Width / 100.0 * ImageRect.Width;
        double h = zone.Height / 100.0 * ImageRect.Height;
        return (x, y, w, h);
    }

    protected (float xPct, float yPct) PixelsToPercent(double px, double py)
    {
        if (ImageRect is { Width: <= 0 } or { Height: <= 0 })
            return (0, 0);

        float xPct = (float)((px - ImageRect.X) / ImageRect.Width * 100.0);
        float yPct = (float)((py - ImageRect.Y) / ImageRect.Height * 100.0);
        return (xPct, yPct);
    }

    protected static Color GetZoneColor(ZoneFieldType fieldType) => fieldType switch
    {
        ZoneFieldType.Header => Color.FromRgb(33, 150, 243),
        ZoneFieldType.ShortAnswer => Color.FromRgb(76, 175, 80),
        ZoneFieldType.LongAnswer => Color.FromRgb(156, 39, 176),
        ZoneFieldType.FreeForm => Color.FromRgb(121, 85, 72),
        ZoneFieldType.Correction => Color.FromRgb(255, 152, 0),
        ZoneFieldType.CellGrid => Color.FromRgb(0, 151, 167),
        ZoneFieldType.Drawing => Color.FromRgb(233, 30, 99),
        _ => Color.FromRgb(158, 158, 158)
    };
}
