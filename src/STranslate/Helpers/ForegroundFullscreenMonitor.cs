using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace STranslate.Helpers;

/// <summary>
/// 监听前台窗口的全屏状态变化。
/// </summary>
internal sealed class ForegroundFullscreenMonitor : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectLocationChange = 0x800B;
    private const uint WineventOutOfContext = 0x0000;
    private const int ObjidWindow = 0;

    private readonly Action<bool> _fullscreenChanged;
    private readonly DispatcherTimer _fallbackTimer;
    private readonly WinEventDelegate _winEventCallback;
    private nint _foregroundHook;
    private nint _locationChangeHook;
    private bool? _lastFullscreenState;
    private bool _isRunning;
    private bool _disposed;

    internal ForegroundFullscreenMonitor(Action<bool> fullscreenChanged)
    {
        _fullscreenChanged = fullscreenChanged;
        _winEventCallback = OnWinEvent;
        _fallbackTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _fallbackTimer.Tick += OnFallbackTimerTick;
    }

    internal void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_isRunning)
        {
            EvaluateFullscreenState();
            return;
        }

        _isRunning = true;
        _lastFullscreenState = null;
        _foregroundHook = SetWinEventHook(
            EventSystemForeground,
            EventSystemForeground,
            nint.Zero,
            _winEventCallback,
            0,
            0,
            WineventOutOfContext);
        _locationChangeHook = SetWinEventHook(
            EventObjectLocationChange,
            EventObjectLocationChange,
            nint.Zero,
            _winEventCallback,
            0,
            0,
            WineventOutOfContext);
        _fallbackTimer.Start();
        EvaluateFullscreenState();
    }

    internal void Stop()
    {
        if (!_isRunning)
            return;

        _isRunning = false;
        _lastFullscreenState = null;
        _fallbackTimer.Stop();
        ReleaseHook(ref _foregroundHook);
        ReleaseHook(ref _locationChangeHook);
    }

    private void OnWinEvent(
        nint winEventHook,
        uint eventType,
        nint hwnd,
        int idObject,
        int idChild,
        uint eventThread,
        uint eventTime)
    {
        if (!_isRunning)
            return;

        if (eventType == EventObjectLocationChange &&
            (idObject != ObjidWindow || hwnd != Win32Helper.GetForegroundWindow()))
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted)
            return;

        if (dispatcher.CheckAccess())
            EvaluateFullscreenState();
        else
            _ = dispatcher.BeginInvoke(DispatcherPriority.Send, EvaluateFullscreenState);
    }

    private void OnFallbackTimerTick(object? sender, EventArgs e) => EvaluateFullscreenState();

    private void EvaluateFullscreenState()
    {
        if (!_isRunning)
            return;

        bool isFullscreen;
        try
        {
            isFullscreen = Win32Helper.IsForegroundWindowFullscreen();
        }
        catch
        {
            // 前台窗口可能在查询过程中销毁；保留上一次状态并等待下一次事件。
            return;
        }

        if (_lastFullscreenState == isFullscreen)
            return;

        _lastFullscreenState = isFullscreen;
        _fullscreenChanged(isFullscreen);
    }

    private static void ReleaseHook(ref nint hook)
    {
        if (hook == nint.Zero)
            return;

        _ = UnhookWinEvent(hook);
        hook = nint.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _fallbackTimer.Tick -= OnFallbackTimerTick;
        _disposed = true;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void WinEventDelegate(
        nint winEventHook,
        uint eventType,
        nint hwnd,
        int idObject,
        int idChild,
        uint eventThread,
        uint eventTime);

    [DllImport("user32.dll")]
    private static extern nint SetWinEventHook(
        uint eventMin,
        uint eventMax,
        nint eventHookModule,
        WinEventDelegate winEventProc,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(nint winEventHook);
}
