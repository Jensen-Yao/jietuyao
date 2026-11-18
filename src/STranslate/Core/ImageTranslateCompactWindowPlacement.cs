using System.Drawing;
using System.Windows;

using WpfRect = System.Windows.Rect;

namespace STranslate.Core;

internal static class ImageTranslateCompactWindowPlacement
{
    /// <summary>
    /// 按截图选区物理坐标与屏幕工作区计算精简窗口完整布局：
    /// 窗口矩形、图片在窗口内的偏移、按钮条位置与按钮侧（下方/上方/叠加）。
    /// 算法在物理像素域计算；输出给 XAML 的偏移/按钮位置换算成 DIP。
    /// 铁律：图片始终钉在选区物理屏幕位置，任何避让都通过移动按钮条完成。
    /// </summary>
    internal static CompactWindowLayout CreateLayout(
        Rectangle imageBounds,
        Rectangle workArea,
        double dpiScaleX,
        double dpiScaleY,
        double minWidthDip,
        double minImageHeightDip,
        double toolbarWidthDip,
        double toolbarHeightDip,
        double gapHDip,
        double gapVDip,
        double windowMarginDip)
    {
        var toolbarWidth = ToPhysicalPixels(toolbarWidthDip, dpiScaleX);
        var toolbarHeight = ToPhysicalPixels(toolbarHeightDip, dpiScaleY);
        var gapH = ToPhysicalPixels(gapHDip, dpiScaleX);
        var gapV = ToPhysicalPixels(gapVDip, dpiScaleY);
        var margin = ToPhysicalPixels(windowMarginDip, dpiScaleY);
        var minWidth = ToPhysicalPixels(minWidthDip, dpiScaleX);
        var minImageHeight = ToPhysicalPixels(minImageHeightDip, dpiScaleY);

        var imageWidth = Math.Max(minWidth, imageBounds.Width);
        var imageHeight = Math.Max(minImageHeight, imageBounds.Height);

        // —— 横向 ——
        int windowLeft, windowRight, toolbarX;
        if (toolbarWidth <= imageWidth)
        {
            // 按钮条窄于选区：居中
            toolbarX = imageBounds.Left + (imageWidth - toolbarWidth) / 2;
            windowLeft = imageBounds.Left;
            windowRight = imageBounds.Left + imageWidth;
        }
        else
        {
            // 贴左缘向右延展
            var toolbarLeft = imageBounds.Left + gapH;
            var toolbarRight = toolbarLeft + toolbarWidth;
            var imageRight = imageBounds.Left + imageWidth;

            if (toolbarRight + gapH <= workArea.Right)
            {
                // 右边放得下：向右延展
                toolbarX = toolbarLeft;
                windowLeft = imageBounds.Left;
                windowRight = Math.Max(imageRight, toolbarRight + gapH);
            }
            else
            {
                // 右边放不下：镜像向左延展，按钮条右缘 = 图片右缘 - gap
                toolbarX = imageRight - gapH - toolbarWidth;
                windowLeft = toolbarX - gapH;
                windowRight = imageRight;
            }
        }

        // —— 纵向 ——
        var imageBottom = imageBounds.Top + imageHeight;
        var spaceBelow = workArea.Bottom - imageBottom;

        int toolbarY, windowTop, windowBottom;
        ToolbarSide side;

        if (spaceBelow >= toolbarHeight + gapV)
        {
            // 下方放得下
            toolbarY = imageBottom + gapV;
            windowTop = imageBounds.Top;
            windowBottom = imageBottom + gapV + toolbarHeight + margin;
            side = ToolbarSide.Below;
        }
        else
        {
            var spaceAbove = imageBounds.Top - workArea.Top;
            if (spaceAbove >= toolbarHeight + gapV)
            {
                // 翻上方
                toolbarY = imageBounds.Top - gapV - toolbarHeight;
                windowTop = toolbarY;
                windowBottom = imageBottom + margin;
                side = ToolbarSide.Above;
            }
            else
            {
                // 上下都放不下：叠加在图片底部之上（按钮条 ZIndex 高于图片，由 XAML 保证）
                toolbarY = imageBottom - toolbarHeight;
                windowTop = imageBounds.Top;
                windowBottom = imageBottom;
                side = ToolbarSide.Overlay;
            }
        }

        var windowWidth = windowRight - windowLeft;
        var windowHeight = windowBottom - windowTop;

        return new CompactWindowLayout(
            new Rectangle(windowLeft, windowTop, windowWidth, windowHeight),
            imageBounds.Left - windowLeft,
            imageBounds.Top - windowTop,
            imageWidth,
            imageHeight,
            toolbarX - windowLeft,
            toolbarY - windowTop,
            toolbarWidth,
            toolbarHeight,
            side);
    }

    internal static Rectangle CreateCenteredOnWorkArea(
        Rectangle workArea,
        System.Drawing.Size bitmapSize,
        double dpiScaleX,
        double dpiScaleY,
        double minWidthDip,
        double minImageHeightDip,
        double toolbarHeightDip,
        double maxWidthRatio,
        double maxHeightRatio)
    {
        var toolbarHeight = ToPhysicalPixels(toolbarHeightDip, dpiScaleY);
        var minWidth = ToPhysicalPixels(minWidthDip, dpiScaleX);
        var minImageHeight = ToPhysicalPixels(minImageHeightDip, dpiScaleY);
        var maxWidth = Math.Max(minWidth, (int)Math.Round(workArea.Width * maxWidthRatio));
        var maxImageHeight = Math.Max(minImageHeight, (int)Math.Round(workArea.Height * maxHeightRatio) - toolbarHeight);
        var width = Clamp(bitmapSize.Width, minWidth, maxWidth);
        var imageHeight = Clamp(bitmapSize.Height, minImageHeight, maxImageHeight);
        var height = imageHeight + toolbarHeight;
        var left = workArea.Left + (workArea.Width - width) / 2;
        var top = workArea.Top + (workArea.Height - height) / 2;

        return new Rectangle(left, top, width, height);
    }

    /// <summary>
    /// 将物理像素矩形换算为 WPF 逻辑像素(DIP)矩形，供同步 WPF 的 Left/Top/Width/Height 使用。
    /// 对齐 ScreenGrab 的 ScaledBounds：物理坐标 ÷ DPI 缩放。
    /// </summary>
    internal static WpfRect ToDipBounds(Rectangle physicalBounds, double dpiScaleX, double dpiScaleY) =>
        new(
            physicalBounds.Left / dpiScaleX,
            physicalBounds.Top / dpiScaleY,
            Math.Max(1d, physicalBounds.Width / dpiScaleX),
            Math.Max(1d, physicalBounds.Height / dpiScaleY));

    private static int ToPhysicalPixels(double dip, double dpiScale) =>
        Math.Max(1, (int)Math.Round(dip * dpiScale));

    private static int Clamp(int value, int min, int max) =>
        Math.Min(Math.Max(value, min), Math.Max(min, max));
}

internal enum ToolbarSide { Below, Above, Overlay }

internal sealed record CompactWindowLayout(
    Rectangle WindowBounds,
    int ImageOffsetX,
    int ImageOffsetY,
    int ImageWidth,
    int ImageHeight,
    int ToolbarX,
    int ToolbarY,
    int ToolbarWidth,
    int ToolbarHeight,
    ToolbarSide ToolbarSide);
