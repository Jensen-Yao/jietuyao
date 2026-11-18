using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace STranslate.Helpers;

/// <summary>
/// 用于设置和恢复系统光标
/// </summary>
public static class CursorHelper
{
    #region Constants
    private const uint OCR_NORMAL = 32512;
    private const uint OCR_IBEAM = 32513;

    private static readonly string[] ErrorCursorPaths =
    [
        @"C:\Windows\Cursors\aero_unavail.cur",
        @"C:\Windows\Cursors\aero_unavail_l.cur",
        @"C:\Windows\Cursors\aero_unavail_xl.cur"
    ];
    #endregion

    #region Fields
    private static readonly object _lock = new();
    // 引用计数用于处理并发静默任务，只有最后一次 Restore 才会真正恢复系统光标。
    private static int _executionRefCount;
    private static int? _cachedCursorSize; // 缓存光标尺寸

    private static HCURSOR _originalNormalCursor;
    private static HCURSOR _originalBeamCursor;
    private static DestroyCursorSafeHandle? _customNormalCursor;
    private static DestroyCursorSafeHandle? _customBeamCursor;
    private static DestroyCursorSafeHandle? _errorNormalCursor;
    private static DestroyCursorSafeHandle? _errorBeamCursor;
    #endregion

    static CursorHelper()
    {
        InitializeDpiAwareness();
    }

    #region Public Methods
    /// <summary>
    /// 执行光标替换操作
    /// </summary>
    /// <exception cref="InvalidOperationException">光标操作失败时抛出</exception>
    public static void Execute()
    {
        lock (_lock)
        {
            if (_executionRefCount > 0)
            {
                _executionRefCount++;
                return;
            }

            try
            {
                SaveOriginalCursors();
                LoadAndSetCustomCursors();
                _executionRefCount = 1;
            }
            catch (Exception ex)
            {
                RestoreCore();
                throw new InvalidOperationException("Failed to execute cursor replacement", ex);
            }
        }
    }

    /// <summary>
    /// 设置错误状态光标
    /// </summary>
    /// <exception cref="InvalidOperationException">设置错误光标失败时抛出</exception>
    public static void Error()
    {
        lock (_lock)
        {
            if (_executionRefCount <= 0)
                return;

            try
            {
                var cursorPath = GetErrorCursorPath();
                SetErrorCursors(cursorPath);
            }
            catch (Exception ex)
            {
                RestoreCore();
                throw new InvalidOperationException("Failed to set error cursors", ex);
            }
        }
    }

    /// <summary>
    /// 恢复原始光标
    /// </summary>
    public static void Restore()
    {
        lock (_lock)
        {
            if (_executionRefCount <= 0)
                return;

            _executionRefCount--;
            if (_executionRefCount > 0)
                return;

            try
            {
                RestoreCore();
            }
            finally
            {
                _executionRefCount = 0;
            }
        }
    }

