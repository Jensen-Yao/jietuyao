using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Size = System.Windows.Size;
using Pen = System.Windows.Media.Pen;

namespace STranslate.Core;

/// <summary>
/// 单个工具的样式（颜色/线宽/字号），持久化到 Settings.json
/// </summary>
public sealed class ScreenshotToolStyle
{
    public string ColorHex { get; set; } = "#FF0000";
    public double StrokeWidth { get; set; } = 9;
    public double FontSize { get; set; } = 14;
    public bool Fill { get; set; }
    public bool TextBackground { get; set; } = true;
    public double EraserSize { get; set; } = 25;
}

/// <summary>
/// 选区输出样式（jietuyao v1 独家：圆角截图 / 描边 / 阴影 / 锁定纵横比）
/// </summary>
public sealed class SelectionStyleState
{
    /// <summary>圆角截图开关</summary>
    public bool RoundedEnabled { get; set; }

    /// <summary>圆角半径（物理像素）</summary>
    public double RoundedRadius { get; set; } = 16;

    /// <summary>描边/阴影模式：none / shadow / border</summary>
    public string BorderMode { get; set; } = "none";

    /// <summary>描边宽度 / 阴影大小</summary>
    public double BorderSize { get; set; } = 21;

    /// <summary>阴影颜色</summary>
    public string ShadowColorHex { get; set; } = "#0078FF";

    /// <summary>描边颜色</summary>
    public string BorderColorHex { get; set; } = "#FF0000";

    /// <summary>锁定纵横比（调整选区时保持比例）</summary>
    public bool LockRatio { get; set; }
}

/// <summary>
/// 截图编辑器会话：冻结图像、选区、标注、撤销与完成状态的唯一事实源，
/// 由各显示器的编辑器窗口共享。
/// </summary>
public sealed class ScreenshotEditorSession : IDisposable
{
    /// <summary>常用调色板</summary>
    public static readonly string[] Palette =
    [
        "#FF0000", "#FF7F00", "#FFD700", "#00B050", "#00B0F0", "#0078D7", "#8A2BE2", "#FF69B4", "#000000", "#FFFFFF"
    ];

    public ScreenshotEditorSession(Bitmap frozenImage, BitmapSource frozenSource, Rect virtualBounds,
        List<ScreenshotMonitor> monitors, ScreenshotWindowFinder windowFinder, ScreenshotEditOptions options)
    {
        _frozenImage = frozenImage;
        _frozenSource = frozenSource;
        VirtualBounds = virtualBounds;
        Monitors = monitors;
        WindowFinder = windowFinder;
        Options = options;
        Selection = options.InitialSelection;
    }

    // ===== 冻结的桌面 =====
    private Bitmap _frozenImage;
    private BitmapSource _frozenSource;

    public Bitmap FrozenImage => _frozenImage;
    public BitmapSource FrozenSource => _frozenSource;
    public Rect VirtualBounds { get; }
    public List<ScreenshotMonitor> Monitors { get; }
    public ScreenshotWindowFinder WindowFinder { get; }
    public ScreenshotEditOptions Options { get; }

    /// <summary>选区输出样式（圆角/描边/阴影/锁定比例）</summary>
    public SelectionStyleState SelectionStyle { get; internal set; } = new();

    // ===== 会话状态（物理像素）=====
    private Rect? _selection;
    public Rect? Selection
    {
        get => _selection;
        set
        {
            _selection = value;
            SelectionChanged?.Invoke();
        }
    }

    public ScreenshotTool ActiveTool { get; private set; } = ScreenshotTool.Select;

    public List<ScreenshotAnnotation> Annotations { get; } = [];

    public ScreenshotUndoHistory Undo { get; } = new();

    /// <summary>工具样式（键为工具名）</summary>
    public Dictionary<string, ScreenshotToolStyle> ToolStyles { get; set; } = new();

    /// <summary>文字编辑中的图元（未提交）</summary>
    public TextAnnotation? EditingText { get; set; }

    // ===== 事件 =====
    public event Action? RepaintNeeded;
    public event Action? SelectionChanged;
    public event Action? ToolChanged;
    public event Action? UndoChanged;
    public event Action? SessionClosing;
    /// <summary>编辑器需要一个合适的窗口来承载工具栏/文本框（参数为物理坐标）</summary>
    public event Action<Point>? OverlayFocusNeeded;

    // ===== 完成 =====
    private readonly TaskCompletionSource<ScreenshotEditorResult?> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _finished;

    public Task<ScreenshotEditorResult?> Task => _tcs.Task;

    public void RequestRepaint() => RepaintNeeded?.Invoke();

    public void SetTool(ScreenshotTool tool)
    {
        if (ActiveTool == tool) return;
        ActiveTool = tool;
        ToolChanged?.Invoke();
    }

