using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using STranslate.Core;

namespace STranslate.Views;

/// <summary>
/// 译图结果窗：预览 + 复制/贴图/保存（吸收自 SnipTrans 工作台的双图思路，精简为结果优先）
/// </summary>
public sealed class TranslatedImageWindow : Window
{
    private readonly Bitmap _image;

    public TranslatedImageWindow(System.Drawing.Bitmap image, string info)
    {
        _image = image;
        var source = ScreenshotCapturer.ToBitmapSource(image);

        WindowStyle = WindowStyle.ToolWindow;
        Title = "jietuyao 译图";
        Width = Math.Min(1100, source.PixelWidth / Dpi() + 48);
        Height = Math.Min(760, source.PixelHeight / Dpi() + 118);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(38, 38, 38));
        Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235));

        var panel = new DockPanel();

        // 顶栏：信息 + 按钮
        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 8, 12, 8) };
        var infoText = new TextBlock
        {
            Text = info,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 420,
            ToolTip = info,
        };
        top.Children.Add(infoText);
        var spacer = new Border { Width = 12 };
        top.Children.Add(spacer);
        top.Children.Add(Btn("复制译图 (Ctrl+C)", () => { using var b = CloneCurrent(); ScreenshotOutput.CopyToClipboard(b); }, accent: true));
        top.Children.Add(Btn("贴图 (T)", () => { using var b = CloneCurrent(); PinImageManager.Instance.Pin(b, new Rect(Left * Dpi(), Top * Dpi(), b.Width, b.Height), 1.0); }));
        top.Children.Add(Btn("保存 (S)", Save));
        top.Children.Add(Btn("关闭 (Esc)", Close));
        DockPanel.SetDock(top, Dock.Top);
        panel.Children.Add(top);

        var viewer = new ScrollViewer
        {
            Background = new SolidColorBrush(Color.FromRgb(20, 20, 20)),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var imageControl = new System.Windows.Controls.Image
        {
            Source = source,
            Stretch = Stretch.None,
            Margin = new Thickness(8),
        };
        viewer.Content = imageControl;
        panel.Children.Add(viewer);
        Content = panel;

        PreviewKeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Escape:
                    Close();
                    e.Handled = true;
                    break;
                case Key.C when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                    using (var b = CloneCurrent()) ScreenshotOutput.CopyToClipboard(b);
                    e.Handled = true;
                    break;
                case Key.T:
                    using (var b = CloneCurrent()) PinImageManager.Instance.Pin(b, new Rect(Left * Dpi(), Top * Dpi(), b.Width, b.Height), 1.0);
                    e.Handled = true;
                    break;
                case Key.S:
                    Save();
                    e.Handled = true;
                    break;
            }
        };
    }

    private double Dpi() =>
        PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

    private System.Drawing.Bitmap CloneCurrent() => new(_image);

    private void Save()
    {
        using var clone = CloneCurrent();
        ScreenshotOutput.SaveToFile(clone);
    }

    private Button Btn(string text, Action action, bool accent = false)
    {
        var button = new Button
        {
            Content = text,
            Margin = new Thickness(4, 0, 4, 0),
            Padding = new Thickness(12, 4, 12, 4),
            Cursor = Cursors.Hand,
            Background = accent
                ? new SolidColorBrush(Color.FromRgb(0, 120, 215))
                : new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
            Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
            BorderThickness = new Thickness(0),
        };
        button.Click += (_, _) => action();
        return button;
    }
}
