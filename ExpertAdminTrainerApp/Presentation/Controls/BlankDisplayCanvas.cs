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

    private readonly Image _backgroundImage = new() { Stretch = Stretch.Uniform, IsHitTestVisible = false };

    public BlankDisplayCanvas()
    {
        ClipToBounds = true;
        Background = Brushes.Transparent;
        Children.Add(_backgroundImage);

        SizeChanged += (_, _) => Redraw();
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
        if (d is BlankDisplayCanvas c)
            c.Redraw();
    }

    private static void OnSelectedZoneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BlankDisplayCanvas c)
            c.Redraw();
    }

    protected virtual void Redraw()
    {
        ClearZoneRectangles();
        RecalcImageRect();

        if (ImageRect is { Width: <= 0 } or { Height: <= 0 })
            return;

        var zones = Zones;
        if (zones is null)
            return;

        foreach (var zone in zones)
            AddZoneRectangle(zone);
    }

    private void RecalcImageRect()
    {
        var src = _backgroundImage.Source as BitmapSource;
        if (src is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            ImageRect = Rect.Empty;
            _backgroundImage.Width = 0;
            _backgroundImage.Height = 0;
            return;
        }

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
    }

    protected void AddZoneRectangle(ZoneDefinition zone)
    {
        var rect = CreateRectangle(zone);
        var (px, py, pw, ph) = ZoneToPixels(zone);

        rect.Width = pw;
        rect.Height = ph;
        SetLeft(rect, px);
        SetTop(rect, py);

        Children.Add(rect);
    }

    private void ClearZoneRectangles()
    {
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (Children[i] is Rectangle)
                Children.RemoveAt(i);
        }
    }

    protected virtual Rectangle CreateRectangle(ZoneDefinition zone)
    {
        bool isSelected = SelectedZone is not null && SelectedZone.Id == zone.Id;
        var color = GetZoneColor(zone.FieldType);

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
        _ => Color.FromRgb(158, 158, 158)
    };
}
