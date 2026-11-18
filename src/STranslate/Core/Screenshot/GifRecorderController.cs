using System.Drawing;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.DependencyInjection;
using STranslate.Helpers;
using Size = System.Drawing.Size;

namespace STranslate.Core;

/// <summary>
/// GIF 录制控制器：后台线程 BitBlt 连拍 → JPEG 帧缓存 → GDI+ 动画 GIF 导出
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

    /// <summary>录制状态变化（时长/帧数/暂停）</summary>
    public event Action? StateChanged;

    public bool IsRunning => _running;
    public bool IsPaused => _paused;

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

        var width = Math.Max(1, (int)Math.Round(physicalRegion.Width));
        var height = Math.Max(1, (int)Math.Round(physicalRegion.Height));
        _buffer = new GifFrameBuffer(width, height);

        _overlay = new Views.ScrollOverlayWindow(physicalRegion);
        _overlay.ShowNoActivate();
        _toolbar = new Views.GifToolbarWindow(this, physicalRegion);
        _toolbar.Show();
        _toolbar.Activate();

        _thread = new Thread(CaptureLoop) { IsBackground = true, Name = "jietuyao.gif" };
        _thread.Start();
    }

    private void CaptureLoop()
    {
        var interval = 1000.0 / _fps;
        var width = Math.Max(1, (int)Math.Round(_region.Width));
        var height = Math.Max(1, (int)Math.Round(_region.Height));
        while (_running)
        {
            var tick = DateTime.UtcNow;
            if (!_paused && _buffer != null)
            {
                try
                {
                    var frame = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    using (var g = Graphics.FromImage(frame))
                    {
                        g.CopyFromScreen((int)_region.X, (int)_region.Y, 0, 0, new Size(width, height),
                            System.Drawing.CopyPixelOperation.SourceCopy);
                    }
                    _buffer.Add(frame);
                    frame.Dispose();
                    NotifyState();
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

    private void NotifyState() => StateChanged?.Invoke();

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

    /// <summary>完成：停止采集 → 导出 GIF（进度窗）</summary>
    public async void Finish()
    {
        if (!_running)
            return;
        _running = false;
        _thread?.Join(1000);
        var buffer = _buffer;
        _buffer = null;
        CleanupWindows();

        if (buffer == null || buffer.Count == 0)
        {
            buffer?.Dispose();
            return;
        }

        // 保存对话框
        var suggested = $"jietuyao_{DateTime.Now:yyyyMMdd_HHmmss}.gif";
        var path = ScreenshotOutput.SaveGifFile(suggested, _settings.LastScreenshotSaveDir);
        if (path == null)
        {
            buffer.Dispose();
            return;
        }
        _settings.LastScreenshotSaveDir = Path.GetDirectoryName(path) ?? string.Empty;

        var progress = new Views.GifExportProgressWindow();
        progress.Show();
        try
        {
            var delayCs = Math.Max(2, (int)Math.Round(100.0 / _fps));
            await Task.Run(() => buffer.ExportGif(path, delayCs, p =>
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
        }
    }

    public void Cancel()
    {
        if (!_running)
            return;
        _running = false;
        _thread?.Join(500);
        _buffer?.Dispose();
        _buffer = null;
        CleanupWindows();
    }

    private void CleanupWindows()
    {
        _overlay?.Close();
        _overlay = null;
        _toolbar?.Close();
        _toolbar = null;
    }

    public void Dispose() => Cancel();
}
