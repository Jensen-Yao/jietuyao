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
    public static Bitmap CaptureVirtualDesktop(Rect virtualBounds, bool includeCursor = false)
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
        if (includeCursor)
            DrawMouseCursor(graphics, virtualBounds);
        return bitmap;
    }

    /// <summary>把当前鼠标指针（含热点偏移）画入截图（吸收自 SnipTrans CaptureService）</summary>
    private static void DrawMouseCursor(Graphics graphics, Rect virtualBounds)
    {
        try
        {
            var info = new NativeCursor.CURSORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeCursor.CURSORINFO>() };
            if (!NativeCursor.GetCursorInfo(ref info) || info.flags != NativeCursor.CURSOR_SHOWING || info.hCursor == 0)
                return;

            var iconInfo = default(NativeCursor.ICONINFO);
            if (!NativeCursor.GetIconInfo(info.hCursor, ref iconInfo))
                return;

            var x = info.ptScreenPos.X - iconInfo.xHotspot - (int)virtualBounds.Left;
            var y = info.ptScreenPos.Y - iconInfo.yHotspot - (int)virtualBounds.Top;
            var hdc = graphics.GetHdc();
            try
            {
                NativeCursor.DrawIconEx(hdc, x, y, info.hCursor, 0, 0, 0, 0, 0x3);
            }
            finally
            {
                graphics.ReleaseHdc();
            }
            if (iconInfo.hbmMask != 0) NativeCursor.DeleteObject(iconInfo.hbmMask);
            if (iconInfo.hbmColor != 0) NativeCursor.DeleteObject(iconInfo.hbmColor);
        }
        catch
        {
            // 指针绘制失败不影响截图本体
        }
    }

    /// <summary>指针绘制专用友好签名（规避 CsWin32 原始指针形状）</summary>
    private static class NativeCursor
    {
        public const int CURSOR_SHOWING = 1;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public nint hCursor;
            public POINT ptScreenPos;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public nint hbmMask;
            public nint hbmColor;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool GetCursorInfo(ref CURSORINFO pci);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool GetIconInfo(nint hIcon, ref ICONINFO piconinfo);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool DrawIconEx(nint hdc, int xLeft, int yTop, nint hIcon, int cxWidth, int cyHeight, int istepIfAniCur, nint hbrFlickerFreeDraw, int diFlags);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool DeleteObject(nint hObject);
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
