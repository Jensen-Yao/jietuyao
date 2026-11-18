using System.Drawing;
using System.IO;
using System.Windows;
using Gma.System.MouseKeyHook;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.DependencyInjection;
using STranslate.Helpers;
using Size = System.Drawing.Size;

namespace STranslate.Core;

/// <summary>
/// GIF 录制控制器：后台线程 BitBlt 连拍 → JPEG 帧缓存 → GDI+ 动画 GIF 导出。
/// 含鼠标点击/滚轮特效事件记录（v1 cursor_overlay 移植）与时间线分段标注。
/// </summary>
public sealed class GifRecorderController : IDisposable
{
    private static readonly Lazy<GifRecorderController> _instance = new(() =>
        new GifRecorderController(Ioc.Default.GetService<Microsoft.Extensions.Logging.ILogger<GifRecorderController>>(),
            Ioc.Default.GetRequiredService<Settings>()));
    public static GifRecorderController Instance => _instance.Value;

    private readonly ILogger<GifRecorderController>? _logger;
    private readonly Settings _settings;

    private Thread? _thread;
    private volatile bool _running;
    private volatile bool _paused;
    private Rect _region;
    private int _fps = 15;
    private DateTime _startTime;
    private TimeSpan _pausedTotal;
    private DateTime _pauseMark;

    private GifFrameBuffer? _buffer;
    private Views.ScrollOverlayWindow? _overlay;
    private Views.GifToolbarWindow? _toolbar;
    private Views.GifAnnotationWindow? _annotation;

    // 鼠标特效事件（时间线）
    private readonly List<MouseEffectEvent> _mouseEvents = [];
    private IKeyboardMouseEvents? _mouseHook;
    private long _lastWheelEventMs = long.MinValue;
    private int _lastWheelDirection;

    /// <summary>统一录制时钟（毫秒，暂停不计时）</summary>
    private long NowMs => (long)(((_paused ? _pauseMark : DateTime.Now) - _startTime - _pausedTotal).TotalMilliseconds);

    /// <summary>录制中标注开关</summary>
    public void ToggleAnnotate()
    {
        if (!_running)
            return;
        _annotation ??= new Views.GifAnnotationWindow(_region, () => NowMs);
        var active = !_annotation.IsHitTestVisible;
        _annotation.SetAnnotateMode(active);
        StateChanged?.Invoke();
    }

    public bool IsAnnotating => _annotation?.IsHitTestVisible == true;

    /// <summary>录制状态变化（时长/帧数/暂停）</summary>
    public event Action? StateChanged;

    public bool IsRunning => _running;
    public bool IsPaused => _paused;

    internal IReadOnlyList<MouseEffectEvent> MouseEvents => _mouseEvents;

    private GifRecorderController(ILogger<GifRecorderController>? logger, Settings settings)
    {
        _logger = logger;
        _settings = settings;
    }

    public void Begin(Rect physicalRegion, int fps = 15)
    {
        if (_running)
            return;
        _running = true;
        _paused = false;
        _region = physicalRegion;
        _fps = Math.Clamp(fps, 5, 24);
        _startTime = DateTime.Now;
        _pausedTotal = TimeSpan.Zero;
        _mouseEvents.Clear();

        var width = Math.Max(1, (int)Math.Round(physicalRegion.Width));
        var height = Math.Max(1, (int)Math.Round(physicalRegion.Height));
        _buffer = new GifFrameBuffer(width, height);

        _overlay = new Views.ScrollOverlayWindow(physicalRegion);
        _overlay.ShowNoActivate();
        _toolbar = new Views.GifToolbarWindow(this, physicalRegion);
        _toolbar.Show();
        _toolbar.Activate();

        // 鼠标特效钩子：点击/滚轮（区域物理坐标换算）
        _mouseHook = Hook.GlobalEvents();
        _mouseHook.MouseDown += OnHookMouseDown;
        _mouseHook.MouseWheelExt += OnHookMouseWheel;

        _encodeQueue = new System.Collections.Concurrent.BlockingCollection<(Bitmap, long)>(30);
        _thread = new Thread(CaptureLoop) { IsBackground = true, Name = "jietuyao.gif" };
        _thread.Start();
        _encoderThread = new Thread(EncodeLoop) { IsBackground = true, Name = "jietuyao.gif.enc" };
        _encoderThread.Start();
    }

