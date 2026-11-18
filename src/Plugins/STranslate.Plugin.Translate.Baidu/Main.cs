using STranslate.Plugin.Translate.Baidu.View;
using STranslate.Plugin.Translate.Baidu.ViewModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows.Controls;

namespace STranslate.Plugin.Translate.Baidu;

public class Main : TranslatePluginBase
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    private const string Url = "https://fanyi-api.baidu.com/api/trans/vip/translate";

    public override Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public override string? GetSourceLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => "auto",
        LangEnum.ChineseSimplified => "zh",
        LangEnum.ChineseTraditional => "cht",
        LangEnum.Cantonese => "yue",
        LangEnum.English => "en",
        LangEnum.Japanese => "jp",
        LangEnum.Korean => "kor",
        LangEnum.French => "fra",
        LangEnum.Spanish => "spa",
        LangEnum.Russian => "ru",
        LangEnum.German => "de",
        LangEnum.Italian => "it",
        LangEnum.Turkish => "tr",
        LangEnum.PortuguesePortugal => "pt",
        LangEnum.PortugueseBrazil => "pot",
        LangEnum.Vietnamese => "vie",
        LangEnum.Indonesian => "id",
        LangEnum.Thai => "th",
        LangEnum.Malay => "may",
        LangEnum.Arabic => "ar",
        LangEnum.Hindi => "hi",
        LangEnum.MongolianCyrillic => null,
        LangEnum.MongolianTraditional => null,
        LangEnum.Khmer => "hkm",
        LangEnum.NorwegianBokmal => "nob",
        LangEnum.NorwegianNynorsk => "nno",
        LangEnum.Persian => "per",
        LangEnum.Swedish => "swe",
        LangEnum.Polish => "pl",
        LangEnum.Dutch => "nl",
        LangEnum.Ukrainian => "ukr",
        LangEnum.Uzbek => "uz",
        LangEnum.Uyghur => null,
        _ => "auto"
    };

    public override string? GetTargetLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => "auto",
        LangEnum.ChineseSimplified => "zh",
        LangEnum.ChineseTraditional => "cht",
        LangEnum.Cantonese => "yue",
        LangEnum.English => "en",
        LangEnum.Japanese => "jp",
        LangEnum.Korean => "kor",
        LangEnum.French => "fra",
        LangEnum.Spanish => "spa",
        LangEnum.Russian => "ru",
        LangEnum.German => "de",
        LangEnum.Italian => "it",
        LangEnum.Turkish => "tr",
        LangEnum.PortuguesePortugal => "pt",
        LangEnum.PortugueseBrazil => "pot",
        LangEnum.Vietnamese => "vie",
        LangEnum.Indonesian => "id",
        LangEnum.Thai => "th",
        LangEnum.Malay => "may",
        LangEnum.Arabic => "ar",
        LangEnum.Hindi => "hi",
        LangEnum.MongolianCyrillic => null,
        LangEnum.MongolianTraditional => null,
        LangEnum.Khmer => "hkm",
        LangEnum.NorwegianBokmal => "nob",
        LangEnum.NorwegianNynorsk => "nno",
        LangEnum.Persian => "per",
        LangEnum.Swedish => "swe",
        LangEnum.Polish => "pl",
        LangEnum.Dutch => "nl",
        LangEnum.Ukrainian => "ukr",
        LangEnum.Uzbek => "uz",
        LangEnum.Uyghur => null,
        _ => "auto"
    };

    public override void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
    }

    public override void Dispose() => _viewModel?.Dispose();

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

        var salt = new Random().Next(100000).ToString();
        var sign = EncryptString(Settings.AppID + request.Text + salt + Settings.AppKey);

        var options = new Options
        {
            QueryParams = new Dictionary<string, string>
            {
                { "q", request.Text },
                { "from", sourceStr },
                { "to", targetStr },
                { "appid", Settings.AppID },
                { "salt", salt },
                { "sign", sign }
            }
        };

        var response = await Context.HttpService.GetAsync(Url, options, cancellationToken);

        var parseData = JsonNode.Parse(response);
        var errorCode = parseData?["error_code"]?.ToString();
        if (errorCode != null)
        {
            var errorMsg = parseData?["error_msg"]?.ToString() ?? "Unknown error";
            result.Fail($"{errorCode}: {errorMsg}");
            return;
        }

        // 替换原有的 dsts 解析代码，避免使用不存在的 Select 方法
        if (parseData?["trans_result"] is not JsonArray transResultNode)
            throw new Exception($"No result\nRaw:{response}");

        var dsts = transResultNode
            .Select(x => x?["dst"]?.ToString())
            .Where(dst => !string.IsNullOrEmpty(dst));

        var data = string.Join(Environment.NewLine, dsts);
        result.Success(data);
    }

    /// <summary>
    ///     计算MD5值
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    private static string EncryptString(string str)
    {
        // 将字符串转换成字节数组
        var byteOld = Encoding.UTF8.GetBytes(str);
        // 调用加密方法
        var byteNew = MD5.HashData(byteOld);
        // 将加密结果转换为字符串
        var sb = new StringBuilder();
        foreach (var b in byteNew)
            // 将字节转换成16进制表示的字符串，
            sb.Append(b.ToString("x2"));
        // 返回加密的字符串
        return sb.ToString();
    }
}
