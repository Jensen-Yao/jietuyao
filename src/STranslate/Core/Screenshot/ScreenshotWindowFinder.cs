using System.Runtime.InteropServices;
using System.Windows;
using Windows.Win32.Foundation;
using Point = System.Windows.Point;

namespace STranslate.Core;

/// <summary>
/// 智能选区：枚举可见顶层窗口矩形（物理像素），悬停吸附。
/// 全部使用自声明 nint 型 P/Invoke，避免 CsWin32 生成签名歧义。
/// </summary>
public class ScreenshotWindowFinder
{
    private readonly List<Rect> _windowRects = [];

    /// <summary>
    /// 需要排除的窗口谓词（编辑器自身窗口等），参数为窗口句柄
    /// </summary>
    public Predicate<nint> Exclude { get; set; } = _ => false;

    /// <summary>
    /// 已捕获的窗口矩形（物理像素，已裁剪到虚拟桌面内）
    /// </summary>
    public IReadOnlyList<Rect> WindowRects => _windowRects;

    /// <summary>
    /// 枚举当前可见顶层窗口。应在编辑器窗口创建前调用。
    /// </summary>
    public unsafe void Snapshot(Rect virtualBounds)
    {
        _windowRects.Clear();
        var currentPid = (uint)Environment.ProcessId;
        NativeMethods.EnumWindows((hwnd, lParam) =>
        {
            try
            {
                if (!NativeMethods.IsWindowVisible(hwnd) || Exclude(hwnd))
                    return true;

                // 排除本进程窗口（编辑器窗口、贴图等）
                NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
                if (pid == currentPid)
                    return true;

                // 排除最小化窗口
                if (NativeMethods.IsIconic(hwnd))
                    return true;

                // 排除 UWP 挂起窗口（Cloaked）
                var cloaked = 0;
                NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_CLOAKED, &cloaked, sizeof(int));
                if (cloaked != 0)
                    return true;

                var rect = GetWindowBounds(hwnd);
                if (rect.IsEmpty || rect.Width < 40 || rect.Height < 40)
                    return true;

                // 裁剪到虚拟桌面
                var clipped = Rect.Intersect(rect, virtualBounds);
                if (clipped.IsEmpty || clipped.Width < 20 || clipped.Height < 20)
                    return true;

                _windowRects.Add(clipped);
            }
            catch
            {
                // 单个窗口枚举失败不影响整体
            }
            return true;
        }, 0);
    }

    /// <summary>
    /// 窗口可见边界：优先 DWM 扩展边界（不含不可见阴影）
    /// </summary>
    public static unsafe Rect GetWindowBounds(nint hwnd)
    {
        var rect = default(RECT);
        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, &rect, sizeof(RECT)) == 0)
            return new Rect(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);

        if (NativeMethods.GetWindowRect(hwnd, out rect))
            return new Rect(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);

        return Rect.Empty;
    }

    /// <summary>
    /// 命中测试：返回包含指定点的最小窗口矩形（越小说明越"近"）
    /// </summary>
    public Rect? HitTest(Point point)
    {
        Rect? best = null;
        foreach (var rect in _windowRects)
        {
            if (rect.Contains(point))
            {
                if (best == null || rect.Width * rect.Height < best.Value.Width * best.Value.Height)
                    best = rect;
            }
        }
        return best;
    }

    /// <summary>nint 型 Win32 声明（智能选区专用）</summary>
    private static class NativeMethods
    {
        public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        public const int DWMWA_CLOAKED = 14;

        [DllImport("dwmapi.dll")]
        public static extern unsafe int DwmGetWindowAttribute(nint hwnd, int attribute, void* value, int size);

        public delegate bool EnumWindowsProc(nint hwnd, nint lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc proc, nint lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(nint hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(nint hwnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(nint hwnd, out RECT rect);
    }
}
