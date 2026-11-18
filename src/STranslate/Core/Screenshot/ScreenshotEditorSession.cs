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
        FrozenImage = frozenImage;
        FrozenSource = frozenSource;
        VirtualBounds = virtualBounds;
        Monitors = monitors;
        WindowFinder = windowFinder;
        Options = options;
        Selection = options.InitialSelection;
    }

    // ===== 冻结的桌面 =====
    public Bitmap FrozenImage { get; }
    public BitmapSource FrozenSource { get; }
    public Rect VirtualBounds { get; }
    public List<ScreenshotMonitor> Monitors { get; }
    public ScreenshotWindowFinder WindowFinder { get; }
    public ScreenshotEditOptions Options { get; }

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
    /// 将选区内容导出为位图（所见即所得，RenderTargetBitmap 1:1 物理像素）
    /// </summary>
    public Bitmap Flatten(Rect selection, bool withAnnotations)
    {
        var w = Math.Max(1, (int)Math.Round(selection.Width));
        var h = Math.Max(1, (int)Math.Round(selection.Height));
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(20, 20, 20)), null, new Rect(0, 0, w, h));
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));
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
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();

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

    public void Dispose()
    {
        FrozenImage.Dispose();
    }
}
