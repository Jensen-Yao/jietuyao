using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace STranslate.Core;

/// <summary>
/// 渲染上下文：马赛克等图元需要访问冻结的桌面背景
/// </summary>
public sealed class ScreenshotRenderContext
{
    public required System.Windows.Media.Imaging.BitmapSource Background { get; init; }

    /// <summary>背景绘制区域（物理像素，通常等于虚拟桌面范围）</summary>
    public required Rect BackgroundBounds { get; init; }
}

/// <summary>
/// 标注图元基类。所有坐标均为虚拟桌面物理像素。
/// 渲染走 WPF DrawingContext，导出时用 RenderTargetBitmap 保证所见即所得。
/// </summary>
public abstract class ScreenshotAnnotation
{
    /// <summary>旋转角度（度，绕未旋转包围盒中心）</summary>
    public double Rotation { get; set; }

    /// <summary>图元包围盒（物理像素，未含旋转）</summary>
    public abstract Rect Bounds { get; }

    /// <summary>渲染入口（应用旋转变换后转发 RenderCore）</summary>
    public void Render(DrawingContext dc, ScreenshotRenderContext context)
    {
        if (Math.Abs(Rotation) > 0.01)
        {
            var b = Bounds;
            dc.PushTransform(new RotateTransform(Rotation, b.X + b.Width / 2, b.Y + b.Height / 2));
            RenderCore(dc, context);
            dc.Pop();
        }
        else
        {
            RenderCore(dc, context);
        }
    }

    /// <summary>实际渲染（物理像素坐标系）</summary>
    protected abstract void RenderCore(DrawingContext dc, ScreenshotRenderContext context);

    /// <summary>命中测试</summary>
    public abstract bool HitTest(Point p, double tolerance);

    /// <summary>与矩形相交（橡皮擦用）</summary>
    public abstract bool Intersects(Rect rect);

    /// <summary>整体平移</summary>
    public abstract void Offset(double dx, double dy);

    /// <summary>缩放到新包围盒</summary>
    public abstract void Resize(Rect newBounds);
}

/// <summary>
/// 马赛克：将背景区域放大采样实现像素化遮盖（v2 新增）
/// </summary>
public sealed class MosaicAnnotation : ScreenshotAnnotation
{
    public MosaicAnnotation(Rect rect, double blockSize = 14)
    {
        Rect = rect;
        BlockSize = Math.Clamp(blockSize, 4, 64);
    }

    public Rect Rect { get; private set; }
    public double BlockSize { get; private set; }

    public override Rect Bounds => Rect;

    protected override void RenderCore(DrawingContext dc, ScreenshotRenderContext context)
    {
        // 取中心 1/k 子区域放大到整个矩形 → 像素块
        var f = Math.Max(2.0, BlockSize);
        var srcWidth = Math.Max(1.0, Rect.Width / f);
        var srcHeight = Math.Max(1.0, Rect.Height / f);
        var src = new Rect(
            Rect.X + (Rect.Width - srcWidth) / 2,
            Rect.Y + (Rect.Height - srcHeight) / 2,
            srcWidth, srcHeight);
        dc.PushClip(new RectangleGeometry(Rect));
        var m = new Matrix();
        m.Scale(f, f);
        m.Translate(Rect.X - src.X * f, Rect.Y - src.Y * f);
        dc.PushTransform(new MatrixTransform(m));
        dc.DrawImage(context.Background, context.BackgroundBounds);
        dc.Pop();
        dc.Pop();
    }

    public override bool HitTest(Point p, double tolerance) => Rect.Inflated(tolerance).Contains(p);

    public override bool Intersects(Rect rect) => Rect.Intersect(rect, Rect) is { IsEmpty: false };

    public override void Offset(double dx, double dy) => Rect = new Rect(Rect.X + dx, Rect.Y + dy, Rect.Width, Rect.Height);

    public override void Resize(Rect newBounds)
    {
        Rect = newBounds;
    }
}

