using STranslate.Plugin.Translate.Amazon.ViewModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows.Controls;

namespace STranslate.Plugin.Translate.Amazon;

public class Main : TranslatePluginBase
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    private const string Target = "AWSShineFrontendService_20170701.TranslateText";
    private const string ContentType = "application/x-amz-json-1.1";

    public override Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new View.SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public override string? GetSourceLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => null, // Amazon 必须显式源语言，Auto 时用 en 检测由用户保证
        LangEnum.ChineseSimplified => "zh",
        LangEnum.ChineseTraditional => "zh-TW",
        LangEnum.Cantonese => "zh",
        LangEnum.English => "en",
        LangEnum.Japanese => "ja",
        LangEnum.Korean => "ko",
        LangEnum.French => "fr",
        LangEnum.Spanish => "es",
        LangEnum.Russian => "ru",
        LangEnum.German => "de",
        LangEnum.Italian => "it",
        LangEnum.Turkish => "tr",
        LangEnum.PortuguesePortugal => "pt",
        LangEnum.PortugueseBrazil => "pt",
        LangEnum.Vietnamese => "vi",
        LangEnum.Indonesian => "id",
        LangEnum.Thai => "th",
        LangEnum.Malay => "ms",
        LangEnum.Arabic => "ar",
        LangEnum.Hindi => "hi",
        LangEnum.MongolianCyrillic => "mn",
        LangEnum.MongolianTraditional => null,
        LangEnum.Khmer => "km",
        LangEnum.NorwegianBokmal => "nb",
        LangEnum.NorwegianNynorsk => "nn",
        LangEnum.Persian => "fa",
        LangEnum.Swedish => "sv",
        LangEnum.Polish => "pl",
        LangEnum.Dutch => "nl",
        LangEnum.Ukrainian => "uk",
        LangEnum.Uzbek => "uz",
        LangEnum.Uyghur => null,
        _ => null
    };

    public override string? GetTargetLanguage(LangEnum langEnum) => GetSourceLanguage(langEnum) is null && langEnum == LangEnum.Auto
        ? null
        : GetSourceLanguage(langEnum);

    public override void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
    }

    public override void Dispose() => _viewModel?.Dispose();

    public override async Task TranslateAsync(TranslateRequest request, TranslateResult result, CancellationToken cancellationToken = default)
    {
        var sourceStr = GetSourceLanguage(request.SourceLang) ?? "auto";
        var targetStr = GetTargetLanguage(request.TargetLang);
        if (targetStr is null or "auto")
        {
            result.Fail(Context.GetTranslation("UnsupportedTargetLang"));
            return;
        }

        if (string.IsNullOrWhiteSpace(Settings.AccessKeyId) || string.IsNullOrWhiteSpace(Settings.SecretAccessKey) ||
            string.IsNullOrWhiteSpace(Settings.Region))
        {
            result.Fail("Amazon Translate credentials are not configured");
            return;
        }

        var host = $"translate.{Settings.Region}.amazonaws.com";
        var url = $"https://{host}/";
        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ");
        var dateStamp = now.ToString("yyyyMMdd");

        // 预序列化载荷（签名与请求体必须字节一致）
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            Text = request.Text,
            SourceLanguageCode = sourceStr == "auto" ? DetectHeuristic(request.Text) : sourceStr,
            TargetLanguageCode = targetStr,
        });

        var headers = new Dictionary<string, string>
        {
            ["content-type"] = ContentType,
            ["x-amz-date"] = amzDate,
            ["x-amz-target"] = Target,
        };
        if (!string.IsNullOrWhiteSpace(Settings.SessionToken))
            headers["x-amz-security-token"] = Settings.SessionToken;

        var signedHeaders = string.Join(";", headers.Keys.OrderBy(k => k));
        var canonicalHeaders = string.Concat(headers.Keys.OrderBy(k => k)
            .Select(k => $"{k}:{headers[k].Trim()}\n"));
        var payloadHash = Hex(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var canonicalRequest = string.Join("\n", "POST", "/", "", canonicalHeaders, signedHeaders, payloadHash);
        var scope = $"{dateStamp}/{Settings.Region}/translate/aws4_request";
        var stringToSign = string.Join("\n",
            "AWS4-HMAC-SHA256", amzDate, scope, Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))));

        var kDate = Hmac(Encoding.UTF8.GetBytes("AWS4" + Settings.SecretAccessKey), dateStamp);
        var kRegion = Hmac(kDate, Settings.Region);
        var kService = Hmac(kRegion, "translate");
        var kSigning = Hmac(kService, "aws4_request");
        var signature = Hex(Hmac(kSigning, stringToSign));

        headers["Authorization"] =
            $"AWS4-HMAC-SHA256 Credential={Settings.AccessKeyId}/{scope}, SignedHeaders={signedHeaders}, Signature={signature}";
        headers["host"] = host;

        var options = new Options { Headers = headers, ContentType = ContentType };
        var response = await Context.HttpService.PostAsync(url, payload, options, cancellationToken);

        var parsed = JsonNode.Parse(response);
        var translated = parsed?["TranslatedText"]?.ToString();
        if (string.IsNullOrEmpty(translated))
        {
            var message = parsed?["message"]?.ToString() ?? parsed?["__type"]?.ToString();
            result.Fail(string.IsNullOrEmpty(message) ? $"Invalid response\nRaw:{response}" : message);
            return;
        }

        var detected = parsed?["SourceLanguageCode"]?.ToString();
        result.Success(translated);
    }

    /// <summary>Auto 源语言的轻量启发检测（CJK 判定）</summary>
    private static string DetectHeuristic(string text)
    {
        if (text.Length == 0) return "en";
        var cjkCount = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
        return cjkCount * 2 >= text.Length ? "zh" : "en";
    }

    private static byte[] Hmac(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string Hex(byte[] bytes) => Convert.ToHexStringLower(bytes);
}
