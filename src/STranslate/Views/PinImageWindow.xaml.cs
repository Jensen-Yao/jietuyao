using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using STranslate.Core;
using STranslate.Helpers;
using STranslate.Plugin;
using Brushes = System.Windows.Media.Brushes;
using Size = System.Windows.Size;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;

namespace STranslate.Views;

/// <summary>
/// jietuyao 贴图窗口：置顶、拖动、滚轮缩放、Ctrl+滚轮透明度、旋转/翻转、
/// 缩略图模式、再次编辑、OCR 文字选择层
/// </summary>
public sealed class PinImageWindow : Window
{
    private readonly BitmapSource _source;
    private Rect _physicalBounds;
    private readonly Grid _rootGrid;
    private readonly System.Windows.Controls.Image _image;
    private readonly ScaleTransform _flipTransform = new(1, 1);
    private readonly RotateTransform _rotateTransform = new(0);
    private readonly TransformGroup _renderTransform;
    private readonly Canvas _ocrLayer = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };

    private double _scale = 1.0;
    private int _rotation; // 0/90/180/270
    private double _savedScale = 1.0;
    private bool _isThumbnail;

    /// <summary>OCR 请求</summary>
    public event Action<BitmapSource>? OcrRequested;

    /// <summary>翻译请求</summary>
    public event Action<BitmapSource>? TranslateRequested;

    /// <summary>再次编辑完成（参数为编辑后的位图）</summary>
    public event Action<PinImageWindow, Bitmap>? ImageEdited;

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
        _rootGrid = new Grid();
        _rootGrid.Children.Add(border);
        _rootGrid.Children.Add(_ocrLayer);
        Content = _rootGrid;

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
        Width = _physicalBounds.Width * _scale / scale;
        Height = _physicalBounds.Height * _scale / scale;
        var hwnd = new HWND(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        PInvoke.SetWindowPos(hwnd, HWND.Null,
            (int)_physicalBounds.X, (int)_physicalBounds.Y,
            (int)Math.Ceiling(_physicalBounds.Width * _scale), (int)Math.Ceiling(_physicalBounds.Height * _scale),
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
        Left = _physicalBounds.X / scale;
        Top = _physicalBounds.Y / scale;
        ApplyTransform();
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
        UpdateOcrLayer();
    }

    private void ResetTransform()
    {
        _scale = 1.0;
        _rotation = 0;
        _rotateTransform.Angle = 0;
        _flipTransform.ScaleX = 1;
        _flipTransform.ScaleY = 1;
        _isThumbnail = false;
        PositionWindow();
        // 重置后回到原始位置
        Left = _physicalBounds.X / DpiScale;
        Top = _physicalBounds.Y / DpiScale;
    }

    // ===== 缩略图模式 =====
    public bool IsThumbnail => _isThumbnail;

    public void ToggleThumbnail()
    {
        if (_isThumbnail)
        {
            _scale = _savedScale;
            _isThumbnail = false;
        }
        else
        {
            _savedScale = _scale;
            // 缩略图宽度固定 120 物理像素
            _scale = 120.0 / Math.Max(1, _physicalBounds.Width);
            _isThumbnail = true;
        }
        ApplyTransform();
    }

    public void SetThumbnail(bool thumbnail)
    {
        if (thumbnail != _isThumbnail)
            ToggleThumbnail();
    }

    /// <summary>替换贴图内容（再次编辑后回填）</summary>
    public void UpdateImage(Bitmap newImage)
    {
        var source = ScreenshotCapturer.ToBitmapSource(newImage);
        newImage.Dispose();
        _image.Source = source;
        _physicalBounds = new Rect(_physicalBounds.Location, new Size(source.PixelWidth, source.PixelHeight));
        _scale = 1.0;
        _isThumbnail = false;
        PositionWindow();
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
        if (_isThumbnail) _isThumbnail = false;

        var pos = e.GetPosition(this);
        var screenX = Left + pos.X;
        var screenY = Top + pos.Y;

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
                if (_ocrLayer.Visibility == Visibility.Visible)
                {
                    _ocrLayer.Visibility = Visibility.Collapsed;
                    _ocrLayer.IsHitTestVisible = false;
                }
                else
                {
                    Close();
                }
                e.Handled = true;
                break;
            case Key.C when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                CopyToClipboard();
                e.Handled = true;
                break;
        }
    }

    /// <summary>导出当前视觉状态（含旋转/翻转/缩放）</summary>
    public Bitmap ExportCurrentBitmap()
    {
        var w = Math.Max(1, (int)Math.Round(ActualWidth * DpiScale));
        var h = Math.Max(1, (int)Math.Round(ActualHeight * DpiScale));
        var rtb = new RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(_rootGrid);
        return ScreenshotCapturer.ToGdiBitmap(rtb);
    }

    /// <summary>当前窗口的物理屏幕范围</summary>
    public Rect GetCurrentPhysicalBounds()
    {
        var scale = DpiScale;
        return new Rect(Left * scale, Top * scale, Width * scale, Height * scale);
    }

    private void CopyToClipboard()
    {
        using var bitmap = ExportCurrentBitmap();
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
        Add("再次编辑...", () => ImageEdited?.Invoke(this, ExportCurrentBitmap()));
        Add("OCR 识别", () => OcrRequested?.Invoke(_source));
        Add("翻译", () => TranslateRequested?.Invoke(_source));
        Add("OCR 文字层（点击复制）", ToggleOcrLayer);
        Add("缩略图模式", ToggleThumbnail, separator: true);
        Add("还原显示 (双击)", ResetTransform);
        Add("缩放 50%", () => { _isThumbnail = false; _scale = 0.5; ApplyTransform(); });
        Add("缩放 100%", () => { _isThumbnail = false; _scale = 1.0; ApplyTransform(); });
        Add("缩放 200%", () => { _isThumbnail = false; _scale = 2.0; ApplyTransform(); });
        Add("旋转 90°", Rotate90);
        Add("水平翻转", () => _flipTransform.ScaleX *= -1, separator: true);
        Add("不透明度 25%", () => Opacity = 0.25);
        Add("不透明度 50%", () => Opacity = 0.5);
        Add("不透明度 100%", () => Opacity = 1.0);
        Add("关闭 (Esc)", Close, separator: true);
        Add("关闭全部贴图", () => PinImageManager.Instance.CloseAll());

        ContextMenu = menu;
    }

    private void Rotate90()
    {
        _rotation = (_rotation + 90) % 360;
        _rotateTransform.Angle = _rotation;
        ApplyTransform();
    }

    /// <summary>公开：还原显示（管理器批量调用）</summary>
    public void ResetTransformPublic() => ResetTransform();

    /// <summary>居中到所在显示器（保持当前尺寸）</summary>
    public void CenterToWorkArea()
    {
        var scale = DpiScale;
        var bounds = GetCurrentPhysicalBounds();
        var monitor = STranslate.Helpers.MonitorInfo.GetDisplayMonitors()
            .FirstOrDefault(m => m.Bounds.Contains(new System.Windows.Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2)))
            ?? STranslate.Helpers.MonitorInfo.GetPrimaryDisplayMonitor();
        var target = monitor.WorkingArea;
        Left = (target.X + (target.Width - bounds.Width) / 2) / scale;
        Top = (target.Y + (target.Height - bounds.Height) / 2) / scale;
    }

    /// <summary>公开：保存到文件（管理器批量调用）</summary>
    public void SaveToFileDialog() => SaveToFile();

    private void SaveToFile()
    {
        using var bitmap = ExportCurrentBitmap();
        var vm = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<ViewModels.MainWindowViewModel>();
        var dir = vm?.Settings.LastScreenshotSaveDir;
        ScreenshotOutput.SaveToFile(bitmap, string.IsNullOrEmpty(dir) ? null : dir);
    }

    // ===== OCR 文字选择层 =====
    private bool _ocrRunning;

    private async void ToggleOcrLayer()
    {
        if (_ocrLayer.Visibility == Visibility.Visible)
        {
            _ocrLayer.Visibility = Visibility.Collapsed;
            _ocrLayer.IsHitTestVisible = false;
            return;
        }
        if (_ocrRunning) return;

        _ocrRunning = true;
        try
        {
            var ocrService = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<Services.OcrService>();
            var svc = ocrService?.Services.FirstOrDefault(s => s.IsEnabled);
            if (svc?.Plugin is not IOcrPlugin plugin)
            {
                MessageBox.Show(this, "请先在设置中启用一个 OCR 服务", "OCR", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 用原始图像识别（不受缩放旋转影响）
            var bitmap = ScreenshotCapturer.ToGdiBitmap(_source);
            OcrResult? result;
            var lang = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default
                .GetService<ViewModels.MainWindowViewModel>()?.Settings.ScreenshotOcrLanguage ?? LangEnum.Auto;
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                result = await plugin.RecognizeAsync(
                    new OcrRequest(stream.ToArray(), lang, _source.PixelWidth, _source.PixelHeight));
            }
            bitmap.Dispose();
            Utilities.PrepareOcrResult(result);
            if (result?.Regions is not { Count: > 0 })
                return;

            BuildOcrLayer(result);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"OCR 失败：{ex.Message}", "OCR", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _ocrRunning = false;
        }
    }

    private void BuildOcrLayer(OcrResult result)
    {
        _ocrLayer.Children.Clear();
        _ocrBoxes.Clear();
        // 归一：仅 OcrContents（如 PP-OCR）的引擎 → 组装出 Regions 结构复用同一渲染路径
        if (result.Regions.Count == 0 && result.OcrContents.Count > 0)
        {
            var region = new STranslate.Plugin.OcrRegion();
            foreach (var content in result.OcrContents)
            {
                var paragraph = new STranslate.Plugin.OcrParagraph();
                paragraph.Lines.Add(content);
                region.Paragraphs.Add(paragraph);
            }
            result.Regions.Add(region);
        }
        var scaleX = _image.ActualWidth / Math.Max(1, _source.PixelWidth);
        var scaleY = _image.ActualHeight / Math.Max(1, _source.PixelHeight);

        foreach (var region in result.Regions)
        {
            var regionText = string.Join("\n", region.Paragraphs.SelectMany(p => p.Lines.Select(l => l.Text))).Trim();
            if (region.BoxPoints is not { Count: > 0 } || string.IsNullOrEmpty(regionText))
                continue;

            var minX = region.BoxPoints.Min(p => p.X);
            var minY = region.BoxPoints.Min(p => p.Y);
            var maxX = region.BoxPoints.Max(p => p.X);
            var maxY = region.BoxPoints.Max(p => p.Y);
            var rect = new Rect(minX * scaleX, minY * scaleY,
                Math.Max(8, (maxX - minX) * scaleX), Math.Max(8, (maxY - minY) * scaleY));

            var border = new Border
            {
                Width = rect.Width,
                Height = rect.Height,
                Background = new SolidColorBrush(Color.FromArgb(70, 0, 120, 215)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(180, 0, 160, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Cursor = Cursors.Hand,
                ToolTip = regionText,
            };
            var text = new TextBlock
            {
                Text = regionText,
                Foreground = Brushes.White,
                FontSize = Math.Max(8, Math.Min(16, rect.Height * 0.6)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(2, 0, 2, 0),
            };
            border.Child = text;
            border.MouseLeftButtonDown += (_, _) =>
            {
                try { Clipboard.SetText(regionText); } catch { /* 占用 */ }
            };
            Canvas.SetLeft(border, rect.X);
            Canvas.SetTop(border, rect.Y);
            _ocrLayer.Children.Add(border);
            _ocrBoxes.Add((new Rect(rect.X, rect.Y, rect.Width, rect.Height), regionText));
        }

        _ocrLayer.Width = _image.ActualWidth;
        _ocrLayer.Height = _image.ActualHeight;
        _ocrLayer.Visibility = Visibility.Visible;
        _ocrLayer.IsHitTestVisible = true;
        AttachOcrDragSelect();
    }

    private readonly List<(Rect Rect, string Text)> _ocrBoxes = [];
    private System.Windows.Shapes.Rectangle? _ocrSelectionRect;
    private Point _ocrDragStart;
    private bool _ocrDragging;

    /// <summary>OCR 层空白处拖框 → 框住的文字合并复制</summary>
    private void AttachOcrDragSelect()
    {
        _ocrLayer.MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is not Canvas)
                return;
            _ocrDragStart = e.GetPosition(_ocrLayer);
            _ocrDragging = true;
            _ocrSelectionRect = new System.Windows.Shapes.Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0, 160, 255)),
                StrokeThickness = 1.5,
                StrokeDashArray = [2, 2],
                Fill = new SolidColorBrush(Color.FromArgb(30, 0, 120, 215)),
            };
            Canvas.SetLeft(_ocrSelectionRect, _ocrDragStart.X);
            Canvas.SetTop(_ocrSelectionRect, _ocrDragStart.Y);
            _ocrLayer.Children.Add(_ocrSelectionRect);
            _ocrLayer.CaptureMouse();
            e.Handled = true;
        };
        _ocrLayer.MouseMove += (_, e) =>
        {
            if (!_ocrDragging || _ocrSelectionRect == null)
                return;
            var pos = e.GetPosition(_ocrLayer);
            var x = Math.Min(_ocrDragStart.X, pos.X);
            var y = Math.Min(_ocrDragStart.Y, pos.Y);
            var w = Math.Abs(pos.X - _ocrDragStart.X);
            var h = Math.Abs(pos.Y - _ocrDragStart.Y);
            Canvas.SetLeft(_ocrSelectionRect, x);
            Canvas.SetTop(_ocrSelectionRect, y);
            _ocrSelectionRect.Width = w;
            _ocrSelectionRect.Height = h;
            e.Handled = true;
        };
        _ocrLayer.MouseLeftButtonUp += (_, e) =>
        {
            if (!_ocrDragging || _ocrSelectionRect == null)
                return;
            var pos = e.GetPosition(_ocrLayer);
            var selection = new Rect(
                Math.Min(_ocrDragStart.X, pos.X), Math.Min(_ocrDragStart.Y, pos.Y),
                Math.Abs(pos.X - _ocrDragStart.X), Math.Abs(pos.Y - _ocrDragStart.Y));
            _ocrLayer.Children.Remove(_ocrSelectionRect);
            _ocrSelectionRect = null;
            _ocrDragging = false;
            _ocrLayer.ReleaseMouseCapture();

            if (selection.Width > 6 && selection.Height > 6)
            {
                // 按阅读序合并框选内文本
                var ordered = _ocrBoxes.Where(b => b.Rect.IntersectsWith(selection))
                    .OrderBy(b => b.Rect.Y).ThenBy(b => b.Rect.X);
                var joined = string.Join("\n", ordered.Select(b => b.Text));
                if (joined.Length > 0)
                {
                    try { Clipboard.SetText(joined); } catch { /* 占用 */ }
                }
            }
            e.Handled = true;
        };
    }

    private void UpdateOcrLayer()
    {
        if (_ocrLayer.Visibility != Visibility.Visible)
            return;
        _ocrLayer.Visibility = Visibility.Collapsed; // 尺寸变化后旧坐标失效，需重识别
        _ocrLayer.Children.Clear();
    }
}
