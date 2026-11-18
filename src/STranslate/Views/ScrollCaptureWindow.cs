using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using STranslate.Core;

namespace STranslate.Views;

/// <summary>
/// 长截图选区覆盖框：点击穿透 + 高亮边框
/// </summary>
public sealed class ScrollOverlayWindow : Window
{
    public ScrollOverlayWindow(System.Windows.Rect physicalRegion)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Focusable = false;
        IsHitTestVisible = false;

        var accent = new SolidColorBrush(Color.FromRgb(0, 120, 215));
        accent.Freeze();
        Content = new Border
        {
            BorderBrush = accent,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(1),
        };

        var scale = GetDpiScale();
        Width = physicalRegion.Width / scale + 4;
        Height = physicalRegion.Height / scale + 4;
        Left = physicalRegion.Left / scale - 2;
        Top = physicalRegion.Top / scale - 2;

        SourceInitialized += (_, _) => MakeClickThrough();
    }

    private double GetDpiScale() =>
        PresentationSource.FromVisual(this)?.CompositionTarget.TransformToDevice.M11 ?? 1.0;

    private void MakeClickThrough()
    {
        var hwnd = new HWND(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        var style = PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        _ = PInvoke.SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
            style | 0x8000000 /*WS_EX_NOACTIVATE*/ | 0x20 /*WS_EX_TRANSPARENT*/);
    }

    public void ShowNoActivate()
    {
        Show();
        var scale = GetDpiScale();
        var hwnd = new HWND(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        PInvoke.SetWindowPos(hwnd, HWND.Null,
            (int)(Left * scale), (int)(Top * scale),
            (int)(Width * scale), (int)(Height * scale),
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
    }
}

/// <summary>
/// 长截图悬浮工具条：方向切换 / 立即截取 / 完成 / 取消
/// </summary>
public sealed class ScrollToolbarWindow : Window
{
    private readonly ScrollCaptureController _controller;
    readonly TextBlock _status;

    public ScrollToolbarWindow(ScrollCaptureController controller, System.Windows.Rect physicalRegion)
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
            Padding = new Thickness(8, 5, 8, 5),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
            BorderThickness = new Thickness(1),
        };
        Content = card;

        _status = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 10, 0),
            FontSize = 12,
        };
        panel.Children.Add(_status);
        panel.Children.Add(Button("方向 ↓↑", controller.ToggleDirection));
        panel.Children.Add(Button("立即截取", controller.CaptureManual));
        panel.Children.Add(Button("完成 ✔", controller.Finish, accent: true));
        panel.Children.Add(Button("取消 ✕", controller.Cancel));

        UpdateStatus();

        var scale = PresentationSource.FromVisual(this)?.CompositionTarget.TransformToDevice.M11 ?? 1.0;
        Width = 460;
        Height = 44;
        SizeToContent = SizeToContent.WidthAndHeight;
        // 放置在选区上方（放不下则下方）
        var y = physicalRegion.Top / scale - 52;
        if (y < 4)
            y = physicalRegion.Bottom / scale + 12;
        Left = Math.Clamp(physicalRegion.Left / scale, 4, Math.Max(4, SystemParameters.VirtualScreenWidth - 470));
        Top = y;
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

    private void OnStateChanged() =>
        Dispatcher.Invoke(UpdateStatus);

    private void UpdateStatus()
    {
        var direction = _controller.DirectionDown ? "向下" : "向上";
        _status.Text = $"长截图 | 已截 {_controller.FrameCount} 帧 | 滚动滚轮连拍（{direction}）";
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
                ? new SolidColorBrush(Color.FromRgb(0, 120, 215))
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
        _controller.StateChanged -= OnStateChanged;
        base.OnClosed(e);
    }
}
