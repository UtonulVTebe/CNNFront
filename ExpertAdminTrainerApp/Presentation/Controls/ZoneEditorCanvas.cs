using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;
using ExpertAdminTrainerApp.Domain;

namespace ExpertAdminTrainerApp.Presentation.Controls;

/// <summary>
/// Admin-only canvas that extends BlankDisplayCanvas with zone editing:
/// stamp single cells, stamp rows of N cells, select/move/delete zones.
/// Uses Preview (tunneling) events so clicks always reach the canvas
/// even when child elements (Image, Rectangles) are under the cursor.
/// </summary>
public class ZoneEditorCanvas : BlankDisplayCanvas
{
    public static readonly DependencyProperty EditorModeProperty =
        DependencyProperty.Register(nameof(EditorMode), typeof(ZoneEditorMode), typeof(ZoneEditorCanvas),
            new PropertyMetadata(ZoneEditorMode.Select));

    public static readonly DependencyProperty StampFieldTypeProperty =
        DependencyProperty.Register(nameof(StampFieldType), typeof(ZoneFieldType), typeof(ZoneEditorCanvas),
            new PropertyMetadata(ZoneFieldType.ShortAnswer));

    public static readonly DependencyProperty StampFieldNameProperty =
        DependencyProperty.Register(nameof(StampFieldName), typeof(string), typeof(ZoneEditorCanvas),
            new PropertyMetadata("Answer"));

    public static readonly DependencyProperty StampTaskNumberProperty =
        DependencyProperty.Register(nameof(StampTaskNumber), typeof(int), typeof(ZoneEditorCanvas),
            new PropertyMetadata(1));

    public static readonly DependencyProperty CellWidthPercentProperty =
        DependencyProperty.Register(nameof(CellWidthPercent), typeof(float), typeof(ZoneEditorCanvas),
            new PropertyMetadata(2.5f));

    public static readonly DependencyProperty CellHeightPercentProperty =
        DependencyProperty.Register(nameof(CellHeightPercent), typeof(float), typeof(ZoneEditorCanvas),
            new PropertyMetadata(3.0f));

    public static readonly DependencyProperty RowCellCountProperty =
        DependencyProperty.Register(nameof(RowCellCount), typeof(int), typeof(ZoneEditorCanvas),
            new PropertyMetadata(1));

    public static readonly DependencyProperty CellGapPercentProperty =
        DependencyProperty.Register(nameof(CellGapPercent), typeof(float), typeof(ZoneEditorCanvas),
            new PropertyMetadata(0.2f));

    public static readonly RoutedEvent ZoneAddedEvent =
        EventManager.RegisterRoutedEvent(nameof(ZoneAdded), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(ZoneEditorCanvas));

    public static readonly RoutedEvent ZoneSelectedEvent =
        EventManager.RegisterRoutedEvent(nameof(ZoneSelected), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(ZoneEditorCanvas));

    private bool _isDragging;
    private Point _dragStart;
    private ZoneDefinition? _draggingZone;
    private float _dragOrigX, _dragOrigY;

    public ZoneEditorMode EditorMode
    {
        get => (ZoneEditorMode)GetValue(EditorModeProperty);
        set => SetValue(EditorModeProperty, value);
    }

    public ZoneFieldType StampFieldType
    {
        get => (ZoneFieldType)GetValue(StampFieldTypeProperty);
        set => SetValue(StampFieldTypeProperty, value);
    }

    public string StampFieldName
    {
        get => (string)GetValue(StampFieldNameProperty);
        set => SetValue(StampFieldNameProperty, value);
    }

    public int StampTaskNumber
    {
        get => (int)GetValue(StampTaskNumberProperty);
        set => SetValue(StampTaskNumberProperty, value);
    }

    public float CellWidthPercent
    {
        get => (float)GetValue(CellWidthPercentProperty);
        set => SetValue(CellWidthPercentProperty, value);
    }

    public float CellHeightPercent
    {
        get => (float)GetValue(CellHeightPercentProperty);
        set => SetValue(CellHeightPercentProperty, value);
    }

    public int RowCellCount
    {
        get => (int)GetValue(RowCellCountProperty);
        set => SetValue(RowCellCountProperty, value);
    }

    public float CellGapPercent
    {
        get => (float)GetValue(CellGapPercentProperty);
        set => SetValue(CellGapPercentProperty, value);
    }

