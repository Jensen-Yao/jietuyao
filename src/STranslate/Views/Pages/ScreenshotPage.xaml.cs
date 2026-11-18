using System.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using STranslate.Core;
using STranslate.Core.ClipboardHistory;
using STranslate.ViewModels.Pages;

namespace STranslate.Views.Pages;

public partial class ScreenshotPage
{
    public ScreenshotPage(ScreenshotViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();

        // 剪贴板监听开关即时生效（设置页切换 ↔ 服务启停）
        ViewModel.Settings.PropertyChanged += OnSettingsChanged;
        Unloaded += (_, _) => ViewModel.Settings.PropertyChanged -= OnSettingsChanged;
    }

    public ScreenshotViewModel ViewModel { get; }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Settings.ClipboardHistoryEnabled))
            return;
        var service = Ioc.Default.GetService<ClipboardHistoryService>();
        if (service == null)
            return;
        if (ViewModel.Settings.ClipboardHistoryEnabled)
            service.StartMonitor();
        else
            service.StopMonitor();
    }
}
