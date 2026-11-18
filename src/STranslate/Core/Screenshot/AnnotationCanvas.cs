using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace STranslate.Core;

/// <summary>
/// 截图编辑器交互画布：冻结背景 + 遮罩 + 标注 + 预览 + 放大镜 五层视觉。
/// 一个编辑器窗口（对应一块显示器）持有一份本类的实例，多个实例通过共享的
/// ScreenshotEditorSession 协同，坐标系为虚拟桌面物理像素。
/// </summary>
public sealed class AnnotationCanvas : FrameworkElement
{
    // ===== 常量 =====
    private const double DimOpacity = 80.0 / 255.0;
    private const double HandleRadiusPx = 5.5;
    private const double HitTolerancePx = 8;
    private const double MinSelectionSize = 8;
    private const double MagnifierSizeDip = 120;
    private const double MagnifierZoom = 6;

    private readonly DrawingVisual _backgroundVisual = new();
    private readonly DrawingVisual _maskVisual = new();
    private readonly DrawingVisual _annotationVisual = new();
    private readonly DrawingVisual _previewVisual = new();
    private readonly DrawingVisual _magnifierVisual = new();

    // 热路径静态冻结资源（每帧渲染不再重建）
    private static readonly Brush DimBrushCache = Frozen(Color.FromArgb(80, 0, 0, 0));
    private static readonly Pen WhitePenCache = FrozenPen(Brushes.White, 1.0);
    private static readonly Pen BlackDashPenCache = FrozenPen(Brushes.Black, 1.0, dashed: true);
    private static readonly Brush AccentBrushCache = Frozen(Color.FromRgb(0, 120, 215));
    private static readonly Brush AccentLightBrushCache = Frozen(Color.FromRgb(0, 160, 255));
    private static readonly Pen AccentPenCache = FrozenPen(AccentBrushCache, 1.5);
    private static readonly Pen AccentThickPenCache = FrozenPen(AccentLightBrushCache, 2.5);
    private static readonly Pen HandlePenCache = FrozenPen(Brushes.Black, 1);
    private static readonly Pen HoverPenCache = FrozenPen(AccentLightBrushCache, 1.2, dashed: true);

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Pen FrozenPen(Brush brush, double width, bool dashed = false)
    {
        var p = new Pen(brush, width);
        if (dashed)
            p.DashStyle = DashStyles.Dash;
        p.Freeze();
        return p;
    }

    private enum Interaction
    {
        None,
        CreatingSelection,
        MovingSelection,
        ResizingSelection,
        DrawingAnnotation,
        Erasing,
        MovingAnnotation,
        ResizingAnnotation,
        RotatingAnnotation,
    }

    private enum SelectionHandle
    {
        None, TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left
    }

    private Interaction _interaction = Interaction.None;
    private SelectionHandle _activeHandle = SelectionHandle.None;
    private Point _pressPoint;
    private Point _lastPoint;
    private Rect _dragStartSelection;
    private Rect _annotationStartBounds;

    private readonly List<Point> _strokePoints = [];
    private FreehandAnnotation? _previewStroke;
    private Rect _previewShapeRect;
    private Point _previewArrowFrom;
    private bool _hasPreview;

    private Rect? _hoverCandidate;
    private ScreenshotAnnotation? _hoveredAnnotation;

    private bool _magnifierVisible;

    // ===== 宿主窗口信息 =====
    /// <summary>本画布对应显示器在虚拟桌面中的物理原点</summary>
    public Point MonitorOrigin { get; set; }

    /// <summary>本画布对应显示器的 DPI 缩放</summary>
    public double Scale { get; set; } = 1.0;

    public ScreenshotEditorSession Session { get; }

    /// <summary>窗口创建后设置视口（显示器物理原点 + DPI 缩放）并刷新变换</summary>
    public void ApplyViewport(Point origin, double scale)
    {
        MonitorOrigin = origin;
        Scale = scale;
        UpdateVisualTransforms();
        if (IsLoaded)
            RenderAll();
    }

    /// <summary>删除悬停图元，无悬停则清空全部标注（均入撤销栈）</summary>
    public void DeleteHoveredOrClear()
    {
        if (_hoveredAnnotation is { } annotation)
        {
            Session.RemoveAnnotations([annotation]);
            _hoveredAnnotation = null;
        }
        else
        {
            Session.ClearAnnotations();
        }
        RenderAnnotations();
    }

    // ===== 事件（宿主窗口处理）=====
    public event Action<Point>? BeginTextEditRequested;
    public event Action<Point>? ContextMenuRequested;
    public event Action? SelectionStabilized;
    public event Action? InteractionStarted;
    public event Action? InteractionEnded;
    public event Action? DefaultActionRequested;

    public AnnotationCanvas(ScreenshotEditorSession session)
    {
        Session = session;
        ClipToBounds = true;
        Focusable = true;

        RenderOptions.SetBitmapScalingMode(_backgroundVisual, BitmapScalingMode.NearestNeighbor);
        RenderOptions.SetBitmapScalingMode(_magnifierVisual, BitmapScalingMode.NearestNeighbor);

        AddVisualChild(_backgroundVisual);
        AddVisualChild(_maskVisual);
        AddVisualChild(_annotationVisual);
        AddVisualChild(_previewVisual);
        AddVisualChild(_magnifierVisual);
        UpdateVisualTransforms();

        session.RepaintNeeded += () => Dispatcher.Invoke(InvalidateAll);
        session.SelectionChanged += () => Dispatcher.Invoke(InvalidateAll);
        session.ToolChanged += OnToolChanged;
        session.UndoChanged += () => Dispatcher.Invoke(InvalidateAll);

        Loaded += (_, _) => RenderAll();
    }

