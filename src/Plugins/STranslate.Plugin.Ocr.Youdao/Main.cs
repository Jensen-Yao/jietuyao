using CommunityToolkit.Mvvm.ComponentModel;
using STranslate.Plugin.Ocr.Youdao.View;
using STranslate.Plugin.Ocr.Youdao.ViewModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows.Controls;

namespace STranslate.Plugin.Ocr.Youdao;

public class Main : ObservableObject, IOcrPlugin
{
    private const string Url = "https://openapi.youdao.com/ocrapi";

    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    public IEnumerable<LangEnum> SupportedLanguages =>
    [
        LangEnum.Auto,
        LangEnum.ChineseSimplified,
        LangEnum.ChineseTraditional,
        LangEnum.English,
        LangEnum.Japanese,
        LangEnum.Korean,
        LangEnum.French,
        LangEnum.Spanish,
        LangEnum.Russian,
        LangEnum.German,
        LangEnum.Italian,
        LangEnum.Turkish,
        LangEnum.PortuguesePortugal,
        LangEnum.PortugueseBrazil,
        LangEnum.Indonesian,
        LangEnum.Thai,
        LangEnum.Malay,
        LangEnum.Arabic,
        LangEnum.Hindi,
        LangEnum.MongolianCyrillic,
        LangEnum.MongolianTraditional,
        LangEnum.Khmer,
        LangEnum.NorwegianBokmal,
        LangEnum.NorwegianNynorsk,
        LangEnum.Swedish,
        LangEnum.Polish,
        LangEnum.Dutch,
        LangEnum.Ukrainian
    ];

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
        var target = LangConverter(request.Language)
            ?? throw new Exception($"unsupportted language[{request.Language}]");

        var base64Str = Convert.ToBase64String(request.ImageData);
        var formData = new Dictionary<string, string>
        {
            { "img", base64Str },
            { "langType", target },
            { "detectType", "10012" },
            { "imageType", "1" },
            { "docType", "json" }
        };
        AddAuthParams(Settings.AppKey, Settings.AppSecret, formData);

        var resp = await Context.HttpService.PostFormAsync(Url, formData, null, cancellationToken);
        if (string.IsNullOrEmpty(resp))
            throw new Exception("请求结果为空");

        // 解析JSON数据
        var parsedData = JsonNode.Parse(resp) ?? throw new Exception($"反序列化失败: {resp}");

        if (parsedData["errorCode"]?.ToString() != "0")
            return ocrResult.Fail(parsedData["msg"]?.ToString() ?? resp);

        // 提取识别内容
        var regions = parsedData["Result"]?["regions"]?.AsArray();
        if (regions is null)
            return ocrResult;

        foreach (var region in regions)
        {
            // 取每 region 的首行文本与坐标
            var line = region?["lines"]?.AsArray()?.FirstOrDefault();
            var text = line?["text"]?.ToString();
            if (string.IsNullOrEmpty(text))
                continue;

            var content = new OcrContent { Text = text };
            ocrResult.OcrContents.Add(content);

            // 处理区域信息: boundingBox = "x1,y1,x2,y2,x3,y3,x4,y4"（左上、右上、右下、左下）
            var location = line?["boundingBox"]?.ToString();
            if (string.IsNullOrEmpty(location))
                continue;
            // 容错解析: 非法坐标时仅保留文本，不添加坐标框
            var parts = location.Split(',');
            if (parts.Length < 8)
                continue;
            var nums = new int[8];
            var ok = true;
            for (var i = 0; i < 8; i++)
            {
                if (!int.TryParse(parts[i].Trim(), out nums[i]))
                {
                    ok = false;
                    break;
                }
            }
            if (!ok)
                continue;
            content.BoxPoints.Add(new BoxPoint(nums[0], nums[1]));
            content.BoxPoints.Add(new BoxPoint(nums[2], nums[3]));
            content.BoxPoints.Add(new BoxPoint(nums[4], nums[5]));
            content.BoxPoints.Add(new BoxPoint(nums[6], nums[7]));
        }

