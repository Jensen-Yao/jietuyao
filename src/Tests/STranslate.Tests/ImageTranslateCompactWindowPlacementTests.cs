using STranslate.Core;
using System.Drawing;
using WpfRect = System.Windows.Rect;

namespace STranslate.Tests;

public class ImageTranslateCompactWindowPlacementTests
{
    [Fact]
    public void CreateLayoutCentersToolbarWhenNarrowerThanImage()
    {
        // 选区 640x360，100% 缩放，按钮条 DIP 高 64 宽 300。
        // workArea 足够大，下方放得下。
        var layout = ImageTranslateCompactWindowPlacement.CreateLayout(
            imageBounds: new Rectangle(100, 100, 640, 360),
            workArea: new Rectangle(0, 0, 1920, 1080),
            dpiScaleX: 1.0,
            dpiScaleY: 1.0,
            minWidthDip: 1,
            minImageHeightDip: 1,
            toolbarWidthDip: 300,
            toolbarHeightDip: 64,
            gapHDip: 8,
            gapVDip: 6,
            windowMarginDip: 8);

        // 窗口顶 = 选区顶；窗口高 = 图片高 + 纵向间距 + 按钮条高 + 底 margin
        Assert.Equal(new Rectangle(100, 100, 640, 360 + 6 + 64 + 8), layout.WindowBounds);
        // 图片在窗口内偏移 (0,0)，因为窗口顶左 = 选区顶左
        Assert.Equal(0, layout.ImageOffsetX);
        Assert.Equal(0, layout.ImageOffsetY);
        // 按钮条居中于选区：左 = 100 + (640-300)/2 = 270；下方
        // ToolbarBounds 是窗口内 DIP 偏移，故左 = (640-300)/2 = 170，顶 = 360+6 = 366
        Assert.Equal(170, layout.ToolbarX);
        Assert.Equal(366, layout.ToolbarY);
        Assert.Equal(ToolbarSide.Below, layout.ToolbarSide);
    }

    [Fact]
    public void CreateLayoutExtendsRightWhenToolbarWiderThanImage()
    {
        // 选区 200x200，按钮条宽 300 > 200。屏幕宽 1920，右边放得下。
        var layout = ImageTranslateCompactWindowPlacement.CreateLayout(
            imageBounds: new Rectangle(100, 100, 200, 200),
            workArea: new Rectangle(0, 0, 1920, 1080),
            dpiScaleX: 1.0,
            dpiScaleY: 1.0,
            minWidthDip: 1,
            minImageHeightDip: 1,
            toolbarWidthDip: 300,
            toolbarHeightDip: 64,
            gapHDip: 8,
            gapVDip: 6,
            windowMarginDip: 8);

        // 按钮条左缘 = 选区左 + gap = 108；按钮条右 = 408
        // 窗口右 = max(选区右=300, 按钮条右+gap=416) = 416；窗口左 = 选区左 = 100；窗口宽 = 316
        Assert.Equal(new Rectangle(100, 100, 316, 200 + 6 + 64 + 8), layout.WindowBounds);
        Assert.Equal(0, layout.ImageOffsetX);   // 图片左=窗口左
        Assert.Equal(0, layout.ImageOffsetY);
        // 按钮条窗口内偏移：左 = 8 (gap)，顶 = 206
        Assert.Equal(8, layout.ToolbarX);
        Assert.Equal(206, layout.ToolbarY);
        Assert.Equal(ToolbarSide.Below, layout.ToolbarSide);
    }

    [Fact]
    public void CreateLayoutExtendsLeftWhenRightEdgeExceedsWorkArea()
    {
        // 选区左=1850 宽=50，按钮条宽 300。向右延展会顶出 workArea 右=1920。
        var layout = ImageTranslateCompactWindowPlacement.CreateLayout(
            imageBounds: new Rectangle(1850, 100, 50, 200),
            workArea: new Rectangle(0, 0, 1920, 1080),
            dpiScaleX: 1.0,
            dpiScaleY: 1.0,
            minWidthDip: 1,
            minImageHeightDip: 1,
            toolbarWidthDip: 300,
            toolbarHeightDip: 64,
            gapHDip: 8,
            gapVDip: 6,
            windowMarginDip: 8);

        // 向左延展：按钮条右缘 = 图片右 - gap = 1850+50-8 = 1892；按钮条左 = 1892-300 = 1592
        // 窗口左 = 按钮条左 - gap = 1584；窗口右 = 图片右 = 1900；窗口宽 = 316
        Assert.Equal(new Rectangle(1584, 100, 316, 200 + 6 + 64 + 8), layout.WindowBounds);
        // 图片在窗口内偏移：左 = 1850 - 1584 = 266
        Assert.Equal(266, layout.ImageOffsetX);
        Assert.Equal(0, layout.ImageOffsetY);
        // 按钮条窗口内偏移：左 = 1592 - 1584 = 8；顶 = 206
        Assert.Equal(8, layout.ToolbarX);
        Assert.Equal(206, layout.ToolbarY);
        Assert.Equal(ToolbarSide.Below, layout.ToolbarSide);
    }

