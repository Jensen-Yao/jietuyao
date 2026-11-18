using System.Drawing;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using STranslate.Helpers;
using STranslate.ViewModels;

namespace STranslate.Core;

/// <summary>
/// 贴图管理器（jietuyao 钉图）
/// </summary>
public sealed class PinImageManager
{
    public static PinImageManager Instance { get; } = new();

    private readonly List<Views.PinImageWindow> _pins = [];

    public IReadOnlyList<Views.PinImageWindow> Pins => _pins;

    /// <summary>
    /// 将位图贴到屏幕指定位置（物理坐标）
    /// </summary>
    public void Pin(Bitmap bitmap, Rect physicalBounds, double opacity = 1.0)
    {
        var source = ScreenshotCapturer.ToBitmapSource(bitmap);
        bitmap.Dispose();
        var window = new Views.PinImageWindow(source, physicalBounds, opacity);
        _pins.Add(window);
        window.Closed += (_, _) => _pins.Remove(window);

        // OCR / 翻译路由到主窗口视图模型（延迟解析避免启动环）
        window.OcrRequested += source2 => RouteOcr(source2);
        window.TranslateRequested += source2 => RouteTranslate(source2);

        window.Show();
        window.Activate();
    }

    public void CloseAll()
    {
        foreach (var window in _pins.ToList())
            window.Close();
    }

    private static void RouteOcr(System.Windows.Media.Imaging.BitmapSource source)
    {
        var vm = Ioc.Default.GetService<MainWindowViewModel>();
        if (vm == null) return;
        var bitmap = ScreenshotCapturer.ToGdiBitmap(source);
        _ = vm.OcrHandlerAsync(bitmap);
    }

    private static void RouteTranslate(System.Windows.Media.Imaging.BitmapSource source)
    {
        var vm = Ioc.Default.GetService<MainWindowViewModel>();
        if (vm == null) return;
        var bitmap = ScreenshotCapturer.ToGdiBitmap(source);
        _ = vm.ScreenshotTranslateHandlerAsync(bitmap);
    }
}
