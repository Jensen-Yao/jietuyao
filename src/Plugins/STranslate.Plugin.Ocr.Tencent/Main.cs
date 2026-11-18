using CommunityToolkit.Mvvm.ComponentModel;
using STranslate.Plugin.Ocr.Tencent.View;
using STranslate.Plugin.Ocr.Tencent.ViewModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Controls;

namespace STranslate.Plugin.Ocr.Tencent;

public class Main : ObservableObject, IOcrPlugin
{
    private const string Url = "https://ocr.tencentcloudapi.com";
    private const string Version = "2018-11-19";
    private const string Region = "ap-shanghai";

    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    public IEnumerable<LangEnum> SupportedLanguages =>
        Settings.Action switch
        {
            TencentOCRAction.GeneralBasicOCR =>
            [
                LangEnum.Auto,
                LangEnum.ChineseSimplified,
                LangEnum.ChineseTraditional,
                LangEnum.Cantonese,
                LangEnum.English,
                LangEnum.Japanese,
                LangEnum.Korean,
                LangEnum.French,
                LangEnum.Spanish,
                LangEnum.Russian,
                LangEnum.German,
                LangEnum.Italian,
                LangEnum.PortuguesePortugal,
                LangEnum.PortugueseBrazil,
                LangEnum.Vietnamese,
                LangEnum.Thai,
                LangEnum.Malay,
                LangEnum.Arabic,
                LangEnum.Hindi,
                LangEnum.Swedish,
                LangEnum.NorwegianBokmal,
                LangEnum.NorwegianNynorsk,
                LangEnum.Dutch
            ],
            TencentOCRAction.GeneralAccurateOCR =>
            [
                LangEnum.Auto,
                LangEnum.ChineseSimplified,
                LangEnum.ChineseTraditional,
                LangEnum.Cantonese,
                LangEnum.English
            ],
            _ => Enum.GetValues<LangEnum>()
        };

    public bool SupportBoxPoints() => true;

