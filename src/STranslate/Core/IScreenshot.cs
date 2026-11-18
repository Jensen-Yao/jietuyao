using System.Drawing;

namespace STranslate.Core;

public sealed class ScreenshotCaptureResult(Bitmap bitmap, Rectangle? physicalBounds) : IDisposable
{
    private bool _ownsBitmap = true;

    public Bitmap Bitmap { get; } = bitmap;

    /// <summary>
    /// 截图选区在虚拟屏幕中的物理像素坐标。定位失败时为空。
    /// </summary>
    public Rectangle? PhysicalBounds { get; } = physicalBounds;

    public Bitmap DetachBitmap()
    {
        _ownsBitmap = false;
        return Bitmap;
    }

    public void Dispose()
    {
        if (_ownsBitmap)
            Bitmap.Dispose();
    }
}

/// <summary>
/// 截图接口，可以不要定义接口，懒得删了
/// </summary>
public interface IScreenshot
{
    /// <summary>
    /// 获取截图
    /// </summary>
    /// <returns></returns>
    Bitmap? GetScreenshot();
    
    /// <summary>
    /// 异步获取截图
    /// </summary>
    /// <returns></returns>
    Task<Bitmap?> GetScreenshotAsync();

    /// <summary>
    /// 异步获取截图和截图选区坐标。
    /// </summary>
    /// <returns></returns>
    Task<ScreenshotCaptureResult?> GetScreenshotCaptureAsync();
}
