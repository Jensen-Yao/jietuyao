using STranslate.Helpers;
using System.Windows;

namespace STranslate.Tests;

public class FullscreenWindowBoundsTests
{
    [Fact]
    public void MatchingMonitorBoundsAreFullscreen()
    {
        var bounds = new Rect(-1920, 0, 1920, 1080);

        Assert.True(Win32Helper.IsWindowBoundsFullscreen(bounds, bounds));
    }

    [Fact]
    public void OnePixelRoundingDifferenceIsFullscreen()
    {
        var monitorBounds = new Rect(0, 0, 2560, 1440);
        var windowBounds = new Rect(0, 0, 2559, 1439);

        Assert.True(Win32Helper.IsWindowBoundsFullscreen(windowBounds, monitorBounds));
    }

    [Fact]
    public void SameSizeAtDifferentPositionIsNotFullscreen()
    {
        var monitorBounds = new Rect(0, 0, 1920, 1080);
        var windowBounds = new Rect(100, 0, 1920, 1080);

        Assert.False(Win32Helper.IsWindowBoundsFullscreen(windowBounds, monitorBounds));
    }

    [Fact]
    public void MaximizedWorkAreaWindowIsNotFullscreen()
    {
        var monitorBounds = new Rect(0, 0, 1920, 1080);
        var windowBounds = new Rect(0, 0, 1920, 1040);

        Assert.False(Win32Helper.IsWindowBoundsFullscreen(windowBounds, monitorBounds));
    }
}
