using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using STranslate.Helpers;
using Size = System.Drawing.Size;

namespace STranslate.Core;

/// <summary>
/// 多屏冻结捕获：一次 BitBlt 冻结整个虚拟桌面，供截图编辑器交互使用
/// </summary>
public static class ScreenshotCapturer
{
    /// <summary>
    /// 枚举物理显示器（含 DPI）
    /// </summary>
    public static List<ScreenshotMonitor> GetMonitors() =>
        MonitorInfo.GetDisplayMonitors()
            .Select(m => new ScreenshotMonitor
            {
                Bounds = m.Bounds,
                IsPrimary = m.IsPrimary,
                DpiScale = m.DpiScale,
                DeviceName = m.Name,
            })
            .ToList();

    /// <summary>
    /// 虚拟桌面物理范围
    /// </summary>
    public static Rect GetVirtualBounds(IEnumerable<ScreenshotMonitor> monitors)
    {
        var left = int.MaxValue;
        var top = int.MaxValue;
        var right = int.MinValue;
        var bottom = int.MinValue;
        foreach (var m in monitors)
        {
            left = Math.Min(left, (int)m.Bounds.Left);
            top = Math.Min(top, (int)m.Bounds.Top);
            right = Math.Max(right, (int)m.Bounds.Right);
            bottom = Math.Max(bottom, (int)m.Bounds.Bottom);
        }
        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// 冻结整个虚拟桌面为一张位图（Format32bppPArgb，物理像素）
    /// </summary>
    public static Bitmap CaptureVirtualDesktop(Rect virtualBounds)
    {
        var width = (int)virtualBounds.Width;
        var height = (int)virtualBounds.Height;
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        // 逐屏拷贝，避免超大虚拟桌面单次 BitBlt 边界问题
        foreach (var monitor in MonitorInfo.GetDisplayMonitors())
        {
            var b = monitor.Bounds;
            var w = (int)b.Width;
            var h = (int)b.Height;
            if (w <= 0 || h <= 0)
                continue;
            graphics.CopyFromScreen((int)b.Left, (int)b.Top, (int)(b.Left - virtualBounds.Left), (int)(b.Top - virtualBounds.Top),
                new Size(w, h), CopyPixelOperation.SourceCopy);
        }
        return bitmap;
    }

    /// <summary>
    /// GDI 位图转 WPF BitmapSource（冻结、无额外拷贝延迟）
    /// </summary>
    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        var bitmapData = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppPArgb);
        try
        {
            var source = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                96, 96,
                System.Windows.Media.PixelFormats.Pbgra32, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);
            source.Freeze();
            return source;
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }
    }

    /// <summary>
    /// 读取位图中指定物理像素颜色
    /// </summary>
    public static Color GetPixel(Bitmap bitmap, int x, int y)
    {
        if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height)
            return Color.Transparent;
        return bitmap.GetPixel(x, y);
    }

    /// <summary>
    /// WPF BitmapSource 转 GDI 位图（Pbgra32 → PArgb）
    /// </summary>
    public static Bitmap ToGdiBitmap(System.Windows.Media.Imaging.BitmapSource source)
    {
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var bytes = new byte[width * height * 4];
        source.CopyPixels(System.Windows.Int32Rect.Empty, bytes, width * 4, 0);
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return bitmap;
    }
}
