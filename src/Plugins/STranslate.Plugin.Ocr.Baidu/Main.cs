using CommunityToolkit.Mvvm.ComponentModel;
using STranslate.Plugin.Ocr.Baidu.View;
using STranslate.Plugin.Ocr.Baidu.ViewModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Controls;

namespace STranslate.Plugin.Ocr.Baidu;

public class Main : ObservableObject, IOcrPlugin
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    public IEnumerable<LangEnum> SupportedLanguages =>
        Settings.Action switch
        {
            BaiduOCRAction.Accurate or BaiduOCRAction.AccurateBasic =>
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
                LangEnum.Turkish,
                LangEnum.PortuguesePortugal,
                LangEnum.PortugueseBrazil,
                LangEnum.Vietnamese,
                LangEnum.Indonesian,
                LangEnum.Thai,
                LangEnum.Malay,
                LangEnum.Arabic,
                LangEnum.Hindi,
                LangEnum.Swedish,
                LangEnum.Polish,
                LangEnum.Dutch,
            ],
            BaiduOCRAction.General or BaiduOCRAction.GeneralBasic =>
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
        var token = await GetAccessTokenAsync(Settings.ApiKey, Settings.SecretKey, cancellationToken);
        var url = $"https://aip.baidubce.com/rest/2.0/ocr/v1/{Settings.Action.GetDescription()}?access_token={token}";
        var base64Str = Convert.ToBase64String(request.ImageData);
        var target = LangConverter(request.Language) ?? throw new Exception($"unsupportted language[{request.Language}]");
        var options = new Options
        {
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/x-www-form-urlencoded" },
                { "Accept", "application/json" }
            }
        };
        var formData = new Dictionary<string, string>
        {
            { "image", base64Str },
            { "language_type", target },
            { "detect_direction", "false" },
            { "detect_language", "false" },
            { "vertexes_location", "false" },
            { "paragraph", "true" },
            { "probability", "false" }
        };

        var response = await Context.HttpService.PostFormAsync(url, formData, options, cancellationToken);
        var parsedData = JsonSerializer.Deserialize<Root>(response) ?? throw new Exception("Parse ocr result failed");

        // 判断是否出错
        if (parsedData.error_code != 0) return ocrResult.Fail(parsedData.error_msg);

        var contents = parsedData.words_result.Select(CreateContent).ToList();
        ocrResult.OcrContents.AddRange(contents);
        AddStructuredLayout(ocrResult, contents, parsedData.paragraphs_result);

        return ocrResult;
    }

    /**
    * 使用 AK，SK 生成鉴权签名（Access Token）
    * @return 鉴权签名信息（Access Token）
    */
    public async Task<string> GetAccessTokenAsync(string API_KEY, string SECRET_KEY, CancellationToken token = default)
    {
        const string url = "https://aip.baidubce.com/oauth/2.0/token";
        var options = new Options
        {
            QueryParams = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", API_KEY },
                { "client_secret", SECRET_KEY }
            }
        };
        var response = await Context.HttpService.PostAsync(url, string.Empty, options, token);
        var parsedData = JsonNode.Parse(response);
        var access_token = parsedData?["access_token"]?.ToString() ?? throw new Exception("Get accesstoken failed");
        return access_token;
    }

    /// <summary>
    ///     https://ai.baidu.com/ai-doc/OCR/1k3h7y3db
    /// </summary>
    /// <param name="lang"></param>
    /// <returns></returns>
    public string? LangConverter(LangEnum lang)
    {
        return Settings.Action switch
        {
            BaiduOCRAction.Accurate => AccurateType(lang),
            BaiduOCRAction.AccurateBasic => AccurateType(lang),
            BaiduOCRAction.General => GeneralType(lang),
            BaiduOCRAction.GeneralBasic => GeneralType(lang),
            _ => null
        };
    }

    /// <summary>
    ///     高精度版支持语言
    /// </summary>
    /// <param name="lang"></param>
    private string? AccurateType(LangEnum lang)
    {
        return lang switch
        {
            LangEnum.Auto => "auto_detect",
            LangEnum.ChineseSimplified => "CHN_ENG",
            LangEnum.ChineseTraditional => "CHN_ENG",
            LangEnum.Cantonese => "CHN_ENG",
            LangEnum.English => "ENG",
            LangEnum.Japanese => "JAP",
            LangEnum.Korean => "KOR",
            LangEnum.French => "FRE",
            LangEnum.Spanish => "SPA",
            LangEnum.Russian => "RUS",
            LangEnum.German => "GER",
            LangEnum.Italian => "ITA",
            LangEnum.Turkish => "TUR",
            LangEnum.PortuguesePortugal => "POR",
            LangEnum.PortugueseBrazil => "POR",
            LangEnum.Vietnamese => "VIE",
            LangEnum.Indonesian => "IND",
            LangEnum.Thai => "THA",
            LangEnum.Malay => "MAL",
            LangEnum.Arabic => "ARA",
            LangEnum.Hindi => "HIN",
            LangEnum.MongolianCyrillic => null,
            LangEnum.MongolianTraditional => null,
            LangEnum.Khmer => null,
            LangEnum.NorwegianBokmal => null,
            LangEnum.NorwegianNynorsk => null,
            LangEnum.Persian => null,
            LangEnum.Swedish => "SWE",
            LangEnum.Polish => "POL",
            LangEnum.Dutch => "DUT",
            LangEnum.Ukrainian => null,
            LangEnum.Uzbek => null,
            _ => "auto_detect"
        };
    }

    /// <summary>
    ///     标准版支持语言
    /// </summary>
    /// <param name="lang"></param>
    /// <returns></returns>
    private string? GeneralType(LangEnum lang)
    {
        return lang switch
        {
            LangEnum.Auto => "CHN_ENG",
            LangEnum.ChineseSimplified => "CHN_ENG",
            LangEnum.ChineseTraditional => "CHN_ENG",
            LangEnum.Cantonese => "CHN_ENG",
            LangEnum.English => "ENG",
            LangEnum.Japanese => "JAP",
            LangEnum.Korean => "KOR",
            LangEnum.French => "FRE",
            LangEnum.Spanish => "SPA",
            LangEnum.Russian => "RUS",
            LangEnum.German => "GER",
            LangEnum.Italian => "ITA",
            LangEnum.Turkish => null,
            LangEnum.PortuguesePortugal => "POR",
            LangEnum.PortugueseBrazil => "POR",
            LangEnum.Vietnamese => null,
            LangEnum.Indonesian => null,
            LangEnum.Thai => null,
            LangEnum.Malay => null,
            LangEnum.Arabic => null,
            LangEnum.Hindi => null,
            LangEnum.MongolianCyrillic => null,
            LangEnum.MongolianTraditional => null,
            LangEnum.Khmer => null,
            LangEnum.NorwegianBokmal => null,
            LangEnum.NorwegianNynorsk => null,
            LangEnum.Persian => null,
            LangEnum.Swedish => null,
            LangEnum.Polish => null,
            LangEnum.Dutch => null,
            LangEnum.Ukrainian => null,
            LangEnum.Uzbek => null,
            _ => "CHN_ENG"
        };
    }

    public List<BoxPoint> Converter(Location location)
    {
        return
        [
            //left top
            new BoxPoint(location.left, location.top),

            //right top
            new BoxPoint(location.left + location.width, location.top),

            //right bottom
            new BoxPoint(location.left + location.width, location.top + location.height),

            //left bottom
            new BoxPoint(location.left, location.top + location.height)
        ];
    }

    private OcrContent CreateContent(Words_resultItem item)
    {
        var content = new OcrContent { Text = item.words };
        foreach (var point in Converter(item.location).Where(point => point.X != 0 || point.Y != 0))
        {
            content.BoxPoints.Add(new BoxPoint(point.X, point.Y));
        }

        return content;
    }

    private static void AddStructuredLayout(
        OcrResult ocrResult,
        IReadOnlyList<OcrContent> contents,
        IReadOnlyList<Paragraphs_resultItem> paragraphsResult)
    {
        if (contents.Count == 0 || paragraphsResult.Count == 0)
            return;

        var region = new OcrRegion();
        foreach (var paragraphResult in paragraphsResult)
        {
            var paragraph = new OcrParagraph();
            foreach (var index in paragraphResult.words_result_idx)
            {
                if ((uint)index >= (uint)contents.Count)
                    continue;

                var content = contents[index];
                if (string.IsNullOrWhiteSpace(content.Text))
                    continue;

                paragraph.Lines.Add(CloneContent(content));
            }

            if (paragraph.Lines.Count == 0)
                continue;

            paragraph.BoxPoints = CreateUnionBoxPoints(paragraph.Lines.Select(line => line.BoxPoints));
            region.Paragraphs.Add(paragraph);
        }

        if (region.Paragraphs.Count == 0)
            return;

        region.BoxPoints = CreateUnionBoxPoints(region.Paragraphs.Select(paragraph => paragraph.BoxPoints));
        ocrResult.Regions.Add(region);
    }

    private static OcrContent CloneContent(OcrContent content) => new()
    {
        Text = content.Text,
        BoxPoints = CloneBoxPoints(content.BoxPoints)
    };

    private static List<BoxPoint> CloneBoxPoints(IEnumerable<BoxPoint> points) =>
        points.Select(point => new BoxPoint(point.X, point.Y)).ToList();

    private static List<BoxPoint> CreateUnionBoxPoints(IEnumerable<IReadOnlyList<BoxPoint>> boxPointGroups)
    {
        var validGroups = boxPointGroups.Where(points => points.Count > 0).ToList();
        if (validGroups.Count == 0)
            return [];

        var minX = validGroups.Min(points => points.Min(point => point.X));
        var minY = validGroups.Min(points => points.Min(point => point.Y));
        var maxX = validGroups.Max(points => points.Max(point => point.X));
        var maxY = validGroups.Max(points => points.Max(point => point.Y));

        return
        [
            new(minX, minY),
            new(maxX, minY),
            new(maxX, maxY),
            new(minX, maxY)
        ];
    }