    // 采集/编码管线（解耦：BitBlt 节奏不受 JPEG 编码耗时影响）
    private System.Collections.Concurrent.BlockingCollection<(Bitmap Frame, long Ts)>? _encodeQueue;
    private readonly System.Collections.Concurrent.ConcurrentBag<Bitmap> _framePool = new();
    private Thread? _encoderThread;

    private void CaptureLoop()
    {
        var interval = 1000.0 / _fps;
        var width = Math.Max(1, (int)Math.Round(_region.Width));
        var height = Math.Max(1, (int)Math.Round(_region.Height));

        while (_running)
        {
            var tick = DateTime.UtcNow;
            if (!_paused && _buffer != null && _encodeQueue is { IsAddingCompleted: false } queue)
            {
                try
                {
                    if (!_framePool.TryTake(out var frame))
                        frame = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    using (var g = Graphics.FromImage(frame))
                    {
                        g.CopyFromScreen((int)_region.X, (int)_region.Y, 0, 0, new Size(width, height),
                            System.Drawing.CopyPixelOperation.SourceCopy);
                    }
                    if (!queue.TryAdd((frame, NowMs)))
                        _framePool.Add(frame); // 队列满（编码跟不上）：丢弃本帧并回收缓冲
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "GIF 采集帧失败");
                }
            }
            var elapsed = (DateTime.UtcNow - tick).TotalMilliseconds;
            var sleep = (int)Math.Max(5, interval - elapsed);
            Thread.Sleep(sleep);
        }
    }

    /// <summary>编码线程：JPEG 压缩入库，归还缓冲到池</summary>
    private void EncodeLoop()
    {
        var queue = _encodeQueue;
        if (queue == null)
            return;
        try
        {
            foreach (var (frame, ts) in queue.GetConsumingEnumerable())
            {
                try
                {
                    _buffer?.Add(frame, ts);
                    NotifyState();
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "GIF 编码帧失败");
                }
                finally
                {
                    _framePool.Add(frame);
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // 停止时集合先释放
        }
    }

    private void NotifyState() => StateChanged?.Invoke();

    // ===== 鼠标特效事件记录 =====

    private void OnHookMouseDown(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        if (!_running)
            return;
        var kind = e.Button switch
        {
            System.Windows.Forms.MouseButtons.Left => MouseEffectKind.LeftClick,
            System.Windows.Forms.MouseButtons.Right => MouseEffectKind.RightClick,
            _ => (MouseEffectKind?)null,
        };
        if (kind == null)
            return;
        RecordMouseEvent(kind.Value, e.X, e.Y);
    }

    private void OnHookMouseWheel(object? sender, MouseEventExtArgs e)
    {
        if (!_running || e.Delta == 0)
            return;
        RecordMouseEvent(e.Delta > 0 ? MouseEffectKind.WheelUp : MouseEffectKind.WheelDown, e.X, e.Y, throttleWheel: true);
    }

    private void RecordMouseEvent(MouseEffectKind kind, int screenX, int screenY, bool throttleWheel = false)
    {
        var x = (int)(screenX - _region.X);
        var y = (int)(screenY - _region.Y);
        // 仅记录区域附近（含边距 60px）的事件
        if (x < -60 || y < -60 || x > _region.Width + 60 || y > _region.Height + 60)
            return;

        lock (_mouseEvents)
        {
            var now = NowMs;
            if (throttleWheel)
            {
                var dir = kind == MouseEffectKind.WheelUp ? 1 : -1;
                if (dir == _lastWheelDirection && now - _lastWheelEventMs < 90)
                    return; // 同向滚轮节流
                _lastWheelDirection = dir;
                _lastWheelEventMs = now;
            }
            _mouseEvents.Add(new MouseEffectEvent(now, Math.Clamp(x, 0, (int)_region.Width), Math.Clamp(y, 0, (int)_region.Height), kind));
        }
    }

    /// <summary>导出用鼠标事件快照（线程安全）</summary>
    internal IReadOnlyList<MouseEffectEvent> SnapshotMouseEvents()
    {
        lock (_mouseEvents)
        {
            return [.. _mouseEvents];
        }
    }

    public void Pause()
    {
        if (!_running || _paused)
            return;
        _paused = true;
        _pauseMark = DateTime.Now;
        StateChanged?.Invoke();
    }

    public void Resume()
    {
        if (!_running || !_paused)
            return;
        _pausedTotal += DateTime.Now - _pauseMark;
        _paused = false;
        StateChanged?.Invoke();
    }

    public void SetFps(int fps)
    {
        _fps = Math.Clamp(fps, 5, 24);
        StateChanged?.Invoke();
    }

    public int Fps => _fps;
    public int FrameCount => _buffer?.Count ?? 0;
    public TimeSpan Elapsed => (_paused ? _pauseMark : DateTime.Now) - _startTime - _pausedTotal;

    /// <summary>完成：停止采集 → 回放裁剪窗 → 导出 GIF（进度窗）</summary>
    public void Finish()
    {
        if (!_running)
            return;
        _running = false;
        StopPipeline();
        var buffer = _buffer;
        _buffer = null;
        var mouseEvents = SnapshotMouseEvents();

        // 时间线分段标注：按笔画时间生成累积叠加变体（UI 线程渲染 WPF 笔迹）
        List<(long TimeMs, System.Drawing.Bitmap Overlay)>? timelineOverlays = null;
        if (_annotation != null)
        {
            var source = System.Windows.PresentationSource.FromVisual(_annotation);
            var scale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            timelineOverlays = _annotation.BuildTimelineOverlays(scale);
            _annotation.Close();
            _annotation = null;
        }
        CleanupWindows();

        if (buffer == null || buffer.Count == 0)
        {
            buffer?.Dispose();
            DisposeOverlays(timelineOverlays);
            return;
        }

        var trim = new Views.GifPlaybackTrimWindow(buffer, _fps, _region, mouseEvents, timelineOverlays);
        trim.ExportConfirmed += (start, end) =>
        {
            trim.Close();
            _ = ExportAsync(buffer, timelineOverlays, mouseEvents, start, end);
        };
        trim.Show();
    }

    private static void DisposeOverlays(List<(long TimeMs, System.Drawing.Bitmap Overlay)>? overlays)
    {
        if (overlays == null)
            return;
        foreach (var (_, bitmap) in overlays)
            bitmap.Dispose();
    }

    private bool _exportStarted;

    private async Task ExportAsync(GifFrameBuffer buffer,
        List<(long TimeMs, System.Drawing.Bitmap Overlay)>? timelineOverlays,
        IReadOnlyList<MouseEffectEvent> mouseEvents, int start, int end)
    {
        var suggested = $"jietuyao_{DateTime.Now:yyyyMMdd_HHmmss}.gif";
        var path = ScreenshotOutput.SaveGifFile(suggested, _settings.LastScreenshotSaveDir);
        if (path == null)
        {
            buffer.Dispose();
            DisposeOverlays(timelineOverlays);
            return;
        }
        _settings.LastScreenshotSaveDir = Path.GetDirectoryName(path) ?? string.Empty;

        _exportStarted = true;
        var progress = new Views.GifExportProgressWindow();
        progress.Show();
        try
        {
            var delayCs = Math.Max(2, (int)Math.Round(100.0 / _fps));
            await Task.Run(() => buffer.ExportGif(path, delayCs, null, start, end, mouseEvents, timelineOverlays, p =>
                progress.ReportProgress(p)));
            progress.Complete(path);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "GIF 导出失败");
            progress.Fail(ex.Message);
        }
        finally
        {
            buffer.Dispose();
            DisposeOverlays(timelineOverlays);
            _exportStarted = false;
        }
    }

    public void Cancel()
    {
        if (!_running)
            return;
        _running = false;
        StopPipeline();
        _buffer?.Dispose();
        _buffer = null;
        _annotation?.Close();
        _annotation = null;
        CleanupWindows();
    }

    /// <summary>停止采集/编码管线并释放缓冲池</summary>
    private void StopPipeline()
    {
        try { _encodeQueue?.CompleteAdding(); } catch { /* 已完成 */ }
        _thread?.Join(1000);
        _encoderThread?.Join(3000);
        while (_framePool.TryTake(out var pooled))
            pooled.Dispose();
        _encodeQueue?.Dispose();
        _encodeQueue = null;
    }

    private void CleanupWindows()
    {
        if (_mouseHook != null)
        {
            _mouseHook.MouseDown -= OnHookMouseDown;
            _mouseHook.MouseWheelExt -= OnHookMouseWheel;
            _mouseHook.Dispose();
            _mouseHook = null;
        }
        _overlay?.Close();
        _overlay = null;
        _toolbar?.Close();
        _toolbar = null;
    }

    public void Dispose() => Cancel();
}
