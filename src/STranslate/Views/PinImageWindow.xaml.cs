using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using STranslate.Core;
using STranslate.Helpers;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;

namespace STranslate.Views;

/// <summary>
/// jietuyao 贴图窗口：置顶、拖动、滚轮缩放、Ctrl+滚轮透明度、旋转/翻转、OCR/翻译
/// </summary>
public sealed class PinImageWindow : Window
{
    private readonly BitmapSource _source;
    private readonly Rect _physicalBounds;
    private readonly System.Windows.Controls.Image _image;
    private readonly ScaleTransform _flipTransform = new(1, 1);
    private readonly RotateTransform _rotateTransform = new(0);
    private readonly TransformGroup _renderTransform;

    private double _scale = 1.0;
    private int _rotation; // 0/90/180/270

    /// <summary>OCR 请求</summary>
    public event Action<BitmapSource>? OcrRequested;

    /// <summary>翻译请求</summary>
    public event Action<BitmapSource>? TranslateRequested;

    public PinImageWindow(BitmapSource source, Rect physicalBounds, double opacity = 1.0)
    {
        _source = source;
        _physicalBounds = physicalBounds;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = true;
        Background = Brushes.Transparent;
        AllowsTransparency = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Opacity = Math.Clamp(opacity, 0.1, 1.0);
        Focusable = true;

        _image = new System.Windows.Controls.Image
        {
            Source = source,
            Stretch = Stretch.None,
            RenderTransformOrigin = new Point(0.5, 0.5),
        };
        _renderTransform = new TransformGroup();
        _renderTransform.Children.Add(_rotateTransform);
        _renderTransform.Children.Add(_flipTransform);
        _image.RenderTransform = _renderTransform;

        var border = new Border
        {
            Child = _image,
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
        };
        Content = border;

        MouseLeftButtonDown += OnDragMove;
        PreviewMouseWheel += OnMouseWheel;
        PreviewKeyDown += OnKeyDown;
        MouseDoubleClick += (_, _) => ResetTransform();
        Loaded += (_, _) => PositionWindow();
    }

    private double DpiScale
    {
        get
        {
            if (PresentationSource.FromVisual(this)?.CompositionTarget is { } target)
                return target.TransformToDevice.M11;
            return 1.0;
        }
    }

    /// <summary>按物理坐标定位并按物理尺寸设置窗口</summary>
    private unsafe void PositionWindow()
    {
        var scale = DpiScale;
        Width = _physicalBounds.Width / scale;
        Height = _physicalBounds.Height / scale;
        var hwnd = new HWND(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        PInvoke.SetWindowPos(hwnd, HWND.Null,
            (int)_physicalBounds.X, (int)_physicalBounds.Y,
            (int)Math.Ceiling(_physicalBounds.Width), (int)Math.Ceiling(_physicalBounds.Height),
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
        // WPF 内部布局坐标转换
        Left = _physicalBounds.X / scale;
        Top = _physicalBounds.Y / scale;
    }

    /// <summary>应用缩放/旋转后的窗口尺寸</summary>
    private void ApplyTransform()
    {
        var scale = DpiScale;
        var rotated = _rotation is 90 or 270;
        var w = (_physicalBounds.Width * _scale) / scale;
        var h = (_physicalBounds.Height * _scale) / scale;
        _image.Width = rotated ? h : w;
        _image.Height = rotated ? w : h;
        Width = _image.Width;
        Height = _image.Height;
    }

    private void ResetTransform()
    {
        _scale = 1.0;
        _rotation = 0;
        _rotateTransform.Angle = 0;
        _flipTransform.ScaleX = 1;
        _flipTransform.ScaleY = 1;
        PositionWindow();
        ApplyTransform();
        // 重置后回到原始位置
        Left = _physicalBounds.X / DpiScale;
        Top = _physicalBounds.Y / DpiScale;
    }

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            Opacity = Math.Clamp(Opacity + (e.Delta > 0 ? 0.05 : -0.05), 0.1, 1.0);
            return;
        }

        // 以光标为锚点缩放
        var factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
        var newScale = Math.Clamp(_scale * factor, 0.1, 8.0);
        if (Math.Abs(newScale - _scale) < 0.001) return;
        _scale = newScale;

        var pos = e.GetPosition(this);
        var screenX = Left + pos.X;
        var screenY = Top + pos.Y;

        // 保持 (screenX, screenY) 相对内容位置不变
        var relX = Width > 0 ? pos.X / Width : 0.5;
        var relY = Height > 0 ? pos.Y / Height : 0.5;
        ApplyTransform();
        Left = screenX - relX * Width;
        Top = screenY - relY * Height;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.C when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                CopyToClipboard();
                e.Handled = true;
                break;
        }
    }

    private Bitmap ToCurrentBitmap()
    {
        // 导出当前视觉状态（含旋转/翻转/缩放）
        var w = Math.Max(1, (int)Math.Round(ActualWidth * DpiScale));
        var h = Math.Max(1, (int)Math.Round(ActualHeight * DpiScale));
        var rtb = new RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        var clone = CloneVisual();
        rtb.Render(clone);
        return ScreenshotCapturer.ToGdiBitmap(rtb);
    }

    private System.Windows.Media.Visual CloneVisual()
    {
        // 简化：直接渲染 _image 所在 Border
        return (System.Windows.Media.Visual)Content;
    }

    private void CopyToClipboard()
    {
        using var bitmap = ToCurrentBitmap();
        ScreenshotOutput.CopyToClipboard(bitmap);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        BuildContextMenu();
    }

    private void BuildContextMenu()
    {
        var menu = new ContextMenu();

        void Add(string header, Action action, bool separator = false)
        {
            if (separator)
                menu.Items.Add(new Separator());
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        Add("复制 (Ctrl+C)", CopyToClipboard);
        Add("保存为文件...", SaveToFile);
        Add("OCR 识别", () => OcrRequested?.Invoke(_source));
        Add("翻译", () => TranslateRequested?.Invoke(_source));
        Add("还原显示 (双击)", ResetTransform, separator: true);
        Add("缩放 50%", () => ZoomTo(0.5));
        Add("缩放 100%", () => ZoomTo(1.0));
        Add("缩放 200%", () => ZoomTo(2.0));
        Add("旋转 90°", Rotate90);
        Add("水平翻转", () => _flipTransform.ScaleX *= -1, separator: true);
        Add("不透明度 25%", () => Opacity = 0.25);
        Add("不透明度 50%", () => Opacity = 0.5);
        Add("不透明度 100%", () => Opacity = 1.0);
        Add("关闭 (Esc)", Close, separator: true);
        Add("关闭全部贴图", () => PinImageManager.Instance.CloseAll());

        ContextMenu = menu;
    }

    private void ZoomTo(double scale)
    {
        _scale = scale;
        ApplyTransform();
    }

    private void Rotate90()
    {
        _rotation = (_rotation + 90) % 360;
        _rotateTransform.Angle = _rotation;
        ApplyTransform();
    }

    private void SaveToFile()
    {
        using var bitmap = ToCurrentBitmap();
        var vm = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<ViewModels.MainWindowViewModel>();
        var dir = vm?.Settings.LastScreenshotSaveDir;
        ScreenshotOutput.SaveToFile(bitmap, string.IsNullOrEmpty(dir) ? null : dir);
    }
}