    public event RoutedEventHandler ZoneAdded
    {
        add => AddHandler(ZoneAddedEvent, value);
        remove => RemoveHandler(ZoneAddedEvent, value);
    }

    public event RoutedEventHandler ZoneSelected
    {
        add => AddHandler(ZoneSelectedEvent, value);
        remove => RemoveHandler(ZoneSelectedEvent, value);
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        Focus();

        if (ImageRect is { Width: <= 0 } or { Height: <= 0 })
            return;

        var pos = e.GetPosition(this);

        if (!ImageRect.Contains(pos))
            return;

        switch (EditorMode)
        {
            case ZoneEditorMode.Select:
                HandleSelectDown(pos);
                break;
            case ZoneEditorMode.StampSingle:
                HandleStamp(pos, 1);
                break;
            case ZoneEditorMode.StampRow:
                HandleStamp(pos, Math.Max(1, RowCellCount));
                break;
        }

        e.Handled = true;
        CaptureMouse();
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);

        if (!_isDragging || _draggingZone is null)
            return;

        var pos = e.GetPosition(this);
        var dx = pos.X - _dragStart.X;
        var dy = pos.Y - _dragStart.Y;

        float dxPct = (float)(dx / ImageRect.Width * 100.0);
        float dyPct = (float)(dy / ImageRect.Height * 100.0);

        _draggingZone.X = Math.Clamp(_dragOrigX + dxPct, 0, 100 - _draggingZone.Width);
        _draggingZone.Y = Math.Clamp(_dragOrigY + dyPct, 0, 100 - _draggingZone.Height);

        Redraw();
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);

        if (_isDragging)
        {
            _isDragging = false;
            _draggingZone = null;
            ReleaseMouseCapture();
            RaiseEvent(new RoutedEventArgs(ZoneAddedEvent, this));
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Delete && SelectedZone is not null)
        {
            var zones = Zones as IList<ZoneDefinition>;
            zones?.Remove(SelectedZone);
            SelectedZone = null;
            RaiseEvent(new RoutedEventArgs(ZoneAddedEvent, this));
            Redraw();
        }
    }

    private void HandleSelectDown(Point pos)
    {
        var hit = HitTestZone(pos);
        SelectedZone = hit;
        RaiseEvent(new RoutedEventArgs(ZoneSelectedEvent, this));

        if (hit is not null)
        {
            _isDragging = true;
            _dragStart = pos;
            _draggingZone = hit;
            _dragOrigX = hit.X;
            _dragOrigY = hit.Y;
        }

        Redraw();
    }

    private void HandleStamp(Point pos, int count)
    {
        var (xPct, yPct) = PixelsToPercent(pos.X, pos.Y);

        float w = CellWidthPercent;
        float h = CellHeightPercent;
        float gap = CellGapPercent;
        string baseName = StampFieldName;
        int baseTask = StampTaskNumber;

        var zones = Zones as IList<ZoneDefinition>;
        if (zones is null) return;

        for (int i = 0; i < count; i++)
        {
            var zone = new ZoneDefinition
            {
                FieldName = count > 1 ? $"{baseName}_{baseTask + i}" : baseName,
                FieldType = StampFieldType,
                TaskNumber = baseTask + i,
                X = Math.Clamp(xPct + i * (w + gap), 0, 100 - w),
                Y = Math.Clamp(yPct, 0, 100 - h),
                Width = w,
                Height = h
            };
            zones.Add(zone);
        }

        RaiseEvent(new RoutedEventArgs(ZoneAddedEvent, this));
        Redraw();
    }

    private ZoneDefinition? HitTestZone(Point pos)
    {
        var zones = Zones;
        if (zones is null) return null;

        for (int i = zones.Count - 1; i >= 0; i--)
        {
            var zone = zones[i];
            var (zx, zy, zw, zh) = ZoneToPixels(zone);
            if (pos.X >= zx && pos.X <= zx + zw && pos.Y >= zy && pos.Y <= zy + zh)
                return zone;
        }
        return null;
    }

    protected override Rectangle CreateRectangle(ZoneDefinition zone)
    {
        var rect = base.CreateRectangle(zone);
        rect.IsHitTestVisible = false;
        rect.Cursor = EditorMode == ZoneEditorMode.Select ? Cursors.SizeAll : Cursors.Cross;
        return rect;
    }
}

public enum ZoneEditorMode
{
    Select,
    StampSingle,
    StampRow
}
