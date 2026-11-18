using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using STranslate.Core;

namespace STranslate.Views;

/// <summary>
/// GIF 录制悬浮工具条：计时 / 暂停继续 / 帧率 / 完成 / 取消
/// </summary>
public sealed class GifToolbarWindow : Window
{
    private readonly GifRecorderController _controller;
    private readonly TextBlock _status;
    private readonly Button _pauseButton;
    private readonly ComboBox _fpsBox;
    private readonly System.Windows.Threading.DispatcherTimer _timer;

    public GifToolbarWindow(GifRecorderController controller, System.Windows.Rect physicalRegion)
    {
        _controller = controller;
        controller.StateChanged += OnStateChanged;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = true;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.Manual;
        PreviewKeyDown += OnKeyDown;

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var card = new Border
        {
            Child = panel,
            Background = new SolidColorBrush(Color.FromArgb(235, 32, 32, 32)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 5, 8, 5),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
            BorderThickness = new Thickness(1),
        };
        Content = card;

        _status = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            FontSize = 12,
            MinWidth = 190,
        };
        panel.Children.Add(_status);

        _pauseButton = Button("⏸ 暂停", controller.Pause);
        panel.Children.Add(_pauseButton);

        var annotateButton = Button("✏ 标注", controller.ToggleAnnotate);
        panel.Children.Add(annotateButton);

        _fpsBox = new ComboBox
        {
            MinWidth = 64,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
        };
        foreach (var fps in new[] { 5, 10, 15, 24 })
            _fpsBox.Items.Add(new ComboBoxItem { Content = $"{fps} fps", Tag = fps });
        _fpsBox.SelectedIndex = 2;
        _fpsBox.SelectionChanged += (_, _) =>
        {
            if (_fpsBox.SelectedItem is ComboBoxItem { Tag: int f })
                _controller.SetFps(f);
        };
        panel.Children.Add(_fpsBox);

        panel.Children.Add(Button("⏹ 完成并保存", controller.Finish, accent: true));
        panel.Children.Add(Button("✕ 取消", controller.Cancel));

        SizeToContent = SizeToContent.WidthAndHeight;
        var scale = PresentationSource.FromVisual(this)?.CompositionTarget.TransformToDevice.M11 ?? 1.0;
        var y = physicalRegion.Top / scale - 52;
        if (y < 4)
            y = physicalRegion.Bottom / scale + 12;
        Left = Math.Clamp(physicalRegion.Left / scale, 4, Math.Max(4, SystemParameters.VirtualScreenWidth - 520));
        Top = y;

        _timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => UpdateStatus();
        _timer.Start();
        UpdateStatus();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _controller.Cancel();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            _controller.Finish();
            e.Handled = true;
        }
    }

    private void OnStateChanged() => Dispatcher.Invoke(UpdateStatus);

    private void UpdateStatus()
    {
        _status.Text = $"● GIF 录制 {_controller.Elapsed:mm\\:ss} | {_controller.FrameCount} 帧{(_controller.IsPaused ? " | 已暂停" : "")}";
        _pauseButton.Content = _controller.IsPaused ? "▶ 继续" : "⏸ 暂停";
        _pauseButton.Click -= OnPauseClick;
        _pauseButton.Click += OnPauseClick;
    }

    private void OnPauseClick(object sender, RoutedEventArgs e)
    {
        if (_controller.IsPaused)
            _controller.Resume();
        else
            _controller.Pause();
    }

    private static Button Button(string text, Action action, bool accent = false)
    {
        var button = new Button
        {
            Content = text,
            Margin = new Thickness(4, 0, 4, 0),
            Padding = new Thickness(10, 4, 10, 4),
            Cursor = Cursors.Hand,
            Background = accent
                ? new SolidColorBrush(Color.FromRgb(196, 43, 28))
                : new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
            Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
            BorderThickness = new Thickness(0),
            FontSize = 12,
        };
        button.Click += (_, _) => action();
        return button;
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _controller.StateChanged -= OnStateChanged;
        base.OnClosed(e);
    }
}