    [Fact]
    public void CreateLayoutOverlaysWhenBothSidesTooSmall()
    {
        // 选区占满高度：顶=0 高=1080=workArea 高。下方空间=0，上方空间=0，都 < 70 → 叠加。
        var layout = ImageTranslateCompactWindowPlacement.CreateLayout(
            imageBounds: new Rectangle(100, 0, 640, 1080),
            workArea: new Rectangle(0, 0, 1920, 1080),
            dpiScaleX: 1.0,
            dpiScaleY: 1.0,
            minWidthDip: 1,
            minImageHeightDip: 1,
            toolbarWidthDip: 300,
            toolbarHeightDip: 64,
            gapHDip: 8,
            gapVDip: 6,
            windowMarginDip: 8);

        // 叠加：toolbarY = imageBottom - toolbarHeight = 1080 - 64 = 1016
        // 窗口顶 = imageTop = 0；窗口底 = imageBottom = 1080；窗口高 = 1080
        Assert.Equal(new Rectangle(100, 0, 640, 1080), layout.WindowBounds);
        Assert.Equal(0, layout.ImageOffsetX);
        Assert.Equal(0, layout.ImageOffsetY);
        // 按钮条居中：左 = (640-300)/2 = 170；顶 = 1016
        Assert.Equal(170, layout.ToolbarX);
        Assert.Equal(1016, layout.ToolbarY);
        Assert.Equal(ToolbarSide.Overlay, layout.ToolbarSide);
    }

    [Fact]
    public void CreateLayoutFlipsAboveWhenBelowTooSmall()
    {
        // 选区顶=950 高=100，底=1050。workArea 底=1080。下方空间=30 < 64+6=70 放不下。
        // 上方空间 = 950 - 0 = 950 >= 70 放得下 → 翻上方。
        var layout = ImageTranslateCompactWindowPlacement.CreateLayout(
            imageBounds: new Rectangle(100, 950, 640, 100),
            workArea: new Rectangle(0, 0, 1920, 1080),
            dpiScaleX: 1.0,
            dpiScaleY: 1.0,
            minWidthDip: 1,
            minImageHeightDip: 1,
            toolbarWidthDip: 300,
            toolbarHeightDip: 64,
            gapHDip: 8,
            gapVDip: 6,
            windowMarginDip: 8);

        // 翻上方：toolbarY = imageTop - gapV - toolbarHeight = 950 - 6 - 64 = 880
        // 窗口顶 = toolbarY = 880；窗口底 = imageBottom + margin = 1050 + 8 = 1058
        // 窗口高 = 1058 - 880 = 178
        Assert.Equal(new Rectangle(100, 880, 640, 178), layout.WindowBounds);
        // 图片窗口内偏移：左=0；顶 = imageTop - windowTop = 950 - 880 = 70
        Assert.Equal(0, layout.ImageOffsetX);
        Assert.Equal(70, layout.ImageOffsetY);
        // 按钮条居中：左 = (640-300)/2 = 170；顶 = 0
        Assert.Equal(170, layout.ToolbarX);
        Assert.Equal(0, layout.ToolbarY);
        Assert.Equal(ToolbarSide.Above, layout.ToolbarSide);
    }

    [Fact]
    public void CreateCenteredOnWorkAreaClampsToPhysicalWorkArea()
    {
        var actual = ImageTranslateCompactWindowPlacement.CreateCenteredOnWorkArea(
            new Rectangle(100, 50, 1000, 800),
            new Size(2000, 2000),
            dpiScaleX: 1.25,
            dpiScaleY: 1.25,
            minWidthDip: 320,
            minImageHeightDip: 180,
            toolbarHeightDip: 64,
            maxWidthRatio: 0.85,
            maxHeightRatio: 0.85);

        Assert.Equal(new Rectangle(175, 110, 850, 680), actual);
    }

    [Fact]
    public void ToDipBoundsScalesPhysicalPixelsByDpi()
    {
        var actual = ImageTranslateCompactWindowPlacement.ToDipBounds(
            new Rectangle(-120, 80, 640, 440),
            dpiScaleX: 1.25,
            dpiScaleY: 1.25);

        Assert.Equal(new WpfRect(-96, 64, 512, 352), actual);
    }

    [Fact]
    public void ToDipBoundsClampsTinySizeToOneDip()
    {
        var actual = ImageTranslateCompactWindowPlacement.ToDipBounds(
            new Rectangle(10, 20, 0, 0),
            dpiScaleX: 2,
            dpiScaleY: 1.5);

        Assert.Equal(new WpfRect(5, 13.333333333333334, 1, 1), actual);
    }
}