/// <summary>自由手绘（画笔/荧光笔）</summary>
public sealed class FreehandAnnotation : ScreenshotAnnotation
{
    private readonly List<Point> _points = [];

    public FreehandAnnotation(IEnumerable<Point> points, Color color, double strokeWidth, bool highlighter = false)
    {
        _points.AddRange(points);
        Color = color;
        StrokeWidth = strokeWidth;
        Highlighter = highlighter;
        if (Highlighter)
            Color = Color.FromArgb(102, color.R, color.G, color.B);
    }

    public Color Color { get; }
    public double StrokeWidth { get; }
    public bool Highlighter { get; }
    public IReadOnlyList<Point> Points => _points;

    /// <summary>替换点集（绘制预览期间实时刷新用）</summary>
    public void ReplacePoints(IEnumerable<Point> points)
    {
        _points.Clear();
        _points.AddRange(points);
    }

    public override Rect Bounds
    {
        get
        {
            if (_points.Count == 0) return Rect.Empty;
            var left = _points.Min(p => p.X);
            var top = _points.Min(p => p.Y);
            var right = _points.Max(p => p.X);
            var bottom = _points.Max(p => p.Y);
            var pad = StrokeWidth / 2 + 1;
            return new Rect(left - pad, top - pad, right - left + pad * 2, bottom - top + pad * 2);
        }
    }

    protected override void RenderCore(DrawingContext dc, ScreenshotRenderContext context)
    {
        if (_points.Count == 0) return;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(_points[0], false, false);
            for (var i = 1; i < _points.Count; i++)
                ctx.LineTo(_points[i], true, false);
        }
        geometry.Freeze();
        var pen = new Pen(new SolidColorBrush(Color), StrokeWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        // 荧光笔垫底：乘法混合近似（半透明黄色叠加）
        dc.DrawGeometry(null, pen, geometry);
    }

    public override bool HitTest(Point p, double tolerance)
    {
        var dist = tolerance + StrokeWidth / 2;
        for (var i = 1; i < _points.Count; i++)
        {
            if (DistanceToSegment(p, _points[i - 1], _points[i]) <= dist)
                return true;
        }
        return _points.Count == 1 && Distance(_points[0], p) <= dist;
    }

    public override bool Intersects(Rect rect)
    {
        var inflated = rect.Inflated(StrokeWidth / 2);
        foreach (var p in _points)
        {
            if (inflated.Contains(p))
                return true;
        }
        return false;
    }

    public override void Offset(double dx, double dy)
    {
        for (var i = 0; i < _points.Count; i++)
            _points[i] = new Point(_points[i].X + dx, _points[i].Y + dy);
    }

    public override void Resize(Rect newBounds)
    {
        var old = Bounds;
        if (old.Width <= 0 || old.Height <= 0) return;
        var sx = newBounds.Width / old.Width;
        var sy = newBounds.Height / old.Height;
        for (var i = 0; i < _points.Count; i++)
            _points[i] = new Point(
                newBounds.X + (_points[i].X - old.X) * sx,
                newBounds.Y + (_points[i].Y - old.Y) * sy);
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        if (dx == 0 && dy == 0) return Distance(p, a);
        var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy);
        t = Math.Clamp(t, 0, 1);
        return Distance(p, new Point(a.X + t * dx, a.Y + t * dy));
    }
}

/// <summary>矩形/椭圆</summary>
public sealed class ShapeAnnotation : ScreenshotAnnotation
{
    public ShapeAnnotation(Rect rect, Color color, double strokeWidth, bool isEllipse, bool fill = false)
    {
        Rect = rect;
        Color = color;
        StrokeWidth = strokeWidth;
        IsEllipse = isEllipse;
        Fill = fill;
    }

    public Rect Rect { get; private set; }
    public Color Color { get; }
    public double StrokeWidth { get; }
    public bool IsEllipse { get; }
    public bool Fill { get; }

    public override Rect Bounds => Rect;