/// <summary>
/// GIF 录制中标注覆盖窗（InkCanvas 手绘）。
/// 笔画带录制时间戳 → 导出时按帧时间分段合成（v1 逐帧标注时间线语义）。
/// </summary>
public sealed class GifAnnotationWindow : Window
{
    private static readonly Guid TimestampGuid = new("3F9E7C2A-6B45-4D8E-9A21-5C0E7F1B2D34");

    private readonly Func<long> _timestampProvider;

    public InkCanvas Ink { get; }

    public GifAnnotationWindow(System.Windows.Rect physicalRegion, Func<long>? timestampProvider = null)
    {
        _timestampProvider = timestampProvider ?? (() => 0);

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.Manual;
        IsHitTestVisible = true;

        Ink = new InkCanvas
        {
            Background = System.Windows.Media.Brushes.Transparent,
            DefaultDrawingAttributes = new System.Windows.Ink.DrawingAttributes
            {
                Color = System.Windows.Media.Colors.Red,
                Width = 3,
                Height = 3,
                FitToCurve = true,
            },
        };
        // 笔画落笔即打时间戳
        Ink.Strokes.StrokesChanged += (_, e) =>
        {
            if (e.Added.Count == 0)
                return;
            var timestamp = _timestampProvider();
            foreach (System.Windows.Ink.Stroke stroke in e.Added)
                stroke.AddPropertyData(TimestampGuid, BitConverter.GetBytes(timestamp));
        };
        Content = Ink;

        var scale = PresentationSource.FromVisual(this)?.CompositionTarget.TransformToDevice.M11 ?? 1.0;
        Width = physicalRegion.Width / scale;
        Height = physicalRegion.Height / scale;
        Left = physicalRegion.Left / scale;
        Top = physicalRegion.Top / scale;

        // 初始不拦截鼠标（仅标注模式开启时交互）
        IsHitTestVisible = false;
        Opacity = 0.999;
    }

    public void SetAnnotateMode(bool on)
    {
        IsHitTestVisible = on;
        if (on)
        {
            Show();
            Activate();
        }
    }

    /// <summary>渲染指定笔画子集为位图（区域物理尺寸）</summary>
    private System.Drawing.Bitmap RenderSubset(System.Windows.Ink.StrokeCollection strokes, double dpiScale)
    {
        var w = Math.Max(1, (int)Math.Round(ActualWidth * dpiScale));
        var h = Math.Max(1, (int)Math.Round(ActualHeight * dpiScale));
        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(new StrokeHostVisual(strokes));
        rtb.Freeze();
        return STranslate.Core.ScreenshotCapturer.ToGdiBitmap(rtb);
    }

    /// <summary>渲染当前全部笔迹为位图（兼容旧路径）</summary>
    public System.Drawing.Bitmap? RenderOverlayBitmap(double dpiScale)
    {
        if (Ink.Strokes.Count == 0)
            return null;
        return RenderSubset(Ink.Strokes, dpiScale);
    }

    /// <summary>
    /// 按时间线生成累积叠加变体：返回 [(t1,V1),(t2,V2)...]，
    /// 帧时间 ≥ tk 时应用 Vk（笔画从落笔时刻起持续显示到结尾）。
    /// </summary>
    public List<(long TimeMs, System.Drawing.Bitmap Overlay)> BuildTimelineOverlays(double dpiScale)
    {
        var result = new List<(long, System.Drawing.Bitmap)>();
        if (Ink.Strokes.Count == 0)
            return result;

        // 收集 (时间, 笔画) 并按时间排序
        var timed = new List<(long Time, System.Windows.Ink.Stroke Stroke)>();
        foreach (System.Windows.Ink.Stroke stroke in Ink.Strokes)
        {
            var data = stroke.ContainsPropertyData(TimestampGuid)
                ? (byte[]?)stroke.GetPropertyData(TimestampGuid)
                : null;
            var time = data is { Length: >= 8 } ? BitConverter.ToInt64(data) : 0;
            timed.Add((time, stroke));
        }
        timed.Sort((a, b) => a.Time.CompareTo(b.Time));

        // 累积渲染：每出现一个新时间点生成一个变体
        var cumulative = new System.Windows.Ink.StrokeCollection();
        long lastTime = long.MinValue;
        foreach (var (time, stroke) in timed)
        {
            cumulative.Add(stroke);
            if (time == lastTime)
                continue; // 同一时刻多笔画共用变体
            lastTime = time;
            result.Add((time, RenderSubset(criticalClone(cumulative), dpiScale)));
        }
        return result;

        static System.Windows.Ink.StrokeCollection criticalClone(System.Windows.Ink.StrokeCollection source)
        {
            // RenderSubset 在 RTB 渲染时不修改集合，直接复用
            return source;
        }
    }

