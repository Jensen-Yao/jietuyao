using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace STranslate.Plugin.Translate.GoogleBuiltIn.ViewModel;

/// <summary>
/// 管理 Google 内置翻译设置并验证当前请求模式。
/// </summary>
public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext _context;
    private readonly Settings _settings;
    private readonly Main _main;

    /// <summary>
    /// 初始化设置视图模型并绑定插件配置存储。
    /// </summary>
    public SettingsViewModel(IPluginContext context, Settings settings, Main main)
    {
        _context = context;
        _settings = settings;
        _main = main;
        RequestMode = settings.RequestMode;
        ApiUrl = settings.ApiUrl;
        PropertyChanged += OnPropertyChanged;
    }

    /// <summary>
    /// 当前选择的请求模式。
    /// </summary>
    [ObservableProperty] public partial RequestMode RequestMode { get; set; }

    /// <summary>
    /// 自定义中转 API 的完整地址。
    /// </summary>
    [ObservableProperty] public partial string ApiUrl { get; set; }

    /// <summary>
    /// 当前配置的连接验证结果。
    /// </summary>
    [ObservableProperty] public partial string ValidateResult { get; set; } = string.Empty;

    /// <summary>
    /// 获取是否应显示自定义 API 地址设置。
    /// </summary>
    public bool IsCustomApiMode => RequestMode == RequestMode.CustomApi;

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RequestMode):
                _settings.RequestMode = RequestMode;
                OnPropertyChanged(nameof(IsCustomApiMode));
                break;
            case nameof(ApiUrl):
                _settings.ApiUrl = ApiUrl.Trim();
                break;
            default:
                return;
        }

        ValidateResult = string.Empty;
        _context.SaveSettingStorage<Settings>();
    }

    /// <summary>
    /// 使用当前模式发起测试翻译并更新验证结果。
    /// </summary>
    [RelayCommand]
    public async Task ValidateAsync()
    {
        try
        {
            await _main.TranslateTextAsync("Hello world!", "auto", "zh-CN");
            ValidateResult = _context.GetTranslation("ValidationSuccess");
        }
        catch (Exception ex)
        {
            ValidateResult = _context.GetTranslation("ValidationFailure");
            _context.Logger.LogError(ex, _context.GetTranslation("ValidationFailure"));
        }
    }

    /// <summary>
    /// 解除配置变更事件订阅。
    /// </summary>
    public void Dispose() => PropertyChanged -= OnPropertyChanged;
}