    private void OnToolChanged()
    {
        _hoveredAnnotation = null;
        Cursor = Session.ActiveTool switch
        {
            ScreenshotTool.Pen or ScreenshotTool.Highlighter => Cursors.Pen,
            ScreenshotTool.Text => Cursors.IBeam,
            ScreenshotTool.Eraser => Cursors.None,
            _ => Cursors.Arrow
        };
        InvalidateAll();
    }

    private void UpdateVisualTransforms()
    {
        var matrix = new Matrix(1.0 / Scale, 0, 0, 1.0 / Scale, -MonitorOrigin.X / Scale, -MonitorOrigin.Y / Scale);
        foreach (var visual in new DrawingVisual[] { _backgroundVisual, _maskVisual, _annotationVisual, _previewVisual, _magnifierVisual })
            visual.Transform = new MatrixTransform(matrix);
    }

    // ===== 坐标换算 =====
    public Point DipToPhysical(Point p) => new(p.X * Scale + MonitorOrigin.X, p.Y * Scale + MonitorOrigin.Y);
    public Point PhysicalToDip(Point p) => new((p.X - MonitorOrigin.X) / Scale, (p.Y - MonitorOrigin.Y) / Scale);

    // ===== 渲染 =====
    private void RenderAll()
    {
        RenderBackground();
        RenderMask();
        RenderAnnotations();
        RenderPreview();
        RenderMagnifier();
    }

    private void InvalidateAll()
    {
        RenderMask();
        RenderAnnotations();
        RenderPreview();
        // 背景与放大镜独立管理
    }