    /// <summary>承载笔画子集的独立视觉（脱离 InkCanvas 渲染）</summary>
    private sealed class StrokeHostVisual : System.Windows.Media.DrawingVisual
    {
        public StrokeHostVisual(System.Windows.Ink.StrokeCollection strokes)
        {
            using var dc = RenderOpen();
            foreach (System.Windows.Ink.Stroke stroke in strokes)
                stroke.Draw(dc);
        }
    }
}

/// <summary>
/// GIF 回放裁剪窗：预览循环 + 起止帧滑杆 + 导出
/// </summary>
public sealed class GifPlaybackTrimWindow : Window
{
    private readonly GifFrameBuffer _buffer;
    private readonly int _fps;
    private readonly IReadOnlyList<STranslate.Core.MouseEffectEvent> _mouseEvents;
    private readonly List<(long TimeMs, System.Drawing.Bitmap Overlay)>? _timelineOverlays;
    private readonly System.Windows.Controls.Image _image;
    private readonly Slider _start;
    private readonly Slider _end;
    private readonly TextBlock _info;
    private readonly System.Windows.Threading.DispatcherTimer _timer;
    private readonly List<System.Drawing.Bitmap> _frames = [];
    private int _current;

    /// <summary>用户确认导出（start/end 帧）</summary>
    public event Action<int, int>? ExportConfirmed;

