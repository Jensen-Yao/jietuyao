using STranslate.Plugin.Translate.DeepL.View;
using STranslate.Plugin.Translate.DeepL.ViewModel;
using System.Text.Json.Nodes;
using System.Windows.Controls;

namespace STranslate.Plugin.Translate.DeepL;

/// <summary>
/// 提供基于 DeepL API 的文本翻译能力。
/// </summary>
public class Main : TranslatePluginBase
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    /// <summary>
    /// 获取 DeepL 插件设置界面。
    /// </summary>
    /// <returns>绑定当前插件配置的设置控件。</returns>
    public override Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    /// <summary>
    /// 将 STranslate 源语言转换为 DeepL 源语言代码。
    /// </summary>
    /// <param name="langEnum">STranslate 语言枚举。</param>
    /// <returns>DeepL 语言代码；不支持时返回 <see langword="null"/>。</returns>
    public override string? GetSourceLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => "auto",
        LangEnum.ChineseSimplified => "ZH",
        LangEnum.ChineseTraditional => "ZH",
        LangEnum.Cantonese => "ZH",
        LangEnum.English => "EN",
        LangEnum.Japanese => "JA",
        LangEnum.Korean => "KO",
        LangEnum.French => "FR",
        LangEnum.Spanish => "ES",
        LangEnum.Russian => "RU",
        LangEnum.German => "DE",
        LangEnum.Italian => "IT",
        LangEnum.Turkish => "TR",
        LangEnum.PortuguesePortugal => "PT-PT",
        LangEnum.PortugueseBrazil => "PT-BR",
        LangEnum.Vietnamese => null,
        LangEnum.Indonesian => "ID",
        LangEnum.Thai => null,
        LangEnum.Malay => null,
        LangEnum.Arabic => "AR",
        LangEnum.Hindi => null,
        LangEnum.MongolianCyrillic => null,
        LangEnum.MongolianTraditional => null,
        LangEnum.Khmer => null,
        LangEnum.NorwegianBokmal => "NB",
        LangEnum.NorwegianNynorsk => "NB",
        LangEnum.Persian => null,
        LangEnum.Swedish => "SV",
        LangEnum.Polish => "PL",
        LangEnum.Dutch => "NL",
        LangEnum.Ukrainian => null,
        LangEnum.Uzbek => null,
        LangEnum.Uyghur => null,
        _ => "auto"
    };

    /// <summary>
    /// 将 STranslate 目标语言转换为 DeepL 目标语言代码。
    /// </summary>
    /// <param name="langEnum">STranslate 语言枚举。</param>
    /// <returns>DeepL 语言代码；不支持时返回 <see langword="null"/>。</returns>
    public override string? GetTargetLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => "auto",
        LangEnum.ChineseSimplified => "ZH-HANS",
        LangEnum.ChineseTraditional => "ZH-HANT",
        LangEnum.Cantonese => "ZH",
        LangEnum.English => "EN",
        LangEnum.Japanese => "JA",
        LangEnum.Korean => "KO",
        LangEnum.French => "FR",
        LangEnum.Spanish => "ES",
        LangEnum.Russian => "RU",
        LangEnum.German => "DE",
        LangEnum.Italian => "IT",
        LangEnum.Turkish => "TR",
        LangEnum.PortuguesePortugal => "PT-PT",
        LangEnum.PortugueseBrazil => "PT-BR",
        LangEnum.Vietnamese => null,
        LangEnum.Indonesian => "ID",
        LangEnum.Thai => null,
        LangEnum.Malay => null,
        LangEnum.Arabic => "AR",
        LangEnum.Hindi => null,
        LangEnum.MongolianCyrillic => null,
        LangEnum.MongolianTraditional => null,
        LangEnum.Khmer => null,
        LangEnum.NorwegianBokmal => "NB",
        LangEnum.NorwegianNynorsk => "NB",
        LangEnum.Persian => null,
        LangEnum.Swedish => "SV",
        LangEnum.Polish => "PL",
        LangEnum.Dutch => "NL",
        LangEnum.Ukrainian => null,
        LangEnum.Uzbek => null,
        LangEnum.Uyghur => null,
        _ => "auto"
    };

    /// <summary>
    /// 初始化插件上下文、加载设置并迁移旧版 API 类型配置。
    /// </summary>
    /// <param name="context">当前插件服务的运行时上下文。</param>
    public override void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
        if (Settings.MigrateLegacyApiType())
            context.SaveSettingStorage<Settings>();
    }

    /// <summary>
    /// 释放设置视图模型订阅的事件。
    /// </summary>
    public override void Dispose() => _viewModel?.Dispose();

    /// <summary>
    /// 使用当前配置的 DeepL API 端点翻译文本。
    /// </summary>
    /// <param name="request">翻译文本及源、目标语言。</param>
    /// <param name="result">用于写入翻译结果或错误信息的结果对象。</param>
    /// <param name="cancellationToken">用于取消 HTTP 请求的令牌。</param>
    /// <returns>表示异步翻译操作的任务。</returns>
    public override async Task TranslateAsync(TranslateRequest request, TranslateResult result, CancellationToken cancellationToken = default)
    {
        if (GetSourceLanguage(request.SourceLang) is not string sourceStr)
        {
            result.Fail(Context.GetTranslation("UnsupportedSourceLang"));
            return;
        }
        if (GetTargetLanguage(request.TargetLang) is not string targetStr)
        {
            result.Fail(Context.GetTranslation("UnsupportedTargetLang"));
            return;
        }

        var content = new
        {
            text = new[] { request.Text },
            source_lang = sourceStr,
            target_lang = targetStr,
        };

        var option = string.IsNullOrEmpty(Settings.ApiKey)
            ? default :
            new Options
            {
                Headers = new Dictionary<string, string>
                {
                    { "Authorization", "DeepL-Auth-Key " + Settings.ApiKey }
                }
            };

        if (!Constant.TryBuildEndpoint(Settings, Constant.TranslatePath, out var url))
        {
            result.Fail(Context.GetTranslation("STranslate_Plugin_Translate_DeepL_InvalidApiUrl"));
            return;
        }

        var response = await Context.HttpService.PostAsync(url, content, option, cancellationToken);

        var jsonNode = JsonNode.Parse(response);
        var translatedText = jsonNode?["translations"]?[0]?["text"]?.ToString() ?? throw new Exception(response);

        result.Success(translatedText);
    }
}
