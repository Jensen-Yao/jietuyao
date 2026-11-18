using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Gma.System.MouseKeyHook;
using Microsoft.Extensions.Logging;
using STranslate.Helpers;
using Size = System.Windows.Size;

namespace STranslate.Core;

/// <summary>
/// 滚动长截图控制器：全局滚轮钩子 → 冷却后连拍 → 哈希拼接 → 编辑器后处理
/// </summary>
public sealed class ScrollCaptureController : IDisposable
{
    private static readonly Lazy<ScrollCaptureController> _instance = new(() =>
        new ScrollCaptureController(Ioc.Default.GetService<Microsoft.Extensions.Logging.ILogger<ScrollCaptureController>>(),
            Ioc.Default.GetRequiredService<Settings>()));
    public static ScrollCaptureController Instance => _instance.Value;

    private readonly ILogger<ScrollCaptureController>? _logger;
    private readonly Settings _settings;

    private IKeyboardMouseEvents? _mouseEvents;
    private readonly DispatcherTimer _cooldown;
    private readonly List<Bitmap> _frames = [];
    private ulong _lastFingerprint;

    private Rect _region;
    private bool _running;
    private bool _directionDown = true;

    private Views.ScrollOverlayWindow? _overlay;
    private Views.ScrollToolbarWindow? _toolbar;

    private ScrollCaptureController(ILogger<ScrollCaptureController>? logger, Settings settings)
    {
        _logger = logger;
        _settings = settings;
        _cooldown = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(420) };
        _cooldown.Tick += (_, _) =>
        {
            _cooldown.Stop();
            CaptureFrame();
        };
    }

    public bool IsRunning => _running;
    public int FrameCount => _frames.Count;

    /// <summary>状态变化（帧数/方向）</summary>
    public event Action? StateChanged;

    /// <summary>
    /// 从选区开始长截图（编辑器须已关闭）
    /// </summary>
    public void Begin(Rect physicalRegion)
    {
        if (_running)
            return;
        _running = true;
        _region = physicalRegion;
        _frames.Clear();
        _lastFingerprint = 0;
        _directionDown = true;

        // 覆盖框 + 工具条
        _overlay = new Views.ScrollOverlayWindow(physicalRegion);
        _overlay.ShowNoActivate();
        _toolbar = new Views.ScrollToolbarWindow(this, physicalRegion);
        _toolbar.Show();
        _toolbar.Activate();

        // 全局滚轮钩子
        _mouseEvents = Hook.GlobalEvents();
        _mouseEvents.MouseWheelExt += OnMouseWheel;

        // 首帧
        CaptureFrame();
    }

    private void OnMouseWheel(object? sender, MouseEventExtArgs e)
    {
        if (!_running)
            return;
        // 只有滚轮作用于选区附近时才连拍（宽容半径：光标在区域横向范围 + 垂直±600px）
        var p = e.Location;
        var inZone = p.X >= _region.Left - 80 && p.X <= _region.Right + 80 &&
                     p.Y >= _region.Top - 600 && p.Y <= _region.Bottom + 600;
        if (!inZone)
            return;
        _cooldown.Stop();
        _cooldown.Start();
    }

    /// <summary>手动补拍一帧（工具条"立即截取"）</summary>
    public void CaptureManual()
    {
        _cooldown.Stop();
        CaptureFrame();
    }

    private void CaptureFrame()
    {
        if (!_running)
            return;
        try
        {
            var frame = ScrollStitcher.CaptureRegion(_region);
            var fingerprint = ScrollStitcher.ImageFingerprint(frame);
            if (fingerprint == _lastFingerprint)
            {
                frame.Dispose(); // 未滚动
                return;
            }
            _lastFingerprint = fingerprint;
            _frames.Add(frame);
            StateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "长截图连拍失败");
        }
    }

    public void ToggleDirection()
    {
        _directionDown = !_directionDown;
        // 换向时把已拍帧按新方向重排（简单起见清空重来，避免拼接混乱）
        foreach (var f in _frames)
            f.Dispose();
        _frames.Clear();
        _lastFingerprint = 0;
        StateChanged?.Invoke();
        CaptureFrame();
    }

    public bool DirectionDown => _directionDown;

    /// <summary>完成：拼接 → 打开编辑器</summary>
    public async void Finish()
    {
        if (!_running)
            return;
        var frames = _frames.ToArray();
        StopInternal();

        if (frames.Length == 0)
            return;

        await Task.Run(() =>
        {
            Bitmap stitched;
            try
            {
                stitched = ScrollStitcher.Stitch(frames, _directionDown ? 1 : -1);
            }
            catch
            {
                stitched = frames[0];
            }
            foreach (var f in frames.Where(f => f != stitched))
                f.Dispose();

            // 打开编辑器继续标注/保存/贴图
            _ = System.Windows.Application.Current?.Dispatcher?.InvokeAsync(async () =>
            {
                var editor = Ioc.Default.GetRequiredService<ScreenshotEditorService>();
                var result = await editor.EditImageAsync(stitched, new Rect(_region.Location, new Size(stitched.Width, stitched.Height)));
                result?.Dispose();
            });
        });
    }

    public void Cancel()
    {
        if (!_running)
            return;
        foreach (var f in _frames)
            f.Dispose();
        StopInternal();
    }

    private void StopInternal()
    {
        _running = false;
        _frames.Clear();
        _cooldown.Stop();
        if (_mouseEvents != null)
        {
            _mouseEvents.MouseWheelExt -= OnMouseWheel;
            _mouseEvents.Dispose();
            _mouseEvents = null;
        }
        _overlay?.Close();
        _overlay = null;
        _toolbar?.Close();
        _toolbar = null;
    }

    public void Dispose() => StopInternal();
}