    protected override void RenderCore(DrawingContext dc, ScreenshotRenderContext context)
    {
        var brush = new SolidColorBrush(Color);
        brush.Freeze();
        var pen = new Pen(brush, StrokeWidth);
        pen.Freeze();
        Geometry geometry = IsEllipse
            ? new EllipseGeometry(Rect)
            : new RectangleGeometry(Rect);
        geometry.Freeze();
        if (Fill)
        {
            var fillBrush = new SolidColorBrush(Color.FromArgb(60, Color.R, Color.G, Color.B));
            fillBrush.Freeze();
            dc.DrawGeometry(fillBrush, pen, geometry);
        }
        else
        {
            dc.DrawGeometry(null, pen, geometry);
        }
    }

    public override bool HitTest(Point p, double tolerance)
    {
        if (Fill && Rect.Contains(p)) return true;
        var pad = tolerance + StrokeWidth / 2;
        var outer = Rect.Inflated(pad);
        var inner = new Rect(Rect.X + pad, Rect.Y + pad, Math.Max(0, Rect.Width - pad * 2), Math.Max(0, Rect.Height - pad * 2));
        return outer.Contains(p) && (inner.IsEmpty || inner.Width <= 0 || inner.Height <= 0 || !inner.Contains(p));
    }

    public override bool Intersects(Rect rect) => Rect.Intersect(rect, Rect.Inflated(StrokeWidth / 2)) is { IsEmpty: false };

    public override void Offset(double dx, double dy) => Rect = new Rect(Rect.X + dx, Rect.Y + dy, Rect.Width, Rect.Height);

    public override void Resize(Rect newBounds) => Rect = newBounds;
}

/// <summary>箭头</summary>
public sealed class ArrowAnnotation : ScreenshotAnnotation
{
    public ArrowAnnotation(Point from, Point to, Color color, double strokeWidth)
    {
        From = from;
        To = to;
        Color = color;
        StrokeWidth = strokeWidth;
    }

    public Point From { get; private set; }
    public Point To { get; private set; }
    public Color Color { get; }
    public double StrokeWidth { get; }

    private double HeadLength => Math.Clamp(StrokeWidth * 3.2, 14, 48);

    public override Rect Bounds
    {
        get
        {
            var pad = HeadLength / 2 + StrokeWidth;
            var left = Math.Min(From.X, To.X) - pad;
            var top = Math.Min(From.Y, To.Y) - pad;
            var right = Math.Max(From.X, To.X) + pad;
            var bottom = Math.Max(From.Y, To.Y) + pad;
            return new Rect(left, top, right - left, bottom - top);
        }
    }

    protected override void RenderCore(DrawingContext dc, ScreenshotRenderContext context)
    {
        var brush = new SolidColorBrush(Color);
        brush.Freeze();
        var pen = new Pen(brush, StrokeWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();

        var dx = To.X - From.X;
        var dy = To.Y - From.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1) return;
        var ux = dx / length;
        var uy = dy / length;

        // 箭杆终点回退半个箭头长度，保证三角形覆盖线端
        var lineEnd = new Point(To.X - ux * HeadLength * 0.35, To.Y - uy * HeadLength * 0.35);
        dc.DrawLine(pen, From, lineEnd);

        var half = HeadLength / 2;
        var p1 = To;
        var p2 = new Point(To.X - ux * HeadLength - uy * half, To.Y - uy * HeadLength + ux * half);
        var p3 = new Point(To.X - ux * HeadLength + uy * half, To.Y - uy * HeadLength - ux * half);
        var triangle = new StreamGeometry();
        using (var ctx = triangle.Open())
        {
            ctx.BeginFigure(p1, true, true);
            ctx.LineTo(p2, true, false);
            ctx.LineTo(p3, true, false);
        }
        triangle.Freeze();
        dc.DrawGeometry(brush, null, triangle);
    }

