using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace STranslate.Plugin.Translate.DeepL.ViewModel;

/// <summary>
/// 管理 DeepL 插件设置并在修改后立即持久化。
/// </summary>
public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext Context;
    private readonly Settings Settings;

    /// <summary>
    /// 初始化 DeepL 设置视图模型。
    /// </summary>
    /// <param name="context">插件运行时上下文。</param>
    /// <param name="settings">当前服务的持久化设置。</param>
    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        Context = context;
        Settings = settings;

        ApiKey = Settings.ApiKey;
        ApiType = Settings.ApiType;
        CustomApiUrl = Settings.CustomApiUrl;
        UsageStr = Settings.UsageStr;
        Usage = Settings.Usage;

        PropertyChanged += PropertyChangedHandler;
    }

    private void PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ApiKey):
                Settings.ApiKey = ApiKey;
                break;
            case nameof(ApiType):
                Settings.ApiType = ApiType;
                break;
            case nameof(CustomApiUrl):
                Settings.CustomApiUrl = CustomApiUrl;
                break;
            case nameof(UsageStr):
                Settings.UsageStr = UsageStr;
                break;
            case nameof(Usage):
                Settings.Usage = Usage;
                break;
            default:
                return;
        }
        Context.SaveSettingStorage<Settings>();
    }

    /// <summary>
    /// 取消设置变更事件订阅。
    /// </summary>
    public void Dispose() => PropertyChanged -= PropertyChangedHandler;

    [ObservableProperty] public partial string ApiKey { get; set; }
    [ObservableProperty] public partial DeepLApiType ApiType { get; set; }
    [ObservableProperty] public partial string CustomApiUrl { get; set; }
    [ObservableProperty] public partial string UsageStr { get; set; }
    [ObservableProperty] public partial double Usage { get; set; }

    public string ApiUrlPreview => ApiType switch
    {
        DeepLApiType.Api => Constant.ApiBaseUrl,
        DeepLApiType.ApiFree => Constant.ApiFreeBaseUrl,
        _ => string.Empty
    };

    public bool IsCustomApi => ApiType == DeepLApiType.Custom;

    public bool IsPresetApi => !IsCustomApi;

    partial void OnApiTypeChanged(DeepLApiType _)
    {
        OnPropertyChanged(nameof(ApiUrlPreview));
        OnPropertyChanged(nameof(IsCustomApi));
        OnPropertyChanged(nameof(IsPresetApi));
    }

    [RelayCommand]
    private async Task QueryUsageAsync()
    {
        if (!Constant.TryBuildEndpoint(Settings, Constant.UsagePath, out var url))
        {
            Context.Snackbar.ShowError(Context.GetTranslation("STranslate_Plugin_Translate_DeepL_InvalidApiUrl"));
            return;
        }

        var option = string.IsNullOrEmpty(Settings.ApiKey)
            ? default :
            new Options
            {
                Headers = new Dictionary<string, string>
                {
                    { "Authorization", "DeepL-Auth-Key " + Settings.ApiKey }
                }
            };
        try
        {
            var response = await Context.HttpService.GetAsync(url, option);
            var parseData = JsonNode.Parse(response);
            var count = parseData?["character_count"]?.ToString() ?? throw new Exception(Context.GetTranslation("STranslate_Plugin_Translate_DeepL_Query_Error1"));
            var limit = parseData?["character_limit"]?.ToString() ?? throw new Exception(Context.GetTranslation("STranslate_Plugin_Translate_DeepL_Query_Error2"));
            UsageStr = $"{count}/{limit}";
            Usage = double.Parse(count) / double.Parse(limit) * 100;
            Context.Snackbar.ShowSuccess(Context.GetTranslation("STranslate_Plugin_Translate_DeepL_Query_Success"));
        }
        catch (OperationCanceledException)
        {
            // ignored
            Context.Snackbar.Show(Context.GetTranslation("STranslate_Plugin_Translate_DeepL_Query_Cancel"));
        }
        catch (Exception ex)
        {
            if (ex.InnerException is { } innEx) ex = innEx;
            Context.Snackbar.ShowError(Context.GetTranslation("STranslate_Plugin_Translate_DeepL_Query_Fail"));
            Context.Logger.LogError($"DeepL query usage error: {ex.Message}");
        }
    }
}