        return ocrResult;
    }

    /// <summary>
    ///     https://ai.youdao.com/DOCSIRMA/html/ocr/api/tyocr/index.html
    /// </summary>
    public string? LangConverter(LangEnum lang)
    {
        return lang switch
        {
            LangEnum.Auto => "auto",
            LangEnum.ChineseSimplified => "zh-CHS",
            LangEnum.ChineseTraditional => "zh-CHT",
            LangEnum.Cantonese => null,
            LangEnum.English => "en",
            LangEnum.Japanese => "jp",
            LangEnum.Korean => "ko",
            LangEnum.French => "fr",
            LangEnum.Spanish => "es",
            LangEnum.Russian => "ru",
            LangEnum.German => "de",
            LangEnum.Italian => "it",
            LangEnum.Turkish => "tr",
            LangEnum.PortuguesePortugal => "pt",
            LangEnum.PortugueseBrazil => "pt",
            LangEnum.Vietnamese => null,
            LangEnum.Indonesian => "id",
            LangEnum.Thai => "th",
            LangEnum.Malay => "ms",
            LangEnum.Arabic => "ar",
            LangEnum.Hindi => "hi",
            LangEnum.MongolianCyrillic => "mn",
            LangEnum.MongolianTraditional => "mn",
            LangEnum.Khmer => "km",
            LangEnum.NorwegianBokmal => "no",
            LangEnum.NorwegianNynorsk => "no",
            LangEnum.Persian => null,
            LangEnum.Swedish => "sv",
            LangEnum.Polish => "pl",
            LangEnum.Dutch => "nl",
            LangEnum.Ukrainian => "uk",
            LangEnum.Uzbek => null,
            _ => "auto"
        };
    }

    #region Youdao V3 Auth (移植自有道翻译插件)

    /*
        添加鉴权相关参数 -
        appKey : 应用ID
        salt : 随机值
        curtime : 当前时间戳(秒)
        signType : 签名版本
        sign : 请求签名

        @param appKey    您的应用ID
        @param appSecret 您的应用密钥
        @param paramsMap 请求参数表
    */
    private static void AddAuthParams(string appKey, string appSecret, Dictionary<string, string> paramsMap)
    {
        var q = paramsMap.TryGetValue("q", out string? value) ? value : paramsMap["img"];
        var salt = Guid.NewGuid().ToString();
        var curtime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() + "";
        var sign = CalculateSign(appKey, appSecret, q, salt, curtime);
        paramsMap.Add("appKey", appKey);
        paramsMap.Add("salt", salt);
        paramsMap.Add("curtime", curtime);
        paramsMap.Add("signType", "v3");
        paramsMap.Add("sign", sign);
    }

    /*
        计算鉴权签名 -
        计算方式 : sign = sha256(appKey + input(q) + salt + curtime + appSecret)

        @param appKey    您的应用ID
        @param appSecret 您的应用密钥
        @param q         请求内容
        @param salt      随机值
        @param curtime   当前时间戳(秒)
        @return 鉴权签名sign
    */
    private static string CalculateSign(string appKey, string appSecret, string q, string salt, string curtime)
    {
        var strSrc = appKey + GetInput(q) + salt + curtime + appSecret;
        return Encrypt(strSrc);
    }

    private static string Encrypt(string strSrc)
    {
        var inputBytes = Encoding.UTF8.GetBytes(strSrc);
        var hashedBytes = SHA256.HashData(inputBytes);
        return BitConverter.ToString(hashedBytes).Replace("-", "").ToUpperInvariant();
    }

    private static string GetInput(string q)
    {
        if (q == null) return "";
        var len = q.Length;
        return len <= 20 ? q : q[..10] + len + q.Substring(len - 10, 10);
    }

    #endregion Youdao V3 Auth (移植自有道翻译插件)
}
