using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using STranslate.Helpers;
using Size = System.Windows.Size;

namespace STranslate.Core;

/// <summary>
/// 截图编辑器编排：冻结屏幕 → 打开各显示器编辑窗口 → 等待用户完成动作
/// </summary>
public sealed class ScreenshotEditorService(Settings settings, PinnedWindowController pinnedWindowController)
{
    private static bool _editing;

    /// <summary>编辑器是否进行中</summary>
    public static bool IsEditing => _editing;

    public async Task<ScreenshotEditorResult?> EditAsync(ScreenshotEditOptions? options = null)
    {
        if (_editing)
            return null;
        _editing = true;
        try
        {
            options ??= new ScreenshotEditOptions();

            // 隐藏主窗口与贴图翻译窗口
            IAsyncDisposable? captureScope = null;
            if (options.HideAppWindows)
            {
                captureScope = await pinnedWindowController.BeginCaptureAsync();
                if (captureScope == null)
                    return null;
            }

            var mainWindow = App.Current.MainWindow;
            var mainWindowHidden = false;
            if (options.HideAppWindows &&
                mainWindow.Visibility == Visibility.Visible &&
                !mainWindow.Topmost)
            {
                mainWindow.Visibility = Visibility.Collapsed;
                mainWindowHidden = true;
            }

            try
            {
                // 等待界面刷新后冻结
                await Task.Delay(120);

                var monitors = ScreenshotCapturer.GetMonitors();
                var virtualBounds = ScreenshotCapturer.GetVirtualBounds(monitors);

                var finder = new ScreenshotWindowFinder();
                finder.Snapshot(virtualBounds);

                var frozen = ScreenshotCapturer.CaptureVirtualDesktop(virtualBounds, settings.CaptureIncludeCursor);
                var source = ScreenshotCapturer.ToBitmapSource(frozen);

                var session = new ScreenshotEditorSession(frozen, source, virtualBounds, monitors, finder, options);
                session.BuildPixelCache();
                session.ToolStyles = settings.ScreenshotToolStyles;
                if (settings.RememberScreenshotTool &&
                    Enum.TryParse<ScreenshotTool>(settings.LastScreenshotTool, out var lastTool))
                {
                    session.SetTool(lastTool);
                }
                session.SelectionStyle = new SelectionStyleState
                {
                    RoundedEnabled = settings.ScreenshotRoundedRadius > 0,
                    RoundedRadius = settings.ScreenshotRoundedRadius,
                    BorderMode = settings.ScreenshotBorderMode,
                    BorderSize = settings.ScreenshotBorderSize,
                    ShadowColorHex = settings.ScreenshotShadowColor,
                    BorderColorHex = settings.ScreenshotBorderColor,
                    LockRatio = settings.ScreenshotLockRatio,
                };

                // 每个显示器一个全屏编辑窗口
                var windows = monitors
                    .Select(m => new Views.ScreenshotEditorWindow(session, m))
                    .ToList();

                foreach (var window in windows)
                    window.ShowNoActivate();

                // 激活光标所在显示器的窗口
                var cursorMonitor = MonitorInfo.GetCursorDisplayMonitor();
                var focusWindow = windows.FirstOrDefault(w => w.Monitor.DeviceName == cursorMonitor.Name) ?? windows[0];
                focusWindow.ActivateCanvas();

                var result = await session.Task;

                foreach (var window in windows)
                    window.CloseEditor();

                session.Dispose();
                PersistStyles(session);

                await PostProcessAsync(result);
                return result;
            }
            finally
            {
                if (mainWindowHidden)
                    mainWindow.Visibility = Visibility.Visible;
                if (captureScope != null)
                    await captureScope.DisposeAsync();
            }
        }
        finally
        {
            _editing = false;
        }
    }

    /// <summary>
    /// 编辑已有图像（贴图再次编辑等场景）：不重新抓屏，隐藏窗口仅限编辑范围
    /// </summary>
    public async Task<ScreenshotEditorResult?> EditImageAsync(Bitmap image, Rect physicalRect)
    {
        if (_editing)
            return null;
        _editing = true;
        try
        {
            var virtualBounds = new Rect(physicalRect.Location, new Size(image.Width, image.Height));
            var monitors = ScreenshotCapturer.GetMonitors();
            var relevant = monitors.Where(m => m.Bounds.IntersectsWith(virtualBounds)).ToList();
            if (relevant.Count == 0)
                relevant = [monitors[0]];

            var source = ScreenshotCapturer.ToBitmapSource(image);
            var session = new ScreenshotEditorSession(image, source, virtualBounds, relevant,
                new ScreenshotWindowFinder(),
                new ScreenshotEditOptions { HideAppWindows = false, SmartSelection = false, InitialSelection = virtualBounds });
            session.BuildPixelCache();
            session.ToolStyles = settings.ScreenshotToolStyles;
            session.SelectionStyle = new SelectionStyleState
            {
                RoundedEnabled = settings.ScreenshotRoundedRadius > 0,
                RoundedRadius = settings.ScreenshotRoundedRadius,
                BorderMode = settings.ScreenshotBorderMode,
                BorderSize = settings.ScreenshotBorderSize,
                ShadowColorHex = settings.ScreenshotShadowColor,
                BorderColorHex = settings.ScreenshotBorderColor,
                LockRatio = settings.ScreenshotLockRatio,
            };

            var windows = relevant.Select(m => new Views.ScreenshotEditorWindow(session, m)).ToList();
            foreach (var window in windows)
                window.ShowNoActivate();
            var cursorMonitor = MonitorInfo.GetCursorDisplayMonitor();
            (windows.FirstOrDefault(w => w.Monitor.DeviceName == cursorMonitor.Name) ?? windows[0]).ActivateCanvas();

            var result = await session.Task;
            foreach (var window in windows)
                window.CloseEditor();
            session.Dispose();
            PersistStyles(session);

            await PostProcessAsync(result);
            return result;
        }
        finally
        {
            _editing = false;
        }
    }

