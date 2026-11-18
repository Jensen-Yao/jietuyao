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

        OverlayTargetBox.Items.Clear();
        OverlayTargetBox.Items.Add(new System.Collections.Generic.KeyValuePair<string, string>("自动推断", ""));
        var targets = new (string Label, string Tag)[]
        {
            ("中文(简体)", "zh-Hans"),
            ("中文(繁體)", "zh-Hant"),
            ("English", "en"),
            ("日本語", "ja"),
            ("한국어", "ko"),
            ("Français", "fr"),
            ("Deutsch", "de"),
            ("Español", "es"),
            ("Русский", "ru"),
        };
        foreach (var item in targets)
            OverlayTargetBox.Items.Add(new System.Collections.Generic.KeyValuePair<string, string>(item.Label, item.Tag));

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