#pragma warning disable IDE1006 // 命名样式
    public class Location
    {
        /// <summary>
        /// </summary>
        public int top { get; set; }

        /// <summary>
        /// </summary>
        public int left { get; set; }

        /// <summary>
        /// </summary>
        public int width { get; set; }

        /// <summary>
        /// </summary>
        public int height { get; set; }
    }

    public class Words_resultItem
    {
        /// <summary>
        /// </summary>
        public string words { get; set; } = string.Empty;

        /// <summary>
        /// </summary>
        public Location location { get; set; } = new();
    }

    public class Paragraphs_resultItem
    {
        /// <summary>
        /// </summary>
        public List<int> words_result_idx { get; set; } = [];
    }

    public class Root
    {
        /// <summary>
        /// </summary>
        public List<Words_resultItem> words_result { get; set; } = [];

        /// <summary>
        /// </summary>
        public List<Paragraphs_resultItem> paragraphs_result { get; set; } = [];

        /// <summary>
        /// </summary>
        public int paragraphs_result_num { get; set; }

        /// <summary>
        /// </summary>
        public int words_result_num { get; set; }

        /// <summary>
        /// </summary>
        public long log_id { get; set; }

        /// <summary>
        /// </summary>
        public string error_msg { get; set; } = string.Empty;

        /// <summary>
        /// </summary>
        public int error_code { get; set; }
    }
#pragma warning restore IDE1006 // 命名样式
}
