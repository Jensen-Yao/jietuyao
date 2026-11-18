using CommunityToolkit.Mvvm.ComponentModel;
using iNKORE.UI.WPF.Modern.Controls;
using STranslate.Core;

namespace STranslate.Controls;

/// <summary>
/// PluginMarketSettingsDialog.xaml 的交互逻辑
/// </summary>
public partial class PluginMarketSettingsDialog : ContentDialog
{
    private readonly PluginMarketSettingsViewModel _viewModel;

    public PluginMarketSettingsDialog(Settings settings, DataProvider dataProvider)
    {
        InitializeComponent();

        _viewModel = new PluginMarketSettingsViewModel(settings, dataProvider);
        DataContext = _viewModel;
    }

    /// <summary>
    /// 获取或设置一个值，该值指示设置是否已保存
    /// </summary>
    public bool IsSaved { get; private set; }

    /// <summary>
    /// 获取或设置一个值，该值指示 CDN 设置是否发生变化
    /// </summary>
    public bool IsCdnSettingsChanged { get; private set; }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 保存设置
        IsCdnSettingsChanged = _viewModel.SaveSettings();
        IsSaved = true;
    }
}

/// <summary>
/// 插件市场设置对话框的视图模型
/// </summary>
public partial class PluginMarketSettingsViewModel(Settings settings, DataProvider dataProvider) : ObservableObject
{
    public DataProvider DataProvider { get; } = dataProvider;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomCdnSource))]
    public partial PluginMarketCdnSourceType SelectedCdnSource { get; set; } = settings.PluginMarketCdnSource;

    [ObservableProperty]
    public partial string CustomCdnUrl { get; set; } = settings.CustomPluginMarketCdnUrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomDownloadProxy))]
    public partial PluginDownloadProxyType SelectedDownloadProxy { get; set; } = settings.PluginDownloadProxy;

    [ObservableProperty]
    public partial string CustomDownloadProxyUrl { get; set; } = settings.CustomDownloadProxyUrl;

    /// <summary>
    /// 是否为自定义 CDN 源
    /// </summary>
    public bool IsCustomCdnSource => SelectedCdnSource == PluginMarketCdnSourceType.Custom;

    /// <summary>
    /// 是否为自定义下载代理
    /// </summary>
    public bool IsCustomDownloadProxy => SelectedDownloadProxy == PluginDownloadProxyType.Custom;

    /// <summary>
    /// 保存设置到 Settings 对象，返回 CDN 设置是否发生变化
    /// </summary>
    public bool SaveSettings()
    {
        // 检查 CDN 设置是否发生变化
        var isCdnChanged = settings.PluginMarketCdnSource != SelectedCdnSource ||
                           settings.CustomPluginMarketCdnUrl != CustomCdnUrl;

        settings.PluginMarketCdnSource = SelectedCdnSource;
        settings.CustomPluginMarketCdnUrl = CustomCdnUrl;
        settings.PluginDownloadProxy = SelectedDownloadProxy;
        settings.CustomDownloadProxyUrl = CustomDownloadProxyUrl;

        return isCdnChanged;
    }
}