    public override bool HitTest(Point p, double tolerance)
    {
        var dist = tolerance + StrokeWidth / 2 + 4;
        var dx = To.X - From.X;
        var dy = To.Y - From.Y;
        if (dx == 0 && dy == 0) return false;
        var t = ((p.X - From.X) * dx + (p.Y - From.Y) * dy) / (dx * dx + dy * dy);
        t = Math.Clamp(t, 0, 1);
        var cx = From.X + t * dx;
        var cy = From.Y + t * dy;
        return Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy)) <= dist;
    }

    public override bool Intersects(Rect rect) => Rect.Intersect(rect, Bounds) is { IsEmpty: false };

    public override void Offset(double dx, double dy)
    {
        From = new Point(From.X + dx, From.Y + dy);
        To = new Point(To.X + dx, To.Y + dy);
    }

    public override void Resize(Rect newBounds)
    {
        var old = Bounds;
        if (old.Width <= 0 || old.Height <= 0) return;
        var sx = newBounds.Width / old.Width;
        var sy = newBounds.Height / old.Height;
        From = new Point(newBounds.X + (From.X - old.X) * sx, newBounds.Y + (From.Y - old.Y) * sy);
        To = new Point(newBounds.X + (To.X - old.X) * sx, newBounds.Y + (To.Y - old.Y) * sy);
    }
}

/// <summary>文字</summary>
public sealed class TextAnnotation : ScreenshotAnnotation
{
    private const double PaddingX = 4;
    private const double PaddingY = 3;

    public TextAnnotation(Point position, string text, Color color, double fontSize, bool useBackground = true)
    {
        Position = position;
        Text = text;
        Color = color;
        FontSize = fontSize;
        UseBackground = useBackground;
    }

    public Point Position { get; private set; }
    public string Text { get; private set; }
    public Color Color { get; }
    public double FontSize { get; private set; }
    public bool UseBackground { get; }

    private FormattedText CreateFormattedText()
    {
        var ft = new FormattedText(
            Text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            FontSize,
            new SolidColorBrush(Color),
            1.0);
        return ft;
    }

    public override Rect Bounds
    {
        get
        {
            var ft = CreateFormattedText();
            return new Rect(Position.X - PaddingX, Position.Y - PaddingY, ft.Width + PaddingX * 2, ft.Height + PaddingY * 2);
        }
    }

    protected override void RenderCore(DrawingContext dc, ScreenshotRenderContext context)
    {
        var ft = CreateFormattedText();
        var bounds = new Rect(Position.X - PaddingX, Position.Y - PaddingY, ft.Width + PaddingX * 2, ft.Height + PaddingY * 2);
        if (UseBackground)
        {
            var bg = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
            bg.Freeze();
            var corner = new RectangleGeometry(new Rect(bounds.Location, bounds.Size), 3, 3);
            corner.Freeze();
            dc.DrawGeometry(bg, null, corner);
        }
        dc.DrawText(ft, Position);
    }

    public override bool HitTest(Point p, double tolerance) => Bounds.Inflated(tolerance).Contains(p);

    public override bool Intersects(Rect rect) => Rect.Intersect(rect, Bounds) is { IsEmpty: false };

    public override void Offset(double dx, double dy) => Position = new Point(Position.X + dx, Position.Y + dy);

    public override void Resize(Rect newBounds)
    {
        var old = Bounds;
        if (old.Height <= 0) return;
        FontSize = Math.Clamp(FontSize * newBounds.Height / old.Height, 8, 256);
    }
}

/// <summary>序号（带圈数字）</summary>
public sealed class NumberAnnotation : ScreenshotAnnotation
{
    public NumberAnnotation(Point center, int number, Color color, double fontSize)
    {
        Center = center;
        Number = number;
        Color = color;
        FontSize = fontSize;
    }

    public Point Center { get; private set; }
    public int Number { get; }
    public Color Color { get; }
    public double FontSize { get; private set; }

    private double Diameter => Math.Max(FontSize * 2.2, 18);