    /// <summary>获取工具样式（带默认值）</summary>
    public ScreenshotToolStyle GetStyle(ScreenshotTool tool)
    {
        var key = tool.ToString();
        if (!ToolStyles.TryGetValue(key, out var style))
        {
            style = tool switch
            {
                ScreenshotTool.Pen => new ScreenshotToolStyle { ColorHex = "#FF0000", StrokeWidth = 12 },
                ScreenshotTool.Highlighter => new ScreenshotToolStyle { ColorHex = "#FFFF00", StrokeWidth = 15 },
                ScreenshotTool.Rectangle or ScreenshotTool.Ellipse or ScreenshotTool.Arrow => new ScreenshotToolStyle { ColorHex = "#FF0000", StrokeWidth = 9 },
                ScreenshotTool.Text => new ScreenshotToolStyle { ColorHex = "#000000", FontSize = 14 },
                ScreenshotTool.Number => new ScreenshotToolStyle { ColorHex = "#FF0000", FontSize = 10 },
                ScreenshotTool.Cursor => new ScreenshotToolStyle { ColorHex = "#FF0000", StrokeWidth = 32 },
                ScreenshotTool.Eraser => new ScreenshotToolStyle { EraserSize = 25 },
                _ => new ScreenshotToolStyle(),
            };
            ToolStyles[key] = style;
        }
        return style;
    }

    public Color GetColor(ScreenshotTool tool) => (Color)ColorConverter.ConvertFromString(GetStyle(tool).ColorHex);

    // ===== 标注操作（全部可撤销）=====

    public void AddAnnotation(ScreenshotAnnotation annotation)
    {
        Undo.Push("add", () => Annotations.Remove(annotation), () => Annotations.Add(annotation));
        RepaintNeeded?.Invoke();
    }

    public void RemoveAnnotations(IReadOnlyList<ScreenshotAnnotation> items)
    {
        if (items.Count == 0) return;
        // 记录当时的 z-order 位置
        var indexMap = items.Select(a => (a, idx: Annotations.IndexOf(a))).ToList();
        Undo.Push("erase",
            () => { foreach (var (a, _) in indexMap) Annotations.Remove(a); },
            () =>
            {
                foreach (var (a, idx) in indexMap.OrderByDescending(x => x.idx))
                    Annotations.Insert(Math.Clamp(idx, 0, Annotations.Count), a);
            });
        RepaintNeeded?.Invoke();
    }

    public void ClearAnnotations()
    {
        if (Annotations.Count == 0) return;
        var snapshot = Annotations.ToList();
        Undo.Push("clear",
            () => { Annotations.Clear(); Annotations.AddRange(snapshot); },
            () => Annotations.Clear());
        RepaintNeeded?.Invoke();
    }

    public ScreenshotAnnotation? HitTestAnnotation(Point p, double tolerance = 6)
    {
        for (var i = Annotations.Count - 1; i >= 0; i--)
        {
            if (Annotations[i].HitTest(p, tolerance))
                return Annotations[i];
        }
        return null;
    }

    /// <summary>下一个序号</summary>
    public int NextNumber() => Annotations.OfType<NumberAnnotation>().Select(a => a.Number).DefaultIfEmpty(0).Max() + 1;

    // ===== 完成/取消 =====

    public void Finish(ScreenshotEditorAction action)
    {
        if (_finished) return;
        _finished = true;

        var selection = Selection;
        if (selection is not { } sel || sel.Width < 1 || sel.Height < 1 || action is ScreenshotEditorAction.None or ScreenshotEditorAction.Close)
        {
            _tcs.TrySetResult(action == ScreenshotEditorAction.None ? null :
                new ScreenshotEditorResult
                {
                    EditedImage = new Bitmap(1, 1),
                    CleanImage = new Bitmap(1, 1),
                    PhysicalBounds = Rect.Empty,
                    Action = action
                });
            SessionClosing?.Invoke();
            return;
        }

        var edited = Flatten(sel, true);
        var clean = Flatten(sel, false);
        _tcs.TrySetResult(new ScreenshotEditorResult
        {
            EditedImage = edited,
            CleanImage = clean,
            PhysicalBounds = sel,
            Action = action
        });
        SessionClosing?.Invoke();
    }

    public void Cancel() => Finish(ScreenshotEditorAction.None);

