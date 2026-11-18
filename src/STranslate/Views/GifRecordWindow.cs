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
