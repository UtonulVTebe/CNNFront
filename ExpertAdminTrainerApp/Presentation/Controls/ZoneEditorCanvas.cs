using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    public static readonly DependencyProperty StampInputModeProperty =
        DependencyProperty.Register(nameof(StampInputMode), typeof(ZoneInputMode), typeof(ZoneEditorCanvas),
            new PropertyMetadata(ZoneInputMode.Cell));

    public static readonly DependencyProperty StampFieldRoleProperty =
        DependencyProperty.Register(nameof(StampFieldRole), typeof(string), typeof(ZoneEditorCanvas),
            new PropertyMetadata(null));

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

    public static readonly DependencyProperty SelectedZonesProperty =
        DependencyProperty.Register(nameof(SelectedZones), typeof(IList<ZoneDefinition>), typeof(ZoneEditorCanvas),
            new PropertyMetadata(null, OnSelectedZonesChanged));

    public static readonly DependencyProperty PendingPresetProperty =
        DependencyProperty.Register(nameof(PendingPreset), typeof(ZonePresetTemplate), typeof(ZoneEditorCanvas),
            new PropertyMetadata(null));

    public static readonly RoutedEvent ZoneAddedEvent =
        EventManager.RegisterRoutedEvent(nameof(ZoneAdded), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(ZoneEditorCanvas));

    public static readonly RoutedEvent ZoneSelectedEvent =
        EventManager.RegisterRoutedEvent(nameof(ZoneSelected), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(ZoneEditorCanvas));

    public static readonly RoutedEvent ZoneMutationStartingEvent =
        EventManager.RegisterRoutedEvent(nameof(ZoneMutationStarting), RoutingStrategy.Bubble,
            typeof(ZoneMutationStartingEventHandler), typeof(ZoneEditorCanvas));

    public static readonly RoutedEvent PendingPresetConsumedEvent =
        EventManager.RegisterRoutedEvent(nameof(PendingPresetConsumed), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(ZoneEditorCanvas));

    private bool _isDragging;
    private bool _undoPushedForCurrentDrag;
    private Point _dragStart;
    private readonly List<ZoneDefinition> _draggingZones = [];
    private readonly Dictionary<string, (float x, float y)> _dragOrigins = new();
    private INotifyCollectionChanged? _selectedZonesNotify;

    private bool _isDrawingRectangle;
    private Point _drawRectStart;
    private Rectangle? _drawRectVisual;

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

    public ZoneInputMode StampInputMode
    {
        get => (ZoneInputMode)GetValue(StampInputModeProperty);
        set => SetValue(StampInputModeProperty, value);
    }

    public string? StampFieldRole
    {
        get => (string?)GetValue(StampFieldRoleProperty);
        set => SetValue(StampFieldRoleProperty, value);
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

    public IList<ZoneDefinition>? SelectedZones
    {
        get => (IList<ZoneDefinition>?)GetValue(SelectedZonesProperty);
        set => SetValue(SelectedZonesProperty, value);
    }

    public ZonePresetTemplate? PendingPreset
    {
        get => (ZonePresetTemplate?)GetValue(PendingPresetProperty);
        set => SetValue(PendingPresetProperty, value);
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

    public event ZoneMutationStartingEventHandler ZoneMutationStarting
    {
        add => AddHandler(ZoneMutationStartingEvent, value);
        remove => RemoveHandler(ZoneMutationStartingEvent, value);
    }

    public event RoutedEventHandler PendingPresetConsumed
    {
        add => AddHandler(PendingPresetConsumedEvent, value);
        remove => RemoveHandler(PendingPresetConsumedEvent, value);
    }

    private static void OnSelectedZonesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ZoneEditorCanvas c)
            return;

        if (c._selectedZonesNotify is not null)
        {
            c._selectedZonesNotify.CollectionChanged -= c.OnSelectedZonesCollectionChanged;
            c._selectedZonesNotify = null;
        }

        if (e.NewValue is INotifyCollectionChanged n)
        {
            c._selectedZonesNotify = n;
            n.CollectionChanged += c.OnSelectedZonesCollectionChanged;
        }

        c.RefreshZoneHighlightStyles();
    }

    private void OnSelectedZonesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshZoneHighlightStyles();

    private void RaiseMutationStarting(ZoneMutationKind kind) =>
        RaiseEvent(new ZoneMutationStartingEventArgs(ZoneMutationStartingEvent, this, kind));

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        Focus();

        if (ImageRect is { Width: <= 0 } or { Height: <= 0 })
            return;

        var pos = e.GetPosition(this);

        if (!ImageRect.Contains(pos))
            return;

        if (PendingPreset is { } preset)
        {
            RaiseMutationStarting(ZoneMutationKind.Stamp);
            BeginBatchUpdate();
            try
            {
                InsertPresetAt(preset, pos);
                SetValue(PendingPresetProperty, null);
                RaiseEvent(new RoutedEventArgs(PendingPresetConsumedEvent, this));
                e.Handled = true;
                RaiseEvent(new RoutedEventArgs(ZoneAddedEvent, this));
            }
            finally
            {
                EndBatchUpdate();
            }

            return;
        }

        switch (EditorMode)
        {
            case ZoneEditorMode.Select:
                HandleSelectDown(pos);
                if (_isDragging)
                    CaptureMouse();
                break;
            case ZoneEditorMode.StampSingle:
                RaiseMutationStarting(ZoneMutationKind.Stamp);
                HandleStamp(pos, 1);
                break;
            case ZoneEditorMode.StampRow:
                RaiseMutationStarting(ZoneMutationKind.Stamp);
                HandleStamp(pos, Math.Max(1, RowCellCount));
                break;
            case ZoneEditorMode.DrawRectangle:
                RaiseMutationStarting(ZoneMutationKind.Stamp);
                _isDrawingRectangle = true;
                _drawRectStart = pos;
                EnsureDrawRectVisual();
                UpdateDrawRectVisual(pos);
                CaptureMouse();
                break;
        }

        e.Handled = true;
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);

        if (_isDrawingRectangle && e.LeftButton == MouseButtonState.Pressed)
        {
            var cur = e.GetPosition(this);
            UpdateDrawRectVisual(cur);
            e.Handled = true;
            return;
        }

        if (!_isDragging || _draggingZones.Count == 0)
            return;

        var pos = e.GetPosition(this);

        if (!_undoPushedForCurrentDrag)
        {
            var moved = Math.Abs(pos.X - _dragStart.X) > 1.5 || Math.Abs(pos.Y - _dragStart.Y) > 1.5;
            if (moved)
            {
                _undoPushedForCurrentDrag = true;
                RaiseMutationStarting(ZoneMutationKind.DragStart);
            }
        }

        var dx = pos.X - _dragStart.X;
        var dy = pos.Y - _dragStart.Y;

        float dxPct = (float)(dx / ImageRect.Width * 100.0);
        float dyPct = (float)(dy / ImageRect.Height * 100.0);

        foreach (var z in _draggingZones)
        {
            if (!_dragOrigins.TryGetValue(z.Id, out var o))
                continue;
            z.X = Math.Clamp(o.x + dxPct, 0, 100 - z.Width);
            z.Y = Math.Clamp(o.y + dyPct, 0, 100 - z.Height);
        }

        UpdateZoneLayoutVisuals();
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);

        if (_isDrawingRectangle)
        {
            var end = e.GetPosition(this);
            CommitDrawRectangle(end);
            _isDrawingRectangle = false;
            HideDrawRectVisual();
            if (IsMouseCaptured)
                ReleaseMouseCapture();
            RaiseEvent(new RoutedEventArgs(ZoneAddedEvent, this));
            e.Handled = true;
            return;
        }

        if (_isDragging)
        {
            _isDragging = false;
            _undoPushedForCurrentDrag = false;
            _draggingZones.Clear();
            _dragOrigins.Clear();
            RaiseEvent(new RoutedEventArgs(ZoneAddedEvent, this));
        }

        if (IsMouseCaptured)
            ReleaseMouseCapture();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (_isDrawingRectangle)
        {
            _isDrawingRectangle = false;
            HideDrawRectVisual();
        }

        if (_isDragging)
        {
            _isDragging = false;
            _undoPushedForCurrentDrag = false;
            _draggingZones.Clear();
            _dragOrigins.Clear();
            RaiseEvent(new RoutedEventArgs(ZoneAddedEvent, this));
        }

        base.OnLostMouseCapture(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Delete)
            return;

        var pageZones = Zones as IList<ZoneDefinition>;
        if (pageZones is null)
            return;

        var toRemove = new List<ZoneDefinition>();
        var sel = SelectedZones;
        if (sel is { Count: > 0 })
        {
            foreach (var z in sel)
                toRemove.Add(z);
        }
        else if (SelectedZone is not null)
        {
            toRemove.Add(SelectedZone);
        }

        if (toRemove.Count == 0)
            return;

        RaiseMutationStarting(ZoneMutationKind.Delete);
        BeginBatchUpdate();
        try
        {
            foreach (var z in toRemove)
                pageZones.Remove(z);

            sel?.Clear();
            SelectedZone = null;
            RaiseEvent(new RoutedEventArgs(ZoneAddedEvent, this));
            RaiseEvent(new RoutedEventArgs(ZoneSelectedEvent, this));
        }
        finally
        {
            EndBatchUpdate();
        }
        e.Handled = true;
    }

    private static bool IsAdditiveSelectionDown()
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            return true;
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
            return true;
        var left = Keyboard.GetKeyStates(Key.LeftCtrl);
        var right = Keyboard.GetKeyStates(Key.RightCtrl);
        if ((left & KeyStates.Down) == KeyStates.Down || (right & KeyStates.Down) == KeyStates.Down)
            return true;
        var alt = Keyboard.GetKeyStates(Key.LeftAlt);
        var altR = Keyboard.GetKeyStates(Key.RightAlt);
        return (alt & KeyStates.Down) == KeyStates.Down || (altR & KeyStates.Down) == KeyStates.Down;
    }

    private static bool IsGroupSelectionDown()
    {
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            return true;
        var left = Keyboard.GetKeyStates(Key.LeftShift);
        var right = Keyboard.GetKeyStates(Key.RightShift);
        return (left & KeyStates.Down) == KeyStates.Down || (right & KeyStates.Down) == KeyStates.Down;
    }

    private void HandleSelectDown(Point pos)
    {
        var hit = HitTestZone(pos);
        var all = Zones as IList<ZoneDefinition>;
        var sel = SelectedZones;

        bool ctrl = IsAdditiveSelectionDown();
        bool shift = IsGroupSelectionDown();

        BeginBatchUpdate();
        try
        {
            if (hit is null)
            {
                if (!ctrl)
                {
                    sel?.Clear();
                    SelectedZone = null;
                }

                RaiseEvent(new RoutedEventArgs(ZoneSelectedEvent, this));
                return;
            }

            if (shift && !string.IsNullOrEmpty(hit.GroupId) && all is not null)
            {
                sel?.Clear();
                foreach (var z in all)
                {
                    if (z.GroupId == hit.GroupId)
                        sel?.Add(z);
                }

                SelectedZone = hit;
            }
            else if (ctrl && sel is not null)
            {
                var existing = -1;
                for (var i = 0; i < sel.Count; i++)
                {
                    if (sel[i].Id == hit.Id)
                    {
                        existing = i;
                        break;
                    }
                }

                if (existing >= 0)
                {
                    sel.RemoveAt(existing);
                    SelectedZone = sel.Count > 0 ? sel[^1] : null;
                }
                else
                {
                    sel.Add(hit);
                    SelectedZone = hit;
                }
            }
            else
            {
                sel?.Clear();
                sel?.Add(hit);
                SelectedZone = hit;
            }

            var dragTargets = CollectDragTargets(hit, shift, all, sel);
            _draggingZones.Clear();
            _dragOrigins.Clear();
            foreach (var z in dragTargets)
            {
                _draggingZones.Add(z);
                _dragOrigins[z.Id] = (z.X, z.Y);
            }

            _dragStart = pos;
            _isDragging = true;

            RaiseEvent(new RoutedEventArgs(ZoneSelectedEvent, this));
        }
        finally
        {
            EndBatchUpdate();
        }
    }

    private static List<ZoneDefinition> CollectDragTargets(
        ZoneDefinition hit,
        bool shiftSelectsGroup,
        IList<ZoneDefinition>? all,
        IList<ZoneDefinition>? selected)
    {
        if (shiftSelectsGroup && !string.IsNullOrEmpty(hit.GroupId) && all is not null)
        {
            return all.Where(z => z.GroupId == hit.GroupId).ToList();
        }

        if (selected is { Count: > 0 })
        {
            foreach (var z in selected)
            {
                if (z.Id == hit.Id)
                    return selected.ToList();
            }
        }

        return [hit];
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

        string? rowGroup = count > 1 ? Guid.NewGuid().ToString("N") : null;
        var sharedTask = ShareTaskAcrossRowCells(StampFieldType, count);

        BeginBatchUpdate();
        try
        {
            for (int i = 0; i < count; i++)
            {
                var zone = new ZoneDefinition
                {
                    FieldName = count > 1
                        ? (sharedTask ? $"{baseName}_{i}" : $"{baseName}_{baseTask + i}")
                        : baseName,
                    FieldType = StampFieldType,
                    TaskNumber = sharedTask ? baseTask : baseTask + i,
                    X = Math.Clamp(xPct + i * (w + gap), 0, 100 - w),
                    Y = Math.Clamp(yPct, 0, 100 - h),
                    Width = w,
                    Height = h,
                    GroupId = rowGroup,
                    InputMode = ZoneInputMode.Cell
                };
                zones.Add(zone);
            }

            RaiseEvent(new RoutedEventArgs(ZoneAddedEvent, this));
        }
        finally
        {
            EndBatchUpdate();
        }
    }

    private static bool ShareTaskAcrossRowCells(ZoneFieldType fieldType, int count) =>
        count > 1 && (fieldType is ZoneFieldType.ShortAnswer or ZoneFieldType.Correction);

    private void InsertPresetAt(ZonePresetTemplate preset, Point pos)
    {
        var zones = Zones as IList<ZoneDefinition>;
        if (zones is null) return;

        if (string.Equals(preset.Id, ZonePresetTemplate.CorrectionTaskNumberThenAnswerId, StringComparison.Ordinal))
        {
            InsertCorrectionTaskNumberThenAnswerPreset(preset, pos);
            return;
        }

        int count = preset.CellCount < 0 ? Math.Max(1, RowCellCount) : preset.CellCount;
        var (xPct, yPct) = PixelsToPercent(pos.X, pos.Y);
        float w = preset.DefaultWidthPercent ?? CellWidthPercent;
        float h = preset.DefaultHeightPercent ?? CellHeightPercent;
        float gap = CellGapPercent;
        int baseTask = StampTaskNumber;
        string? rowGroup = count > 1 ? Guid.NewGuid().ToString("N") : null;
        var sharedTask = ShareTaskAcrossRowCells(preset.FieldType, count);

        for (int i = 0; i < count; i++)
        {
            var validation = ZoneDefinitionCopy.CloneValidation(preset.Validation);
            var xi = Math.Clamp(xPct + i * (w + gap), 0, 100 - w);
            var yi = Math.Clamp(yPct, 0, 100 - h);
            var zone = new ZoneDefinition
            {
                FieldName = count > 1
                    ? (sharedTask ? $"{preset.BaseFieldName}_{i}" : $"{preset.BaseFieldName}_{baseTask + i}")
                    : preset.BaseFieldName,
                FieldType = preset.FieldType,
                TaskNumber = sharedTask ? baseTask : baseTask + i,
                X = xi,
                Y = yi,
                Width = w,
                Height = h,
                GroupId = rowGroup,
                FieldRole = preset.FieldRole,
                InputMode = preset.InputMode,
                Validation = validation
            };
            zones.Add(zone);
        }
    }

    /// <summary>
    /// Ряд «исправление»: 2 ячейки номера задания, затем <see cref="RowCellCount"/> ячеек ответа; одна группа, один TaskNumber.
    /// </summary>
    private void InsertCorrectionTaskNumberThenAnswerPreset(ZonePresetTemplate preset, Point pos)
    {
        var zones = Zones as IList<ZoneDefinition>;
        if (zones is null) return;

        const int taskDigitCells = 2;
        var answerCells = Math.Max(1, RowCellCount);
        var total = taskDigitCells + answerCells;
        var (xPct, yPct) = PixelsToPercent(pos.X, pos.Y);
        float w = CellWidthPercent;
        float h = CellHeightPercent;
        float gap = CellGapPercent;
        int baseTask = StampTaskNumber;
        var rowGroup = Guid.NewGuid().ToString("N");
        var taskNumRules = new ZoneValidationRules { DigitsOnly = true, MinLength = 1, MaxLength = 2 };

        for (var i = 0; i < total; i++)
        {
            var xi = Math.Clamp(xPct + i * (w + gap), 0, 100 - w);
            var yi = Math.Clamp(yPct, 0, 100 - h);
            var isTaskNum = i < taskDigitCells;
            zones.Add(new ZoneDefinition
            {
                FieldName = isTaskNum
                    ? $"{preset.BaseFieldName}_номер_{i}"
                    : $"{preset.BaseFieldName}_ответ_{i - taskDigitCells}",
                FieldType = ZoneFieldType.Correction,
                TaskNumber = baseTask,
                X = xi,
                Y = yi,
                Width = w,
                Height = h,
                GroupId = rowGroup,
                FieldRole = isTaskNum ? "correction_wrong_task_number" : "correction_replacement_answer",
                InputMode = ZoneInputMode.Cell,
                Validation = isTaskNum ? ZoneDefinitionCopy.CloneValidation(taskNumRules) : null
            });
        }
    }

    private void EnsureDrawRectVisual()
    {
        if (_drawRectVisual is not null)
            return;

        _drawRectVisual = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(40, 33, 150, 243)),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        Panel.SetZIndex(_drawRectVisual, 5000);
        Children.Add(_drawRectVisual);
    }

    private void UpdateDrawRectVisual(Point current)
    {
        if (_drawRectVisual is null || ImageRect.Width <= 0)
            return;

        var x0 = Math.Clamp(Math.Min(_drawRectStart.X, current.X), ImageRect.Left, ImageRect.Right);
        var y0 = Math.Clamp(Math.Min(_drawRectStart.Y, current.Y), ImageRect.Top, ImageRect.Bottom);
        var x1 = Math.Clamp(Math.Max(_drawRectStart.X, current.X), ImageRect.Left, ImageRect.Right);
        var y1 = Math.Clamp(Math.Max(_drawRectStart.Y, current.Y), ImageRect.Top, ImageRect.Bottom);

        _drawRectVisual.Visibility = Visibility.Visible;
        Canvas.SetLeft(_drawRectVisual, x0);
        Canvas.SetTop(_drawRectVisual, y0);
        _drawRectVisual.Width = Math.Max(0, x1 - x0);
        _drawRectVisual.Height = Math.Max(0, y1 - y0);
    }

    private void HideDrawRectVisual()
    {
        if (_drawRectVisual is null)
            return;
        _drawRectVisual.Visibility = Visibility.Collapsed;
        _drawRectVisual.Width = 0;
        _drawRectVisual.Height = 0;
    }

    private void CommitDrawRectangle(Point endCanvas)
    {
        if (ImageRect.Width <= 0 || ImageRect.Height <= 0)
            return;

        var x0 = Math.Clamp(Math.Min(_drawRectStart.X, endCanvas.X), ImageRect.Left, ImageRect.Right);
        var y0 = Math.Clamp(Math.Min(_drawRectStart.Y, endCanvas.Y), ImageRect.Top, ImageRect.Bottom);
        var x1 = Math.Clamp(Math.Max(_drawRectStart.X, endCanvas.X), ImageRect.Left, ImageRect.Right);
        var y1 = Math.Clamp(Math.Max(_drawRectStart.Y, endCanvas.Y), ImageRect.Top, ImageRect.Bottom);

        var (xp0, yp0) = PixelsToPercent(x0, y0);
        var (xp1, yp1) = PixelsToPercent(x1, y1);
        var w = Math.Max(xp1 - xp0, 0.6f);
        var h = Math.Max(yp1 - yp0, 0.6f);
        if (w < 0.55f || h < 0.55f)
            return;

        var zones = Zones as IList<ZoneDefinition>;
        if (zones is null)
            return;

        var mode = StampInputMode;
        if (StampFieldType is ZoneFieldType.ShortAnswer or ZoneFieldType.Correction or ZoneFieldType.Header)
            mode = ZoneInputMode.Cell;

        BeginBatchUpdate();
        try
        {
            zones.Add(new ZoneDefinition
            {
                FieldName = StampFieldName,
                FieldType = StampFieldType,
                TaskNumber = StampTaskNumber,
                X = Math.Clamp(Math.Min(xp0, xp1), 0, 100 - w),
                Y = Math.Clamp(Math.Min(yp0, yp1), 0, 100 - h),
                Width = w,
                Height = h,
                InputMode = mode,
                FieldRole = string.IsNullOrWhiteSpace(StampFieldRole) ? null : StampFieldRole.Trim()
            });
        }
        finally
        {
            EndBatchUpdate();
        }
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
    StampRow,
    DrawRectangle
}

public enum ZoneMutationKind
{
    Stamp,
    DragStart,
    Delete
}

public sealed class ZoneMutationStartingEventArgs : RoutedEventArgs
{
    public ZoneMutationKind Kind { get; }

    public ZoneMutationStartingEventArgs(RoutedEvent routedEvent, object source, ZoneMutationKind kind)
        : base(routedEvent, source)
    {
        Kind = kind;
    }
}

public delegate void ZoneMutationStartingEventHandler(object sender, ZoneMutationStartingEventArgs e);