    public override Rect Bounds => new(Center.X - Diameter / 2, Center.Y - Diameter / 2, Diameter, Diameter);

    protected override void RenderCore(DrawingContext dc, ScreenshotRenderContext context)
    {
        var brush = new SolidColorBrush(Color);
        brush.Freeze();
        var geometry = new EllipseGeometry(Bounds);
        geometry.Freeze();
        dc.DrawGeometry(brush, null, geometry);

        var text = Number.ToString(CultureInfo.InvariantCulture);
        var ft = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            FontSize,
            new SolidColorBrush(Colors.White),
            1.0);
        dc.DrawText(ft, new Point(Center.X - ft.Width / 2, Center.Y - ft.Height / 2));
    }

    public override bool HitTest(Point p, double tolerance) => Bounds.Inflated(tolerance).Contains(p);

    public override bool Intersects(Rect rect) => Rect.Intersect(rect, Bounds) is { IsEmpty: false };

    public override void Offset(double dx, double dy) => Center = new Point(Center.X + dx, Center.Y + dy);

    public override void Resize(Rect newBounds)
    {
        var old = Bounds;
        if (old.Height <= 0) return;
        FontSize = Math.Clamp(FontSize * newBounds.Height / old.Height, 8, 128);
    }
}

/// <summary>鼠标指针图元</summary>
public sealed class CursorAnnotation : ScreenshotAnnotation
{
    // 标准 Windows 箭头光标轮廓（12x19 缩放到 fontSize*2）
    private static readonly Point[] CursorOutline =
    [
        new(0, 0), new(0, 16), new(4, 12.5), new(6.5, 18), new(9, 17), new(6.5, 11.5), new(11, 11.5)
    ];

    public CursorAnnotation(Point tip, Color color)
    {
        Tip = tip;
        Color = color;
    }

    public Point Tip { get; private set; }
    public Color Color { get; }
    public double Size { get; init; } = 32;

    public override Rect Bounds
    {
        get
        {
            var s = Size / 19.0;
            var maxX = CursorOutline.Max(p => p.X);
            var maxY = CursorOutline.Max(p => p.Y);
            return new Rect(Tip.X, Tip.Y, maxX * s, maxY * s);
        }
    }

    protected override void RenderCore(DrawingContext dc, ScreenshotRenderContext context)
    {
        var s = Size / 19.0;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(Tip.X + CursorOutline[0].X * s, Tip.Y + CursorOutline[0].Y * s), true, true);
            for (var i = 1; i < CursorOutline.Length; i++)
                ctx.LineTo(new Point(Tip.X + CursorOutline[i].X * s, Tip.Y + CursorOutline[i].Y * s), true, false);
        }
        geometry.Freeze();
        var fill = new SolidColorBrush(Color);
        fill.Freeze();
        var pen = new Pen(new SolidColorBrush(Colors.Black), Math.Max(1.2, s)) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        dc.DrawGeometry(fill, pen, geometry);
    }

    public override bool HitTest(Point p, double tolerance) => Bounds.Inflated(tolerance).Contains(p);

    public override bool Intersects(Rect rect) => Rect.Intersect(rect, Bounds) is { IsEmpty: false };

    public override void Offset(double dx, double dy) => Tip = new Point(Tip.X + dx, Tip.Y + dy);

    public override void Resize(Rect newBounds)
    {
        var old = Bounds;
        if (old.Height <= 0) return;
        // 光标仅整体平移到新包围盒左上角，保持形状比例
        Offset(newBounds.X - old.X, newBounds.Y - old.Y);
    }
}

/// <summary>Rect 辅助扩展（避免 Rect.Inflate 静态重载要求 Size 的繁琐）</summary>
internal static class ScreenshotRectExtensions
{
    public static Rect Inflated(this Rect rect, double delta) =>
        new(rect.X - delta, rect.Y - delta, Math.Max(0, rect.Width + delta * 2), Math.Max(0, rect.Height + delta * 2));
}