    private void PersistStyles(ScreenshotEditorSession session)
    {
        settings.ScreenshotToolStyles = session.ToolStyles;
        var style = session.SelectionStyle;
        settings.ScreenshotRoundedRadius = style.RoundedEnabled ? style.RoundedRadius : 0;
        settings.ScreenshotBorderMode = style.BorderMode;
        settings.ScreenshotBorderSize = style.BorderSize;
        settings.ScreenshotLockRatio = style.LockRatio;
        if (settings.RememberScreenshotTool)
            settings.LastScreenshotTool = session.ActiveTool.ToString();
    }

    /// <summary>保存/贴图/复制等 UI 动作后处理（TCS 延续可能在线程池，调度回 UI 线程）</summary>
    private async Task PostProcessAsync(ScreenshotEditorResult? result)
    {
        if (result == null)
            return;
        if (result.Action is not (ScreenshotEditorAction.Save or ScreenshotEditorAction.Pin or ScreenshotEditorAction.Copy or ScreenshotEditorAction.LongScreenshot or ScreenshotEditorAction.GifRecord or ScreenshotEditorAction.ImageOverlay))
            return;

        await App.Current.Dispatcher.InvokeAsync(() =>
        {
            switch (result.Action)
            {
                case ScreenshotEditorAction.Save:
                {
                    var path = ScreenshotOutput.SaveToFile(result.EditedImage, settings.LastScreenshotSaveDir);
                    if (path != null)
                    {
                        result.SavedFilePath = path;
                        settings.LastScreenshotSaveDir = Path.GetDirectoryName(path) ?? string.Empty;
                    }
                    break;
                }
                case ScreenshotEditorAction.Pin:
                    PinImageManager.Instance.Pin(result.EditedImage, result.PhysicalBounds, settings.PinDefaultOpacity);
                    break;
                case ScreenshotEditorAction.Copy:
                    ScreenshotOutput.CopyToClipboard(result.EditedImage);
                    break;
                case ScreenshotEditorAction.LongScreenshot:
                    // 编辑器窗口已关闭，选区位置开启滚动连拍
                    ScrollCaptureController.Instance.Begin(result.PhysicalBounds);
                    break;
                case ScreenshotEditorAction.GifRecord:
                    GifRecorderController.Instance.Begin(result.PhysicalBounds);
                    break;
                case ScreenshotEditorAction.ImageOverlay:
                {
                    // 译图：干净图（无标注）→ OCR → 逐行翻译 → 回填渲染 → 结果窗
                    var clean = result.TakeClean();
                    var bounds = result.PhysicalBounds;
                    _ = RunImageOverlayAsync(clean, bounds);
                    break;
                }
            }
        });
    }

    private async Task RunImageOverlayAsync(System.Drawing.Bitmap clean, Rect physicalBounds)
    {
        try
        {
            var outcome = await TranslatedImageController.CreateAsync(clean, TranslatedImageController.ParseTarget(settings.ImageOverlayTargetLang));
            if (outcome.Image is { } image)
            {
                // 工作台接管 clean/image 所有权（关闭时统一释放）
                var workbench = new Views.TranslatedImageWorkbench(clean, image, outcome.Lines, outcome.Info, outcome.Target, physicalBounds);
                workbench.Show();
                workbench.Activate();
            }
            else
            {
                clean.Dispose();
                System.Windows.MessageBox.Show(outcome.Error, "jietuyao 译图",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            clean.Dispose();
            System.Windows.MessageBox.Show($"译图失败：{ex.Message}", "jietuyao 译图",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }
}

/// <summary>
/// jietuyao 截图编辑器实现 STranslate 的截图接口。
/// 所有走 IScreenshot 的流程（截图翻译/OCR/图片翻译/二维码）自动升级为编辑器体验。
/// </summary>
public sealed class JietuyaoScreenshot(ScreenshotEditorService editorService) : IScreenshot
{
    public Bitmap? GetScreenshot()
    {
        // 无调用方，但接口要求：泵消息等待编辑器完成
        if (Application.Current?.Dispatcher.CheckAccess() == true)
        {
            var task = CaptureBitmapAsync();
            var frame = new DispatcherFrame();
            task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
            return task.Result;
        }
        return CaptureBitmapAsync().GetAwaiter().GetResult();
    }

    public async Task<Bitmap?> GetScreenshotAsync() => await CaptureBitmapAsync();

    private async Task<Bitmap?> CaptureBitmapAsync()
    {
        var result = await editorService.EditAsync(new ScreenshotEditOptions());
        return result?.DetachEditedOrNull();
    }

    public async Task<ScreenshotCaptureResult?> GetScreenshotCaptureAsync()
    {
        var result = await CaptureAsyncCore();
        if (result == null) return null;
        var bitmap = result.TakeEdited();
        var bounds = System.Drawing.Rectangle.Round(new RectangleF(
            (float)result.PhysicalBounds.X, (float)result.PhysicalBounds.Y,
            (float)result.PhysicalBounds.Width, (float)result.PhysicalBounds.Height));
        result.Dispose();
        return new ScreenshotCaptureResult(bitmap, bounds);
    }

    private Task<ScreenshotEditorResult?> CaptureAsyncCore() =>
        editorService.EditAsync(new ScreenshotEditOptions());
}

file static class ScreenshotResultExtensions
{
    public static Bitmap? DetachEditedOrNull(this ScreenshotEditorResult result)
    {
        if (result == null) return null;
        var bitmap = result.TakeEdited();
        result.Dispose();
        return bitmap;
    }
}
