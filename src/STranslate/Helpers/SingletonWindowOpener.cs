using CommunityToolkit.Mvvm.DependencyInjection;
using iNKORE.UI.WPF.Modern;
using STranslate.Core;
using System.Windows;
using System.Windows.Threading;

namespace STranslate.Helpers;

/// <summary>
///     <see href="https://github.com/Flow-Launcher/Flow.Launcher"/>
/// </summary>
public static class SingletonWindowOpener
{
    private static readonly Settings _settings = Ioc.Default.GetRequiredService<Settings>();

    public static T Open<T>(params object[] args) where T : Window
    {
        var window = Application.Current.Windows.OfType<T>().FirstOrDefault()
                     ?? (T)Activator.CreateInstance(typeof(T), args)!;

        Activate(window);

        return window;
    }

    public static async Task<T> OpenAsync<T>(params object[] args) where T : Window
    {
        var window = Application.Current.Windows.OfType<T>().FirstOrDefault();

        if (window != null)
        {
            Activate(window);
            return window;
        }

        // 在UI线程上异步创建和显示窗口
        window = await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var newWindow = (T)Activator.CreateInstance(typeof(T), args)!;
            Activate(newWindow);
            return newWindow;
        }, DispatcherPriority.Background);

        return window;
    }

    public static async Task<T> OpenPreparedAsync<T>(
        Action<T> prepareBeforeShow,
        params object[] args) where T : Window
    {
        return await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var window = Application.Current.Windows.OfType<T>().FirstOrDefault()
                         ?? (T)Activator.CreateInstance(typeof(T), args)!;
            prepareBeforeShow(window);
            Activate(window);
            return window;
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// 激活窗口
    /// </summary>
    /// <param name="window"></param>
    private static void Activate<T>(T window) where T : Window
    {
        // Fix UI bug
        // Add `window.WindowState = WindowState.Normal`
        // If only use `window.Show()`, Settings-window doesn't show when minimized in taskbar 
        // Not sure why this works tho
        // Probably because, when `.Show()` fails, `window.WindowState == Minimized` (not `Normal`) 
        // https://stackoverflow.com/a/59719760/4230390
        // Ensure the window is not minimized before showing it
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        // 如果窗口未显示，则设置主题并显示
        if (!window.IsVisible)
        {
            ThemeManager.SetRequestedTheme(window, _settings.ColorScheme);
            window.Show();
        }

        Win32Helper.ActivateForegroundWindow(window);

        window.Activate();
        window.Focus();
    }
}