    public GifPlaybackTrimWindow(GifFrameBuffer buffer, int fps, System.Windows.Rect physicalRegion,
        IReadOnlyList<STranslate.Core.MouseEffectEvent>? mouseEvents = null,
        List<(long TimeMs, System.Drawing.Bitmap Overlay)>? timelineOverlays = null)
    {
        _buffer = buffer;
        _fps = Math.Clamp(fps, 5, 24);
        _mouseEvents = mouseEvents ?? [];
        _timelineOverlays = timelineOverlays;

        WindowStyle = WindowStyle.ToolWindow;
        Title = "GIF 回放与裁剪";
        Width = 640;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
        Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235));

        var panel = new StackPanel { Margin = new Thickness(14) };

        _image = new System.Windows.Controls.Image
        {
            Height = 300,
            Stretch = Stretch.Uniform,
        };
        var imageBorder = new Border
        {
            Child = _image,
            Background = new SolidColorBrush(Color.FromRgb(20, 20, 20)),
            Padding = new Thickness(4),
        };
        panel.Children.Add(imageBorder);

        _info = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(187, 187, 187)),
            Margin = new Thickness(0, 8, 0, 4),
            FontSize = 12,
        };
        panel.Children.Add(_info);

        var startLabel = new TextBlock { Text = "起始帧", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)) };
        panel.Children.Add(startLabel);
        _start = new Slider { Minimum = 0, IsMoveToPointEnabled = true };
        panel.Children.Add(_start);

        var endLabel = new TextBlock { Text = "结束帧", FontSize = 12, Margin = new Thickness(0, 6, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)) };
        panel.Children.Add(endLabel);
        _end = new Slider { Minimum = 0, IsMoveToPointEnabled = true };
        panel.Children.Add(_end);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0),
        };
        var export = new Button
        {
            Content = "保存 GIF",
            Padding = new Thickness(16, 4, 16, 4),
            Background = new SolidColorBrush(Color.FromRgb(196, 43, 28)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
        };
        export.Click += (_, _) =>
        {
            var s = (int)_start.Value;
            var e = (int)Math.Max(_end.Value, _start.Value + 1);
            ExportConfirmed?.Invoke(s, e);
        };
        var cancel = new Button
        {
            Content = "取消",
            Padding = new Thickness(16, 4, 16, 4),
            Margin = new Thickness(6, 0, 0, 0),
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
        };
        cancel.Click += (_, _) => Close();
        buttons.Children.Add(export);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        Content = panel;

        // 载入帧
        for (var i = 0; i < buffer.Count; i++)
            _frames.Add(buffer.DecodeFrame(i));
        _start.Maximum = Math.Max(0, _frames.Count - 1);
        _end.Maximum = Math.Max(1, _frames.Count - 1);
        _end.Value = Math.Max(1, _frames.Count - 1);
        _start.ValueChanged += (_, _) => { if (_start.Value > _end.Value) _end.Value = _start.Value; UpdateInfo(); ShowFrame((int)_start.Value); };
        _end.ValueChanged += (_, _) => { if (_end.Value < _start.Value) _start.Value = _end.Value; UpdateInfo(); };

        _timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / _fps) };
        _timer.Tick += (_, _) => PlayNext();
        _timer.Start();
        UpdateInfo();
        ShowFrame(0);

        Closed += (_, _) =>
        {
            _timer.Stop();
            foreach (var f in _frames)
                f.Dispose();
        };
    }

    private void PlayNext()
    {
        var s = (int)_start.Value;
        var e = (int)_end.Value;
        _current = _current < s || _current >= e ? s : _current + 1;
        ShowFrame(_current);
    }

    private void ShowFrame(int index)
    {
        if (index < 0 || index >= _frames.Count)
            return;
        var source = _frames[index];

        // 预览即所见：合成时间线标注与鼠标特效（临时副本，不污染源帧）
        System.Drawing.Bitmap? composite = null;
        var needsComposite = (_timelineOverlays is { Count: > 0 }) || _mouseEvents.Count > 0;
        if (needsComposite)
        {
            composite = new System.Drawing.Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = System.Drawing.Graphics.FromImage(composite))
            {
                g.DrawImage(source, 0, 0, source.Width, source.Height);
                var frameTime = _buffer.FrameTimestampMs(index, Math.Max(2, (int)Math.Round(100.0 / _fps)));
                if (_timelineOverlays is { Count: > 0 })
                {
                    foreach (var (t, o) in _timelineOverlays)
                    {
                        if (t <= frameTime)
                            g.DrawImage(o, 0, 0, composite.Width, composite.Height);
                        else
                            break;
                    }
                }
                if (_mouseEvents.Count > 0)
                    STranslate.Core.MouseEffectRenderer.Draw(g, frameTime, _mouseEvents);
            }
        }

        _image.Source = STranslate.Core.ScreenshotCapturer.ToBitmapSource(composite ?? source);
        composite?.Dispose();
    }

    private void UpdateInfo()
    {
        var s = (int)_start.Value;
        var e = (int)_end.Value;
        var duration = (e - s) / (double)_fps;
        _info.Text = $"共 {_frames.Count} 帧 | 选中 {e - s} 帧 ≈ {duration:F1}s @ {_fps}fps";
    }
}

/// <summary>
/// GIF 导出进度窗
/// </summary>
public sealed class GifExportProgressWindow : Window
{
    private readonly ProgressBar _bar;
    private readonly TextBlock _text;

    public GifExportProgressWindow()
    {
        WindowStyle = WindowStyle.ToolWindow;
        Title = "正在导出 GIF";
        Width = 380;
        Height = 140;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
        Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235));

        var panel = new StackPanel { Margin = new Thickness(20), VerticalAlignment = VerticalAlignment.Center };
        _text = new TextBlock
        {
            Text = "编码帧 0%…",
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 10),
        };
        _bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Height = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
        };
        panel.Children.Add(_text);
        panel.Children.Add(_bar);
        Content = panel;
    }

    public void ReportProgress(double progress)
    {
        Dispatcher.Invoke(() =>
        {
            _bar.Value = Math.Clamp(progress, 0, 1);
            _text.Text = $"编码帧 {(int)(progress * 100)}%…";
        });
    }

    public void Complete(string path)
    {
        Dispatcher.Invoke(() =>
        {
            _bar.Value = 1;
            _text.Text = $"已保存：{path}";
            var closeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            closeTimer.Tick += (_, _) => { closeTimer.Stop(); Close(); };
            closeTimer.Start();
        });
    }

    public void Fail(string message)
    {
        Dispatcher.Invoke(() =>
        {
            _text.Text = $"导出失败：{message}";
            var closeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            closeTimer.Tick += (_, _) => { closeTimer.Stop(); Close(); };
            closeTimer.Start();
        });
    }
}