    /// <summary>
    /// 将选区内容导出为位图（所见即所得，RenderTargetBitmap 1:1 物理像素）。
    /// 含标注时附加选区样式效果（圆角/阴影/描边）；干净图永不加工。
    /// </summary>
    public Bitmap Flatten(Rect selection, bool withAnnotations)
    {
        var w = Math.Max(1, (int)Math.Round(selection.Width));
        var h = Math.Max(1, (int)Math.Round(selection.Height));

        var style = SelectionStyle;
        var rounded = withAnnotations && style.RoundedEnabled && style.RoundedRadius > 0;

        // 核心层：桌面 + 标注（可选圆角裁剪）
        var coreVisual = new DrawingVisual();
        using (var dc = coreVisual.RenderOpen())
        {
            var clip = rounded
                ? new RectangleGeometry(new Rect(0, 0, w, h), style.RoundedRadius, style.RoundedRadius)
                : new RectangleGeometry(new Rect(0, 0, w, h));
            clip.Freeze();
            dc.PushClip(clip);
            dc.PushTransform(new TranslateTransform(-selection.X, -selection.Y));
            dc.DrawImage(FrozenSource, VirtualBounds);
            if (withAnnotations)
            {
                foreach (var annotation in Annotations)
                    annotation.Render(dc);
            }
            dc.Pop();
            dc.Pop();
        }
        var coreRtb = RenderToRtb(coreVisual, w, h);

        if (!withAnnotations || style.BorderMode == "none")
            return RtbToGdi(coreRtb);

        // 效果层：阴影 / 描边
        try
        {
            var mode = style.BorderMode;
            var size = Math.Clamp(style.BorderSize, 1, 50);
            var pad = mode == "shadow" ? (int)Math.Ceiling(size * 0.75) : (int)Math.Ceiling(size / 2.0) + 1;
            var totalW = w + pad * 2;
            var totalH = h + pad * 2;

            var image = new System.Windows.Controls.Image
            {
                Source = coreRtb,
                Width = w,
                Height = h,
            };
            if (mode == "shadow")
            {
                image.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = (Color)ColorConverter.ConvertFromString(style.ShadowColorHex),
                    BlurRadius = size,
                    ShadowDepth = size / 3.0,
                    Direction = 315,
                    Opacity = 0.9,
                };
            }
            image.Measure(new Size(w, h));
            image.Arrange(new Rect(0, 0, w, h));
            image.UpdateLayout();

            var outer = new DrawingVisual();
            using (var dc = outer.RenderOpen())
            {
                var brush = new VisualBrush(image) { Stretch = Stretch.None };
                dc.DrawRectangle(brush, null, new Rect(pad, pad, w, h));

                if (mode == "border")
                {
                    var borderPen = new Pen(
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString(style.BorderColorHex)), size);
                    borderPen.Freeze();
                    var rect = new Rect(pad, pad, w, h);
                    var geometry = rounded
                        ? new RectangleGeometry(rect, style.RoundedRadius, style.RoundedRadius)
                        : new RectangleGeometry(rect);
                    geometry.Freeze();
                    dc.DrawGeometry(null, borderPen, geometry);
                }
            }
            return RtbToGdi(RenderToRtb(outer, totalW, totalH));
        }
        catch
        {
            return RtbToGdi(coreRtb);
        }
    }

    private static RenderTargetBitmap RenderToRtb(System.Windows.Media.Visual visual, int w, int h)
    {
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    private static Bitmap RtbToGdi(RenderTargetBitmap rtb)
    {
        var w = rtb.PixelWidth;
        var h = rtb.PixelHeight;
        var bytes = new byte[w * h * 4];
        rtb.CopyPixels(new Int32Rect(0, 0, w, h), bytes, w * 4, 0);
        var bitmap = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        try
        {
            Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return bitmap;
    }

    /// <summary>读取冻结图像像素颜色（放大镜取色）</summary>
    public System.Drawing.Color GetPixelAt(Point physicalPoint)
    {
        var x = (int)(physicalPoint.X - VirtualBounds.Left);
        var y = (int)(physicalPoint.Y - VirtualBounds.Top);
        if (x < 0 || y < 0 || x >= FrozenImage.Width || y >= FrozenImage.Height)
            return System.Drawing.Color.Transparent;
        return FrozenImage.GetPixel(x, y);
    }

    /// <summary>
    /// 刷新背景：保持选区与标注不变，重新冻结屏幕内容（v1"刷新背景重截"）
    /// </summary>
    public void RefreshBackground()
    {
        var oldImage = _frozenImage;
        var frozen = ScreenshotCapturer.CaptureVirtualDesktop(VirtualBounds);
        _frozenImage = frozen;
        _frozenSource = ScreenshotCapturer.ToBitmapSource(frozen);
        WindowFinder.Snapshot(VirtualBounds);
        RepaintNeeded?.Invoke();
        // 旧位图延迟释放，避免与渲染竞态
        _ = System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            try { oldImage.Dispose(); } catch { /* 已释放 */ }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    public void Dispose()
    {
        _frozenImage.Dispose();
    }
}