    /// <summary>
    /// 手动清理所有光标资源（可选调用）
    /// </summary>
    public static void Cleanup()
    {
        lock (_lock)
        {
            try
            {
                RestoreCore();
            }
            finally
            {
                _executionRefCount = 0;
            }
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 恢复系统光标并清理句柄。
    /// </summary>
    private static void RestoreCore()
    {
        RestoreOriginalCursors();
        CleanupCustomCursors();
        UpdateSystemCursors();
    }

    /// <summary>
    /// 初始化 DPI 感知，兼容不同 Windows 版本
    /// </summary>
    private static void InitializeDpiAwareness()
    {
        try
        {
            if (IsWindows81OrGreater())
            {
#pragma warning disable CA1416 // 验证平台兼容性
                PInvoke.SetProcessDpiAwareness(PROCESS_DPI_AWARENESS.PROCESS_PER_MONITOR_DPI_AWARE);
#pragma warning restore CA1416 // 验证平台兼容性
            }
            else
            {
                SetProcessDpiAware();
            }
        }
        catch (COMException)
        {
            // DPI awareness already set, ignore
        }
        catch
        {
            // If all methods fail, continue silently
        }
    }

    /// <summary>
    /// 检查是否为 Windows 8.1 或更高版本
    /// </summary>
    private static bool IsWindows81OrGreater()
    {
        var version = Environment.OSVersion.Version;
        return version.Major > 6 || (version.Major == 6 && version.Minor >= 3);
    }

    /// <summary>
    /// Windows 7/8 的 DPI 感知设置
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAware();

    /// <summary>
    /// 获取光标尺寸（带缓存）
    /// </summary>
    private static int GetCursorSize()
    {
        if (_cachedCursorSize.HasValue)
            return _cachedCursorSize.Value;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors");
            _cachedCursorSize = key?.GetValue("CursorBaseSize") is int size ? size : 32;
            return _cachedCursorSize.Value;
        }
        catch
        {
            _cachedCursorSize = 32;
            return 32;
        }
    }

    /// <summary>
    /// 保存原始光标
    /// </summary>
    private static unsafe void SaveOriginalCursors()
    {
        var normalCursor = LoadSystemCursor(OCR_NORMAL);
        var beamCursor = LoadSystemCursor(OCR_IBEAM);

        ThrowIfInvalidHandle(normalCursor, "Failed to load original normal cursor");
        ThrowIfInvalidHandle(beamCursor, "Failed to load original beam cursor");

        _originalNormalCursor = new HCURSOR(PInvoke.CopyIcon(new HICON(normalCursor.Value)).Value);
        _originalBeamCursor = new HCURSOR(PInvoke.CopyIcon(new HICON(beamCursor.Value)).Value);

        ThrowIfInvalidHandle(_originalNormalCursor, "Failed to copy original normal cursor");
        ThrowIfInvalidHandle(_originalBeamCursor, "Failed to copy original beam cursor");
    }

    /// <summary>
    /// 加载系统光标
    /// </summary>
    private static unsafe HANDLE LoadSystemCursor(uint cursorId)
    {
        return PInvoke.LoadImage(
            HINSTANCE.Null,
            new PCWSTR((char*)cursorId),
            GDI_IMAGE_TYPE.IMAGE_CURSOR,
            0, 0,
            IMAGE_FLAGS.LR_SHARED);
    }

    /// <summary>
    /// 加载并设置自定义光标
    /// </summary>
    private static void LoadAndSetCustomCursors()
    {
        var tempFilePath = Path.GetTempFileName();
        try
        {
            ExtractCursorResource(tempFilePath);
            SetCustomCursors(tempFilePath);
        }
        finally
        {
            SafeDeleteFile(tempFilePath);
        }
    }

    /// <summary>
    /// 安全删除文件
    /// </summary>
    private static void SafeDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
            // Ignore deletion errors
        }
    }

    /// <summary>
    /// 提取光标资源到临时文件
    /// </summary>
    private static void ExtractCursorResource(string tempFilePath)
    {
        var cursorSize = GetCursorSize() switch
        {
            128 => "128",
            96 => "96",
            64 => "64",
            48 => "48",
            _ => "32"
        };

        var cursorPath = $"pack://application:,,,/STranslate;Component/Resources/Working_{cursorSize}x.ani";

        using var resourceStream = Application.GetResourceStream(new Uri(cursorPath, UriKind.Absolute))?.Stream
            ?? throw new InvalidOperationException($"Could not load cursor resource: {cursorPath}");

        using var fileStream = File.Create(tempFilePath);
        resourceStream.CopyTo(fileStream);
    }

    /// <summary>
    /// 设置自定义光标
    /// </summary>
    private static void SetCustomCursors(string cursorPath)
    {
        _customNormalCursor = PInvoke.LoadCursorFromFile(cursorPath);
        _customBeamCursor = PInvoke.LoadCursorFromFile(cursorPath);

        ThrowIfInvalidHandle(_customNormalCursor, "Failed to load custom normal cursor");
        ThrowIfInvalidHandle(_customBeamCursor, "Failed to load custom beam cursor");

        SetSystemCursor(_customNormalCursor, SYSTEM_CURSOR_ID.OCR_NORMAL, "normal cursor");
        SetSystemCursor(_customBeamCursor, SYSTEM_CURSOR_ID.OCR_IBEAM, "beam cursor");
    }

    /// <summary>
    /// 设置系统光标的辅助方法
    /// </summary>
    private static void SetSystemCursor(DestroyCursorSafeHandle cursor, SYSTEM_CURSOR_ID cursorId, string cursorType)
    {
        if (!PInvoke.SetSystemCursor(cursor, cursorId))
            throw new InvalidOperationException($"Failed to set {cursorType}: {GetLastWin32Error()}");
    }

    /// <summary>
    /// 获取错误光标路径
    /// </summary>
    private static string GetErrorCursorPath()
    {
        return GetCursorSize() switch
        {
            32 => ErrorCursorPaths[0],
            48 => ErrorCursorPaths[1],
            _ => ErrorCursorPaths[2]
        };
    }

    /// <summary>
    /// 设置错误状态光标
    /// </summary>
    private static void SetErrorCursors(string path)
    {
        _errorNormalCursor = PInvoke.LoadCursorFromFile(path);
        _errorBeamCursor = PInvoke.LoadCursorFromFile(path);

        ThrowIfInvalidHandle(_errorNormalCursor, "Failed to load error normal cursor");
        ThrowIfInvalidHandle(_errorBeamCursor, "Failed to load error beam cursor");

        var normalSuccess = PInvoke.SetSystemCursor(_errorNormalCursor, SYSTEM_CURSOR_ID.OCR_NORMAL);
        var beamSuccess = PInvoke.SetSystemCursor(_errorBeamCursor, SYSTEM_CURSOR_ID.OCR_IBEAM);

        if (!normalSuccess || !beamSuccess)
            throw new InvalidOperationException($"Failed to set error cursors: {GetLastWin32Error()}");
    }

    /// <summary>
    /// 恢复原始光标
    /// </summary>
    private static void RestoreOriginalCursors()
    {
        RestoreCursor(ref _originalNormalCursor, SYSTEM_CURSOR_ID.OCR_NORMAL);
        RestoreCursor(ref _originalBeamCursor, SYSTEM_CURSOR_ID.OCR_IBEAM);
    }

    /// <summary>
    /// 恢复单个光标的辅助方法
    /// </summary>
    private static void RestoreCursor(ref HCURSOR cursor, SYSTEM_CURSOR_ID cursorId)
    {
        if (cursor.IsNull) return;

        PInvoke.SetSystemCursor(cursor, cursorId);
        PInvoke.DestroyCursor(cursor);
        cursor = HCURSOR.Null;
    }

    /// <summary>
    /// 清理自定义光标资源
    /// </summary>
    private static void CleanupCustomCursors()
    {
        _customNormalCursor?.Dispose();
        _customNormalCursor = null;

        _customBeamCursor?.Dispose();
        _customBeamCursor = null;

        _errorNormalCursor?.Dispose();
        _errorNormalCursor = null;

        _errorBeamCursor?.Dispose();
        _errorBeamCursor = null;
    }

    /// <summary>
    /// 更新系统光标
    /// </summary>
    private static unsafe void UpdateSystemCursors()
    {
        PInvoke.SystemParametersInfo(
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETCURSORS,
            0,
            null,
            SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS.SPIF_UPDATEINIFILE |
            SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS.SPIF_SENDCHANGE);
    }

    /// <summary>
    /// 验证句柄有效性
    /// </summary>
    private static void ThrowIfInvalidHandle(HANDLE handle, string message)
    {
        if (handle.IsNull)
            throw new InvalidOperationException($"{message}: {GetLastWin32Error()}");
    }

    /// <summary>
    /// 验证光标句柄有效性
    /// </summary>
    private static void ThrowIfInvalidHandle(HCURSOR handle, string message)
    {
        if (handle.IsNull)
            throw new InvalidOperationException($"{message}: {GetLastWin32Error()}");
    }

    /// <summary>
    /// 验证安全句柄有效性
    /// </summary>
    private static void ThrowIfInvalidHandle(DestroyCursorSafeHandle? handle, string message)
    {
        if (handle == null || handle.IsInvalid)
            throw new InvalidOperationException($"{message}: {GetLastWin32Error()}");
    }

    /// <summary>
    /// 获取最后的 Win32 错误
    /// </summary>
    private static int GetLastWin32Error()
    {
        var error = Marshal.GetLastPInvokeError();
        return error != 0 ? error : Marshal.GetLastWin32Error();
    }
    #endregion
}