    public Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
    }

    public void Dispose() => _viewModel?.Dispose();

    public async Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken)
    {
        var ocrResult = new OcrResult();
        var base64Str = Convert.ToBase64String(request.ImageData);

        // 1. 构造 body
        string body;
        if (Settings.Action == TencentOCRAction.GeneralBasicOCR)
        {
            var target = LangConverter(request.Language)
                ?? throw new Exception($"unsupportted language[{request.Language}]");
            body = "{\"ImageBase64\":\"" + base64Str + "\",\"LanguageType\":\"" + target + "\"}";
        }
        else
        {
            body = "{\"ImageBase64\":\"" + base64Str + "\"}";
        }

        // 2. 构造签名与请求头
        var host = Url.Replace("https://", "");
        var contentType = "application/json; charset=utf-8";
        var timestamp = ((int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds).ToString();
        var action = Settings.Action.ToString();
        var auth = GetAuth(Settings.SecretId, Settings.SecretKey, host, contentType, timestamp, body);

        var options = new Options
        {
            ContentType = contentType,
            Headers = new Dictionary<string, string>
            {
                { "Host", host },
                { "X-TC-Timestamp", timestamp },
                { "X-TC-Version", Version },
                { "X-TC-Action", action },
                { "X-TC-Region", Region },
                { "X-TC-Token", "" },
                { "X-TC-RequestClient", "SDK_NET_BAREBONE" },
                { "Authorization", auth }
            }
        };

        // 3. POST（传字符串 body，保证签名与实际 body 一致）
        var resp = await Context.HttpService.PostAsync(Url, body, options, cancellationToken);
        if (string.IsNullOrEmpty(resp))
            throw new Exception("请求结果为空");

        // 4. 解析
        var parsedData = JsonSerializer.Deserialize<Root>(resp)
            ?? throw new Exception($"反序列化失败: {resp}");

        // 判断是否出错
        if (parsedData.Response.Error != null)
            return ocrResult.Fail(parsedData.Response.Error.Message);

        // 提取内容
        foreach (var item in parsedData.Response.TextDetections)
        {
            var content = new OcrContent { Text = item.DetectedText };
            foreach (var pg in item.Polygon)
                content.BoxPoints.Add(new BoxPoint(pg.X, pg.Y));
            ocrResult.OcrContents.Add(content);
        }

        return ocrResult;
    }

    /// <summary>
    ///     https://cloud.tencent.com/document/product/866/33526
    /// </summary>
    public string? LangConverter(LangEnum lang)
    {
        return lang switch
        {
            LangEnum.Auto => "auto",
            LangEnum.ChineseSimplified => "zh",
            LangEnum.ChineseTraditional => "zh_rare",
            LangEnum.Cantonese => "zh_rare",
            LangEnum.English => "auto",
            LangEnum.Japanese => "jap",
            LangEnum.Korean => "kor",
            LangEnum.French => "fre",
            LangEnum.Spanish => "spa",
            LangEnum.Russian => "rus",
            LangEnum.German => "ger",
            LangEnum.Italian => "ita",
            LangEnum.Turkish => null,
            LangEnum.PortuguesePortugal => "por",
            LangEnum.PortugueseBrazil => "por",
            LangEnum.Vietnamese => "vie",
            LangEnum.Thai => "tha",
            LangEnum.Malay => "may",
            LangEnum.Arabic => "ara",
            LangEnum.Hindi => "hi",
            LangEnum.Indonesian => null,
            LangEnum.MongolianCyrillic => null,
            LangEnum.MongolianTraditional => null,
            LangEnum.Khmer => null,
            LangEnum.NorwegianBokmal => "nor",
            LangEnum.NorwegianNynorsk => "nor",
            LangEnum.Persian => null,
            LangEnum.Swedish => "swe",
            LangEnum.Polish => null,
            LangEnum.Dutch => "hol",
            LangEnum.Ukrainian => null,
            LangEnum.Uzbek => null,
            _ => "auto"
        };
    }

    #region Tencent Official Support (TC3-HMAC-SHA256)

    protected static string GetAuth(
        string secretId, string secretKey, string host, string contentType,
        string timestamp, string body
    )
    {
        var canonicalURI = "/";
        var canonicalHeaders = "content-type:" + contentType + "\nhost:" + host + "\n";
        var signedHeaders = "content-type;host";
        var hashedRequestPayload = Sha256Hex(body);
        var canonicalRequest = "POST" + "\n"
                                      + canonicalURI + "\n"
                                      + "\n"
                                      + canonicalHeaders + "\n"
                                      + signedHeaders + "\n"
                                      + hashedRequestPayload;

        var algorithm = "TC3-HMAC-SHA256";
        var date = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(int.Parse(timestamp))
            .ToString("yyyy-MM-dd");
        var service = host.Split(".")[0];
        var credentialScope = date + "/" + service + "/" + "tc3_request";
        var hashedCanonicalRequest = Sha256Hex(canonicalRequest);
        var stringToSign = algorithm + "\n"
                                     + timestamp + "\n"
                                     + credentialScope + "\n"
                                     + hashedCanonicalRequest;

        var tc3SecretKey = Encoding.UTF8.GetBytes("TC3" + secretKey);
        var secretDate = HmacSha256(tc3SecretKey, Encoding.UTF8.GetBytes(date));
        var secretService = HmacSha256(secretDate, Encoding.UTF8.GetBytes(service));
        var secretSigning = HmacSha256(secretService, Encoding.UTF8.GetBytes("tc3_request"));
        var signatureBytes = HmacSha256(secretSigning, Encoding.UTF8.GetBytes(stringToSign));
        var signature = BitConverter.ToString(signatureBytes).Replace("-", "").ToLower();

        return algorithm + " "
                         + "Credential=" + secretId + "/" + credentialScope + ", "
                         + "SignedHeaders=" + signedHeaders + ", "
                         + "Signature=" + signature;
    }

    protected static string Sha256Hex(string s)
    {
        using var algo = SHA256.Create();
        var hashbytes = algo.ComputeHash(Encoding.UTF8.GetBytes(s));
        var builder = new StringBuilder();
        for (var i = 0; i < hashbytes.Length; ++i)
            builder.Append(hashbytes[i].ToString("x2"));

        return builder.ToString();
    }

    private static byte[] HmacSha256(byte[] key, byte[] msg)
    {
        using var mac = new HMACSHA256(key);
        return mac.ComputeHash(msg);
    }

    #endregion Tencent Official Support (TC3-HMAC-SHA256)

    #region Response DTO

#pragma warning disable IDE1006 // 命名样式
    public class PolygonItem
    {
        [JsonPropertyName("X")] public int X { get; set; }

        [JsonPropertyName("Y")] public int Y { get; set; }
    }

    public class TextDetectionsItem
    {
        [JsonPropertyName("DetectedText")] public string DetectedText { get; set; } = string.Empty;

        [JsonPropertyName("Confidence")] public int Confidence { get; set; }

        [JsonPropertyName("Polygon")] public List<PolygonItem> Polygon { get; set; } = [];

        [JsonPropertyName("AdvancedInfo")] public string AdvancedInfo { get; set; } = string.Empty;
    }

    public class Error
    {
        [JsonPropertyName("Code")] public string Code { get; set; } = string.Empty;

        [JsonPropertyName("Message")] public string Message { get; set; } = string.Empty;
    }

    public class Response
    {
        [JsonPropertyName("TextDetections")] public List<TextDetectionsItem> TextDetections { get; set; } = [];

        [JsonPropertyName("Error")] public Error? Error { get; set; }

        [JsonPropertyName("RequestId")] public string RequestId { get; set; } = string.Empty;

        [JsonPropertyName("Language")] public string Language { get; set; } = string.Empty;
    }

    public class Root
    {
        [JsonPropertyName("Response")] public Response Response { get; set; } = new();
    }
#pragma warning restore IDE1006 // 命名样式

    #endregion Response DTO
}
