using System.Drawing;
using System.Windows;

namespace STranslate.Core;

/// <summary>
/// 截图编辑器可用的标注工具
/// </summary>
public enum ScreenshotTool
{
    /// <summary>选择/移动（默认，可拖动选区与已绘制的图元）</summary>
    Select,

    /// <summary>画笔</summary>
    Pen,

    /// <summary>荧光笔</summary>
    Highlighter,

    /// <summary>矩形</summary>
    Rectangle,

    /// <summary>椭圆</summary>
    Ellipse,

    /// <summary>箭头</summary>
    Arrow,

    /// <summary>文字</summary>
    Text,

    /// <summary>序号</summary>
    Number,

    /// <summary>橡皮（整图元擦除）</summary>
    Eraser,

    /// <summary>鼠标指针</summary>
    Cursor,

    /// <summary>马赛克（v2 新增）</summary>
    Mosaic,
}

/// <summary>
/// 编辑器结束方式
/// </summary>
public enum ScreenshotEditorAction
{
    /// <summary>取消 / Esc</summary>
    None,

    /// <summary>复制到剪贴板</summary>
    Copy,

    /// <summary>保存到文件</summary>
    Save,

    /// <summary>贴图</summary>
    Pin,

    /// <summary>OCR 识别</summary>
    Ocr,

    /// <summary>截图翻译</summary>
    Translate,

    /// <summary>直接关闭（已保存等场景）</summary>
    Close,
}

/// <summary>
/// 编辑器结果。所有位图均为物理像素，坐标基于虚拟桌面。
/// </summary>
public sealed class ScreenshotEditorResult : IDisposable
{
    /// <summary>含标注的最终图像（所见即所得）</summary>
    public required Bitmap EditedImage { get; init; }

    /// <summary>不含标注的干净图像（供 OCR 等识别场景可选使用）</summary>
    public required Bitmap CleanImage { get; init; }

    /// <summary>选区在虚拟桌面中的物理像素范围</summary>
    public required Rect PhysicalBounds { get; init; }

    /// <summary>结束动作</summary>
    public required ScreenshotEditorAction Action { get; init; }

    /// <summary>保存动作时的目标路径（由编辑器服务在用户选择路径后回填）</summary>
    public string? SavedFilePath { get; set; }

    private bool _editedTaken;
    private bool _cleanTaken;

    /// <summary>取走含标注图像的所有权（Dispose 时跳过）</summary>
    public Bitmap TakeEdited()
    {
        _editedTaken = true;
        return EditedImage;
    }

    /// <summary>取走干净图像的所有权（Dispose 时跳过）</summary>
    public Bitmap TakeClean()
    {
        _cleanTaken = true;
        return CleanImage;
    }

    public void Dispose()
    {
        if (!_editedTaken) EditedImage.Dispose();
        if (!_cleanTaken) CleanImage.Dispose();
    }
}

/// <summary>
/// 编辑器行为选项
/// </summary>
public sealed class ScreenshotEditOptions
{
    /// <summary>智能选区（悬停识别窗口）</summary>
    public bool SmartSelection { get; init; } = true;

    /// <summary>显示放大镜与取色</summary>
    public bool ShowMagnifier { get; init; } = true;

    /// <summary>初始选区（物理像素），为空则由用户框选</summary>
    public Rect? InitialSelection { get; init; }

    /// <summary>默认动作按钮高亮（仅 UI 提示用）</summary>
    public ScreenshotEditorAction DefaultAction { get; init; } = ScreenshotEditorAction.Copy;

    /// <summary>是否隐藏主窗口与贴图翻译窗口后再截图</summary>
    public bool HideAppWindows { get; init; } = true;
}

/// <summary>
/// 物理显示器信息（PerMonitorV2 下均为物理像素）
/// </summary>
public sealed class ScreenshotMonitor
{
    /// <summary>显示器在虚拟桌面中的物理范围</summary>
    public required Rect Bounds { get; init; }

    /// <summary>是否主显示器</summary>
    public bool IsPrimary { get; init; }

    /// <summary>有效 DPI 缩放（96 基准）</summary>
    public double DpiScale { get; init; }

    /// <summary>设备名（\\.\DISPLAY1）</summary>
    public string DeviceName { get; init; } = string.Empty;
}