    private void RenderBackground()
    {
        using var dc = _backgroundVisual.RenderOpen();
        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, ActualWidth, ActualHeight));
        dc.DrawImage(Session.FrozenSource, Session.VirtualBounds);
    }

    private void RenderMask()
    {
        using var dc = _maskVisual.RenderOpen();
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        var dimBrush = DimBrushCache;

        if (Session.Selection is not { } selection || selection.Width < 1 || selection.Height < 1)
        {
            // 无选区：整体变暗 + 智能候选高亮
            dc.DrawRectangle(dimBrush, null, bounds);
            if (_hoverCandidate is { } candidate)
                DrawHighlight(dc, candidate);
            return;
        }

        // 选区外四块遮罩
        var top = new Rect(0, 0, ActualWidth, Math.Max(0, PhysicalToDip(new Point(selection.Left, selection.Top)).Y));
        var selTopRight = PhysicalToDip(new Point(selection.Right, selection.Top));
        var selBottomLeft = PhysicalToDip(new Point(selection.Left, selection.Bottom));
        var selBottomRight = PhysicalToDip(new Point(selection.Right, selection.Bottom));
        var left = new Rect(0, top.Bottom, Math.Max(0, selTopRight.X), selBottomRight.Y - top.Bottom);
        var right = new Rect(selBottomRight.X, top.Bottom, Math.Max(0, ActualWidth - selBottomRight.X), selBottomRight.Y - top.Bottom);
        var bottom = new Rect(0, selBottomRight.Y, ActualWidth, Math.Max(0, ActualHeight - selBottomRight.Y));
        dc.DrawRectangle(dimBrush, null, top);
        dc.DrawRectangle(dimBrush, null, left);
        dc.DrawRectangle(dimBrush, null, right);
        dc.DrawRectangle(dimBrush, null, bottom);

        // 选区边框（双描边保证任意背景下可见）
        var selDip = new Rect(PhysicalToDip(selection.Location), new Size(selection.Width / Scale, selection.Height / Scale));
        dc.DrawRectangle(null, WhitePenCache, selDip);
        dc.DrawRectangle(null, BlackDashPenCache, selDip.Inflated(1 / Scale));

        // 尺寸标签
        DrawSizeLabel(dc, selDip, (int)selection.Width, (int)selection.Height);

        // 八向手柄
        DrawHandles(dc, selDip, false);
    }

    private void DrawHighlight(DrawingContext dc, Rect physicalRect)
    {
        var dip = new Rect(PhysicalToDip(physicalRect.Location), new Size(physicalRect.Width / Scale, physicalRect.Height / Scale));
        dc.DrawRectangle(null, AccentPenCache, dip);
        // 四角强调
        var len = Math.Min(16.0, Math.Min(dip.Width, dip.Height) / 3);
        var thick = AccentThickPenCache;
        foreach (var (cx, cy, dx, dy) in new[]
                 {
                     (dip.Left, dip.Top, 1.0, 1.0), (dip.Right, dip.Top, -1.0, 1.0),
                     (dip.Right, dip.Bottom, -1.0, -1.0), (dip.Left, dip.Bottom, 1.0, -1.0)
                 })
        {
            dc.DrawLine(thick, new Point(cx, cy), new Point(cx + dx * len, cy));
            dc.DrawLine(thick, new Point(cx, cy), new Point(cx, cy + dy * len));
        }
    }

    private void DrawSizeLabel(DrawingContext dc, Rect selDip, int w, int h)
    {
        var text = $"{w} × {h}";
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            12,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var pad = 6;
        var bgWidth = ft.Width + pad * 2;
        var bgHeight = ft.Height + 4;
        var x = selDip.Left;
        var y = selDip.Top - bgHeight - 6;
        if (y < 2) y = selDip.Bottom + 6; // 上方放不下放下方
        var bg = new Rect(x, y, bgWidth, bgHeight);
        var bgBrush = new SolidColorBrush(Color.FromArgb(220, 30, 30, 30));
        bgBrush.Freeze();
        var geo = new RectangleGeometry(bg, 4, 4);
        geo.Freeze();
        dc.DrawGeometry(bgBrush, null, geo);
        dc.DrawText(ft, new Point(x + pad, y + 2));
    }

    private void DrawHandles(DrawingContext dc, Rect selDip, bool accent)
    {
        var fill = accent ? AccentBrushCache : Brushes.White;
        var pen = HandlePenCache;
        foreach (var p in GetHandlePoints(selDip))
        {
            var r = HandleRadiusPx;
            dc.DrawEllipse(fill, pen, p, r, r);
        }
    }

    private static IEnumerable<Point> GetHandlePoints(Rect rect)
    {
        yield return new Point(rect.Left, rect.Top);
        yield return new Point(rect.Left + rect.Width / 2, rect.Top);
        yield return new Point(rect.Right, rect.Top);
        yield return new Point(rect.Right, rect.Top + rect.Height / 2);
        yield return new Point(rect.Right, rect.Bottom);
        yield return new Point(rect.Left + rect.Width / 2, rect.Bottom);
        yield return new Point(rect.Left, rect.Bottom);
        yield return new Point(rect.Left, rect.Top + rect.Height / 2);
    }

    private void RenderAnnotations()
    {
        using var dc = _annotationVisual.RenderOpen();
        var context = new ScreenshotRenderContext { Background = Session.FrozenSource, BackgroundBounds = Session.VirtualBounds };
        foreach (var annotation in Session.Annotations)
            annotation.Render(dc, context);

        if (_hoveredAnnotation is { } hovered && Session.ActiveTool == ScreenshotTool.Select)
        {
            var bounds = hovered.Bounds;
            var dip = new Rect(PhysicalToDip(bounds.Location), new Size(bounds.Width / Scale, bounds.Height / Scale));
            dc.DrawRectangle(null, HoverPenCache, dip);
        }
    }

    private void RenderPreview()
    {
        using var dc = _previewVisual.RenderOpen();
        var renderContext = new ScreenshotRenderContext { Background = Session.FrozenSource, BackgroundBounds = Session.VirtualBounds };
        if (_interaction == Interaction.DrawingAnnotation)
        {
            if (_previewStroke != null)
            {
                _previewStroke.Render(dc, renderContext);
            }
            else if (_hasPreview)
            {
                var style = Session.GetStyle(Session.ActiveTool);
                var color = (Color)ColorConverter.ConvertFromString(style.ColorHex);
                var rect = Normalize(_previewShapeRect);
                switch (Session.ActiveTool)
                {
                    case ScreenshotTool.Rectangle:
                        new ShapeAnnotation(rect, color, style.StrokeWidth, false, style.Fill).Render(dc, renderContext);
                        break;
                    case ScreenshotTool.Ellipse:
                        new ShapeAnnotation(rect, color, style.StrokeWidth, true, style.Fill).Render(dc, renderContext);
                        break;
                    case ScreenshotTool.Arrow:
                        new ArrowAnnotation(_previewArrowFrom, _lastPoint, color, style.StrokeWidth).Render(dc, renderContext);
                        break;
                    case ScreenshotTool.Mosaic:
                        new MosaicAnnotation(rect, style.StrokeWidth).Render(dc, renderContext);
                        // 外框提示
                        var dashed = new Pen(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), 1) { DashStyle = DashStyles.Dash };
                        dashed.Freeze();
                        dc.DrawRectangle(null, dashed, rect);
                        break;
                }
            }
        }

        // 橡皮光圈
        if (Session.ActiveTool == ScreenshotTool.Eraser && IsMouseOver)
        {
            var size = Session.GetStyle(ScreenshotTool.Eraser).EraserSize;
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), 1.5);
            pen.Freeze();
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), pen, _lastPoint, size / 2, size / 2);
        }

        // 悬停图元的编辑手柄 + 旋转手柄
        if (_hoveredAnnotation is { } hovered && Session.ActiveTool == ScreenshotTool.Select && _interaction is Interaction.None or Interaction.MovingAnnotation or Interaction.RotatingAnnotation)
        {
            var bounds = hovered.Bounds;
            var dip = new Rect(PhysicalToDip(bounds.Location), new Size(bounds.Width / Scale, bounds.Height / Scale));
            var fill = new SolidColorBrush(Color.FromRgb(0, 120, 215));
            fill.Freeze();
            var pen = new Pen(Brushes.White, 1);
            pen.Freeze();
            foreach (var p in GetHandlePoints(dip))
                dc.DrawEllipse(fill, pen, p, 4, 4);

            // 旋转手柄：顶部中央上方 22px，带连接线
            var rotateHandle = GetRotateHandlePoint(hovered);
            var rotateDip = PhysicalToDip(rotateHandle);
            var topCenterDip = new Point(dip.Left + dip.Width / 2, dip.Top);
            var linePen = new Pen(new SolidColorBrush(Color.FromArgb(180, 0, 160, 255)), 1);
            linePen.Freeze();
            dc.DrawLine(linePen, topCenterDip, new Point(rotateDip.X, rotateDip.Y + 6 / Scale * Scale));
            dc.DrawEllipse(fill, pen, rotateDip, 5, 5);
            var rp = new FormattedText("↻",
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                8,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(rp, new Point(rotateDip.X - rp.Width / 2, rotateDip.Y - rp.Height / 2));
        }
    }

    /// <summary>旋转手柄物理坐标（顶部中央上方 22px）</summary>
    private static Point GetRotateHandlePoint(ScreenshotAnnotation annotation)
    {
        var b = annotation.Bounds;
        return new Point(b.X + b.Width / 2, b.Y - 22);
    }

    private void RenderMagnifier()
    {
        using var dc = _magnifierVisual.RenderOpen();
        if (!_magnifierVisible || !Session.Options.ShowMagnifier) return;

        var cursor = _lastPoint;
        var srcSizePx = MagnifierSizeDip * Scale / MagnifierZoom;

        // 位置：光标右下偏移，越界翻转（dip）
        var offsetDip = 24;
        var local = PhysicalToDip(cursor);
        var destDip = new Size(MagnifierSizeDip, MagnifierSizeDip);
        var x = local.X + offsetDip;
        var y = local.Y + offsetDip;
        if (x + destDip.Width > ActualWidth) x = local.X - offsetDip - destDip.Width;
        if (y + destDip.Height + 26 > ActualHeight) y = local.Y - offsetDip - destDip.Height - 26;

        // 背景 + 图像（用变换把光标附近物理像素放大到目标区）
        var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 120, 215)), 1.5);
        borderPen.Freeze();
        var bgGeo = new RectangleGeometry(new Rect(new Point(x, y), destDip), 6, 6);
        bgGeo.Freeze();
        dc.DrawGeometry(Brushes.Black, borderPen, bgGeo);
        dc.PushClip(bgGeo);
        {
            // 绘制空间为物理像素，最终经基变换 B(p)=(p-origin)/Scale 落到 dip。
            // 目标：源区 S（物理）映射到目标区 D（dip），即 B(m(S)) = D，斜率 k=dip/px。
            // 解得 m: 缩放 s=k*Scale，平移 origin + Scale*D.Location - s*S.Location
            var srcLocation = new Point(cursor.X - srcSizePx / 2, cursor.Y - srcSizePx / 2);
            var k = destDip.Width / srcSizePx; // dip per physical px
            var s = k * Scale;
            var m = new Matrix();
            m.Scale(s, s);
            m.Translate(MonitorOrigin.X + Scale * x - srcLocation.X * s,
                        MonitorOrigin.Y + Scale * y - srcLocation.Y * s);
            dc.PushTransform(new MatrixTransform(m));
            dc.DrawImage(Session.FrozenSource, Session.VirtualBounds);
            dc.Pop();
        }
        dc.Pop();

        // 十字准星
        var crossPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 80, 80)), 1);
        crossPen.Freeze();
        var cx = x + destDip.Width / 2;
        var cy = y + destDip.Height / 2;
        dc.DrawLine(crossPen, new Point(cx - 6, cy), new Point(cx + 6, cy));
        dc.DrawLine(crossPen, new Point(cx, cy - 6), new Point(cx, cy + 6));

        // 颜色标签
        var color = Session.GetPixelAt(cursor);
        var text = $"R:{color.R} G:{color.G} B:{color.B}  #{color.R:X2}{color.G:X2}{color.B:X2}";
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            11,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var labelBg = new Rect(x, y + destDip.Height + 2, ft.Width + 12, ft.Height + 4);
        var labelGeo = new RectangleGeometry(labelBg, 4, 4);
        labelGeo.Freeze();
        var bgBrush = new SolidColorBrush(Color.FromArgb(220, 30, 30, 30));
        bgBrush.Freeze();
        dc.DrawGeometry(bgBrush, null, labelGeo);
        dc.DrawText(ft, new Point(x + 6, y + destDip.Height + 4));
    }

    private static Rect Normalize(Rect rect) => new(
        Math.Min(rect.Left, rect.Right), Math.Min(rect.Top, rect.Bottom),
        Math.Abs(rect.Width), Math.Abs(rect.Height));

    // ===== 视觉树 =====
    protected override int VisualChildrenCount => 5;
    protected override Visual GetVisualChild(int index) => index switch
    {
        0 => _backgroundVisual,
        1 => _maskVisual,
        2 => _annotationVisual,
        3 => _previewVisual,
        4 => _magnifierVisual,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        RenderAll();
    }

    // ===== 输入状态机 =====
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        // 双击 = 默认动作（复制）
        if (e.ClickCount == 2 && Session.Selection is { Width: >= MinSelectionSize })
        {
            DefaultActionRequested?.Invoke();
            e.Handled = true;
            return;
        }

        var pos = e.GetPosition(this);
        var physical = DipToPhysical(pos);
        _lastPoint = physical;
        Focus();

        // Ctrl+点击 = 复制放大镜颜色
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Session.Selection == null)
        {
            CopyMagnifierColor(physical);
            return;
        }

        var tool = Session.ActiveTool;

        if (Session.Selection is not { } selection)
        {
            // 无选区：开始拖拽新选区
            _interaction = Interaction.CreatingSelection;
            _pressPoint = physical;
            _hasPreview = true;
            _previewShapeRect = new Rect(physical, physical);
            CaptureMouse();
            InteractionStarted?.Invoke();
            RenderMask();
            return;
        }

        // 有选区
        if (tool == ScreenshotTool.Eraser)
        {
            _interaction = Interaction.Erasing;
            _pressPoint = physical;
            CaptureMouse();
            EraseAt(physical);
            return;
        }

        if (tool == ScreenshotTool.Text)
        {
            if (selection.Contains(physical))
                BeginTextEditRequested?.Invoke(physical);
            return;
        }

        if (tool == ScreenshotTool.Number)
        {
            if (selection.Contains(physical))
            {
                var style = Session.GetStyle(ScreenshotTool.Number);
                Session.AddAnnotation(new NumberAnnotation(physical, Session.NextNumber(),
                    (Color)ColorConverter.ConvertFromString(style.ColorHex), style.FontSize));
            }
            return;
        }

        if (tool == ScreenshotTool.Cursor)
        {
            if (selection.Contains(physical))
            {
                var style = Session.GetStyle(ScreenshotTool.Cursor);
                Session.AddAnnotation(new CursorAnnotation(physical,
                    (Color)ColorConverter.ConvertFromString(style.ColorHex)) { Size = style.StrokeWidth * 2.5 });
            }
            return;
        }

        if (tool is ScreenshotTool.Pen or ScreenshotTool.Highlighter)
        {
            if (!selection.Contains(physical)) return;
            _interaction = Interaction.DrawingAnnotation;
            _strokePoints.Clear();
            _strokePoints.Add(physical);
            var style = Session.GetStyle(tool);
            _previewStroke = new FreehandAnnotation(_strokePoints,
                (Color)ColorConverter.ConvertFromString(style.ColorHex), style.StrokeWidth,
                tool == ScreenshotTool.Highlighter);
            CaptureMouse();
            RenderPreview();
            return;
        }

        if (tool is ScreenshotTool.Rectangle or ScreenshotTool.Ellipse or ScreenshotTool.Arrow or ScreenshotTool.Mosaic)
        {
            if (!selection.Contains(physical)) return;
            _interaction = Interaction.DrawingAnnotation;
            _pressPoint = physical;
            _previewArrowFrom = physical;
            _previewShapeRect = new Rect(physical, physical);
            _hasPreview = true;
            CaptureMouse();
            RenderPreview();
            return;
        }

        // Select 工具：图元编辑优先
        // 旋转手柄位于包围盒外，需独立命中测试（自顶向下取第一个）
        if (Session.ActiveTool == ScreenshotTool.Select)
        {
            for (var i = Session.Annotations.Count - 1; i >= 0; i--)
            {
                var candidate = Session.Annotations[i];
                var hp = GetRotateHandlePoint(candidate);
                if (Math.Abs(physical.X - hp.X) <= 12 && Math.Abs(physical.Y - hp.Y) <= 12)
                {
                    _interaction = Interaction.RotatingAnnotation;
                    _hoveredAnnotation = candidate;
                    _rotationStartAngle = candidate.Rotation;
                    _pressPoint = physical;
                    CaptureMouse();
                    InteractionStarted?.Invoke();
                    return;
                }
            }
        }

        var hovered = Session.HitTestAnnotation(physical, HitTolerancePx);
        if (hovered != null)
        {
            var handle = HitTestAnnotationHandle(hovered, physical);
            if (handle != SelectionHandle.None)
            {
                _interaction = Interaction.ResizingAnnotation;
                _hoveredAnnotation = hovered;
                _activeHandle = handle;
                _annotationStartBounds = hovered.Bounds;
                _pressPoint = physical;
                _dragStartSelection = hovered.Bounds;
                CaptureMouse();
                InteractionStarted?.Invoke();
                return;
            }
            _interaction = Interaction.MovingAnnotation;
            _hoveredAnnotation = hovered;
            _pressPoint = physical;
            _lastPoint = physical;
            CaptureMouse();
            InteractionStarted?.Invoke();
            return;
        }

        // 选区手柄
        var selHandle = HitTestSelectionHandle(selection, physical);
        if (selHandle != SelectionHandle.None)
        {
            _interaction = Interaction.ResizingSelection;
            _activeHandle = selHandle;
            _dragStartSelection = selection;
            _pressPoint = physical;
            CaptureMouse();
            InteractionStarted?.Invoke();
            return;
        }

        // 选区内移动
        if (selection.Contains(physical))
        {
            _interaction = Interaction.MovingSelection;
            _pressPoint = physical;
            _dragStartSelection = selection;
            CaptureMouse();
            InteractionStarted?.Invoke();
            return;
        }

        // 选区外：重新框选
        _interaction = Interaction.CreatingSelection;
        _pressPoint = physical;
        _hasPreview = true;
        _previewShapeRect = new Rect(physical, physical);
        CaptureMouse();
        InteractionStarted?.Invoke();
        RenderMask();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pos = e.GetPosition(this);
        var physical = DipToPhysical(pos);
        _lastPoint = physical;

        switch (_interaction)
        {
            case Interaction.None:
                HandleHover(physical);
                break;

            case Interaction.CreatingSelection:
            {
                var rect = Normalize(new Rect(_pressPoint, physical));
                Session.Selection = ClampToVirtual(rect);
                _magnifierVisible = true;
                RenderMagnifier();
                break;
            }

            case Interaction.MovingSelection:
            {
                var dx = physical.X - _pressPoint.X;
                var dy = physical.Y - _pressPoint.Y;
                var sel = _dragStartSelection;
                sel.Offset(dx, dy);
                Session.Selection = ClampToVirtual(sel);
                break;
            }

            case Interaction.ResizingSelection:
                Session.Selection = ResizeRect(_dragStartSelection, _activeHandle, physical);
                break;

            case Interaction.DrawingAnnotation:
                if (_previewStroke != null)
                {
                    var last = _strokePoints[^1];
                    if (Math.Abs(physical.X - last.X) + Math.Abs(physical.Y - last.Y) >= 1.5)
                        _strokePoints.Add(physical);
                    _previewStroke.ReplacePoints(_strokePoints);
                    RenderPreview();
                }
                else
                {
                    _previewShapeRect = new Rect(_pressPoint, physical);
                    RenderPreview();
                }
                break;

            case Interaction.Erasing:
                EraseAt(physical);
                RenderPreview();
                break;

            case Interaction.MovingAnnotation:
            {
                if (_hoveredAnnotation is { } annotation)
                {
                    var dx = physical.X - _lastPoint.X;
                    var dy = physical.Y - _lastPoint.Y;
                    annotation.Offset(dx, dy);
                    RenderAnnotations();
                }
                break;
            }

            case Interaction.ResizingAnnotation:
            {
                if (_hoveredAnnotation is { } annotation)
                {
                    var newBounds = ResizeRect(_annotationStartBounds, _activeHandle, physical);
                    annotation.Resize(newBounds);
                    RenderAnnotations();
                    RenderPreview();
                }
                break;
            }

            case Interaction.RotatingAnnotation:
            {
                if (_hoveredAnnotation is { } annotation)
                {
                    var b = annotation.Bounds;
                    var cx = b.X + b.Width / 2;
                    var cy = b.Y + b.Height / 2;
                    var angle = Math.Atan2(physical.Y - cy, physical.X - cx) * 180.0 / Math.PI + 90;
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                        angle = Math.Round(angle / 15.0) * 15;
                    annotation.Rotation = angle;
                    RenderAnnotations();
                    RenderPreview();
                }
                break;
            }
        }

        if (_interaction == Interaction.None && Session.Selection == null && Session.Options.ShowMagnifier)
        {
            _magnifierVisible = true;
            RenderMagnifier();
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        var physical = DipToPhysical(e.GetPosition(this));

        switch (_interaction)
        {
            case Interaction.CreatingSelection:
            {
                var rect = Normalize(new Rect(_pressPoint, physical));
                // 近似点击：尝试吸附智能窗口
                if (rect.Width < MinSelectionSize || rect.Height < MinSelectionSize)
                {
                    var candidate = Session.Options.SmartSelection ? Session.WindowFinder.HitTest(physical) : null;
                    if (candidate is { } c)
                        rect = c;
                }
                Session.Selection = ClampToVirtual(rect);
                _magnifierVisible = false;
                if (Session.Selection is { Width: >= MinSelectionSize, Height: >= MinSelectionSize })
                    SelectionStabilized?.Invoke();
                break;
            }

            case Interaction.MovingSelection:
            case Interaction.ResizingSelection:
                _magnifierVisible = false;
                SelectionStabilized?.Invoke();
                break;

            case Interaction.DrawingAnnotation:
                CommitDrawingAnnotation();
                break;

            case Interaction.MovingAnnotation:
            {
                if (_hoveredAnnotation is { } annotation)
                {
                    var before = _annotationStartBoundsForMove;
                    if (before is { } b && (Math.Abs(b.X - annotation.Bounds.X) > 0.5 || Math.Abs(b.Y - annotation.Bounds.Y) > 0.5))
                    {
                        var after = annotation.Bounds;
                        Session.Undo.Push("move",
                            () => { annotation.Offset(b.X - after.X, b.Y - after.Y); Session.RequestRepaint(); },
                            () => { annotation.Offset(after.X - b.X, after.Y - b.Y); Session.RequestRepaint(); });
                    }
                }
                break;
            }

            case Interaction.ResizingAnnotation:
            {
                if (_hoveredAnnotation is { } annotation)
                {
                    var before = _annotationStartBounds;
                    var after = annotation.Bounds;
                    if (Math.Abs(before.Width - after.Width) > 0.5 || Math.Abs(before.Height - after.Height) > 0.5 ||
                        Math.Abs(before.X - after.X) > 0.5 || Math.Abs(before.Y - after.Y) > 0.5)
                    {
                        var beforeSnapshot = before;
                        Session.Undo.Push("resize",
                            () => { annotation.Resize(beforeSnapshot); Session.RequestRepaint(); },
                            () => { annotation.Resize(after); Session.RequestRepaint(); });
                    }
                }
                break;
            }

            case Interaction.RotatingAnnotation:
            {
                if (_hoveredAnnotation is { } annotation)
                {
                    var beforeAngle = _rotationStartAngle;
                    var afterAngle = annotation.Rotation;
                    if (Math.Abs(beforeAngle - afterAngle) > 0.5)
                    {
                        Session.Undo.Push("rotate",
                            () => { annotation.Rotation = beforeAngle; Session.RequestRepaint(); },
                            () => { annotation.Rotation = afterAngle; Session.RequestRepaint(); });
                    }
                }
                break;
            }
        }

        _interaction = Interaction.None;
        _activeHandle = SelectionHandle.None;
        _hasPreview = false;
        _previewStroke = null;
        ReleaseMouseCapture();
        InvalidateAll();
        InteractionEnded?.Invoke();
    }

    private Rect? _annotationStartBoundsForMove;
    private double _rotationStartAngle;

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _magnifierVisible = false;
        _hoverCandidate = null;
        RenderMagnifier();
        RenderMask();
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        if (Session.Selection is { Width: >= MinSelectionSize })
            ContextMenuRequested?.Invoke(DipToPhysical(e.GetPosition(this)));
        else
            Session.Cancel();
    }

    // ===== 键盘 =====
    public void NudgeSelection(double dx, double dy)
    {
        if (Session.Selection is not { } sel) return;
        sel.Offset(dx, dy);
        Session.Selection = ClampToVirtual(sel);
    }

    // ===== 内部 =====
    private void EraseAt(Point physical)
    {
        var size = Session.GetStyle(ScreenshotTool.Eraser).EraserSize;
        var eraserRect = new Rect(physical.X - size / 2, physical.Y - size / 2, size, size);
        var hits = Session.Annotations.Where(a => a.Intersects(eraserRect)).ToList();
        if (hits.Count > 0)
            Session.RemoveAnnotations(hits);
    }

    private void CommitDrawingAnnotation()
    {
        var tool = Session.ActiveTool;
        var style = Session.GetStyle(tool);
        var color = (Color)ColorConverter.ConvertFromString(style.ColorHex);

        switch (tool)
        {
            case ScreenshotTool.Pen or ScreenshotTool.Highlighter when _strokePoints.Count >= 1:
                Session.AddAnnotation(new FreehandAnnotation(_strokePoints.ToList(), color, style.StrokeWidth, tool == ScreenshotTool.Highlighter));
                _strokePoints.Clear();
                break;

            case ScreenshotTool.Rectangle:
            {
                var rect = Normalize(_previewShapeRect);
                if (rect.Width >= 4 && rect.Height >= 4)
                    Session.AddAnnotation(new ShapeAnnotation(rect, color, style.StrokeWidth, false, style.Fill));
                break;
            }

            case ScreenshotTool.Ellipse:
            {
                var rect = Normalize(_previewShapeRect);
                if (rect.Width >= 4 && rect.Height >= 4)
                    Session.AddAnnotation(new ShapeAnnotation(rect, color, style.StrokeWidth, true, style.Fill));
                break;
            }

            case ScreenshotTool.Mosaic:
            {
                var rect = Normalize(_previewShapeRect);
                if (rect.Width >= 6 && rect.Height >= 6)
                    Session.AddAnnotation(new MosaicAnnotation(rect, style.StrokeWidth));
                break;
            }

            case ScreenshotTool.Arrow:
            {
                var dist = Math.Sqrt(Math.Pow(_previewArrowFrom.X - _lastPoint.X, 2) + Math.Pow(_previewArrowFrom.Y - _lastPoint.Y, 2));
                if (dist >= 6)
                    Session.AddAnnotation(new ArrowAnnotation(_previewArrowFrom, _lastPoint, color, style.StrokeWidth));
                break;
            }
        }
    }

    private void HandleHover(Point physical)
    {
        // 智能选区候选
        Rect? candidate = null;
        if (Session.Options.SmartSelection && Session.Selection == null)
            candidate = Session.WindowFinder.HitTest(physical);
        if (!RectEquals(candidate, _hoverCandidate))
        {
            _hoverCandidate = candidate;
            RenderMask();
        }

        // 图元悬停（含旋转手柄区域，位于包围盒外）
        ScreenshotAnnotation? hovered = null;
        if (Session.ActiveTool == ScreenshotTool.Select && Session.Selection != null)
        {
            hovered = Session.HitTestAnnotation(physical, HitTolerancePx);
            if (hovered == null)
            {
                for (var i = Session.Annotations.Count - 1; i >= 0; i--)
                {
                    var hp = GetRotateHandlePoint(Session.Annotations[i]);
                    if (Math.Abs(physical.X - hp.X) <= 12 && Math.Abs(physical.Y - hp.Y) <= 12)
                    {
                        hovered = Session.Annotations[i];
                        break;
                    }
                }
            }
        }
        if (!ReferenceEquals(hovered, _hoveredAnnotation))
        {
            _hoveredAnnotation = hovered;
            _annotationStartBoundsForMove = hovered?.Bounds;
            RenderAnnotations();
            RenderPreview();
        }
        else if (hovered != null)
        {
            _annotationStartBoundsForMove = hovered.Bounds;
        }
    }

    private static bool RectEquals(Rect? a, Rect? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return Math.Abs(a.Value.X - b.Value.X) < 0.5 && Math.Abs(a.Value.Y - b.Value.Y) < 0.5 &&
               Math.Abs(a.Value.Width - b.Value.Width) < 0.5 && Math.Abs(a.Value.Height - b.Value.Height) < 0.5;
    }

    private SelectionHandle HitTestSelectionHandle(Rect selection, Point p)
    {
        var dip = new Rect(PhysicalToDip(selection.Location), new Size(selection.Width / Scale, selection.Height / Scale));
        var local = PhysicalToDip(p);
        var tolerance = HandleRadiusPx + 4;
        return HitTestHandleCore(dip, local, tolerance);
    }

    private SelectionHandle HitTestAnnotationHandle(ScreenshotAnnotation annotation, Point p)
    {
        var bounds = annotation.Bounds;
        var dip = new Rect(PhysicalToDip(bounds.Location), new Size(bounds.Width / Scale, bounds.Height / Scale));
        var local = PhysicalToDip(p);
        return HitTestHandleCore(dip, local, 8);
    }

    private static SelectionHandle HitTestHandleCore(Rect dip, Point local, double tolerance)
    {
        var pts = GetHandlePoints(dip).ToArray();
        var names = new[]
        {
            SelectionHandle.TopLeft, SelectionHandle.Top, SelectionHandle.TopRight, SelectionHandle.Right,
            SelectionHandle.BottomRight, SelectionHandle.Bottom, SelectionHandle.BottomLeft, SelectionHandle.Left
        };
        for (var i = 0; i < pts.Length; i++)
        {
            if (Math.Abs(local.X - pts[i].X) <= tolerance && Math.Abs(local.Y - pts[i].Y) <= tolerance)
                return names[i];
        }
        return SelectionHandle.None;
    }

    private Rect ResizeRect(Rect start, SelectionHandle handle, Point current)
    {
        var flags = _flagMap.GetValueOrDefault(handle);
        var left = start.Left;
        var top = start.Top;
        var right = start.Right;
        var bottom = start.Bottom;

        if (flags.HasFlag(SelectionHandleFlag.Left)) left = current.X;
        if (flags.HasFlag(SelectionHandleFlag.Top)) top = current.Y;
        if (flags.HasFlag(SelectionHandleFlag.Right)) right = current.X;
        if (flags.HasFlag(SelectionHandleFlag.Bottom)) bottom = current.Y;

        var rect = new Rect(Math.Min(left, right), Math.Min(top, bottom),
            Math.Abs(right - left), Math.Abs(bottom - top));
        if (rect.Width < MinSelectionSize || rect.Height < MinSelectionSize)
        {
            // 保持最小尺寸
            rect = new Rect(rect.Location,
                new Size(Math.Max(MinSelectionSize, rect.Width), Math.Max(MinSelectionSize, rect.Height)));
        }

        // 锁定纵横比：宽度主导，锚定拖动侧的对侧（v1 保持纵横比）
        if (Session.SelectionStyle.LockRatio && start.Width > MinSelectionSize && start.Height > MinSelectionSize)
        {
            var ratio = start.Width / start.Height;
            var newH = Math.Max(MinSelectionSize, rect.Width / ratio);
            var newW = newH * ratio;

            var anchorX = flags.HasFlag(SelectionHandleFlag.Left) ? start.Right : start.Left;
            var anchorY = flags.HasFlag(SelectionHandleFlag.Top) ? start.Bottom : start.Top;
            var x = flags.HasFlag(SelectionHandleFlag.Left) ? anchorX - newW : anchorX;
            var y = flags.HasFlag(SelectionHandleFlag.Top) ? anchorY - newH : anchorY;
            rect = new Rect(x, y, newW, newH);
        }
        return rect;
    }

    [Flags]
    private enum SelectionHandleFlag
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8,
    }

    private static readonly Dictionary<SelectionHandle, SelectionHandleFlag> _flagMap = new()
    {
        [SelectionHandle.TopLeft] = SelectionHandleFlag.Left | SelectionHandleFlag.Top,
        [SelectionHandle.Top] = SelectionHandleFlag.Top,
        [SelectionHandle.TopRight] = SelectionHandleFlag.Right | SelectionHandleFlag.Top,
        [SelectionHandle.Right] = SelectionHandleFlag.Right,
        [SelectionHandle.BottomRight] = SelectionHandleFlag.Right | SelectionHandleFlag.Bottom,
        [SelectionHandle.Bottom] = SelectionHandleFlag.Bottom,
        [SelectionHandle.BottomLeft] = SelectionHandleFlag.Left | SelectionHandleFlag.Bottom,
        [SelectionHandle.Left] = SelectionHandleFlag.Left,
    };

    private Rect ClampToVirtual(Rect rect)
    {
        var vb = Session.VirtualBounds;
        var x = Math.Clamp(rect.X, vb.Left, Math.Max(vb.Left, vb.Right - 1));
        var y = Math.Clamp(rect.Y, vb.Top, Math.Max(vb.Top, vb.Bottom - 1));
        var width = Math.Min(rect.Width, vb.Right - x);
        var height = Math.Min(rect.Height, vb.Bottom - y);
        return new Rect(x, y, Math.Max(0, width), Math.Max(0, height));
    }

    private void CopyMagnifierColor(Point physical)
    {
        var color = Session.GetPixelAt(physical);
        try
        {
            System.Windows.Clipboard.SetText($"#{color.R:X2}{color.G:X2}{color.B:X2}");
        }
        catch
        {
            // 剪贴板被占用时静默
        }
    }
}
