using CommunityToolkit.Mvvm.ComponentModel;
using STranslate.Plugin.Ocr.OcrSpace.View;
using STranslate.Plugin.Ocr.OcrSpace.ViewModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Controls;

namespace STranslate.Plugin.Ocr.OcrSpace;

public class Main : ObservableObject, IOcrPlugin
{
    private const string Url = "https://api.ocr.space/parse/image";

    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    public IEnumerable<LangEnum> SupportedLanguages =>
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
        LangEnum.Thai,
        LangEnum.Arabic,
        LangEnum.Swedish,
        LangEnum.Dutch,
        LangEnum.Polish,
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

        // 1. 构造表单：base64Image 需带内容类型前缀
        var base64Str = Convert.ToBase64String(request.ImageData);
        var base64Image = GetBase64ImagePrefix(request.ImageData) + base64Str;

        var langCode = LangConverter(request.Language) ?? "eng";
        // Engine 1 不支持 auto，回退 eng 避免识别失败
        if (Settings.Engine == OcrSpaceEngine.Engine1 && langCode == "auto")
            langCode = "eng";

        var engine = Settings.Engine switch
        {
            OcrSpaceEngine.Engine1 => "1",
            OcrSpaceEngine.Engine2 => "2",
            OcrSpaceEngine.Engine3 => "3",
            _ => "2"
        };

        var formData = new Dictionary<string, string>
        {
            { "base64Image", base64Image },
            { "language", langCode },
            { "isOverlayRequired", "true" },
            { "scale", "true" },
            { "isTable", "true" },
            { "OCREngine", engine }
        };

        var options = new Options
        {
            Headers = new Dictionary<string, string>
            {
                { "apikey", Settings.ApiKey }
            }
        };

        // 2. POST
        var resp = await Context.HttpService.PostFormAsync(Url, formData, options, cancellationToken);
        if (string.IsNullOrEmpty(resp))
            throw new Exception("请求结果为空");

        // 3. 解析
        var parsedData = JsonSerializer.Deserialize<Root>(resp)
            ?? throw new Exception($"反序列化失败: {resp}");

        // 4. 判断错误：IsErroredOnProcessing 或 OCRExitCode 为 3/4 视为失败
        if (parsedData.IsErroredOnProcessing || parsedData.OCRExitCode is 3 or 4)
            return ocrResult.Fail(parsedData.ErrorMessage ?? parsedData.ErrorDetails ?? resp);

        // 5. 提取内容
        if (parsedData.ParsedResults is null || parsedData.ParsedResults.Count == 0)
            return ocrResult;

        foreach (var page in parsedData.ParsedResults)
        {
            // 单页失败则跳过，不中断其余页
            if (page.FileParseExitCode != 1)
                continue;

            var contents = BuildContentsFromLines(page.TextOverlay?.Lines);
            if (contents.Count > 0)
            {
                ocrResult.OcrContents.AddRange(contents);
            }
            else if (!string.IsNullOrEmpty(page.ParsedText))
            {
                // overlay 无词时回退到整页文本
                ocrResult.OcrContents.Add(new OcrContent { Text = page.ParsedText.Trim() });
            }
        }

        return ocrResult;
    }

    /// <summary>
    ///     https://ocr.space/ocrapi 语言码为 3 字母；Engine 2/3 支持 auto 自动检测。
    /// </summary>
    public string? LangConverter(LangEnum lang)
    {
        return lang switch
        {
            LangEnum.Auto => "auto",
            LangEnum.ChineseSimplified => "chs",
            LangEnum.ChineseTraditional => "cht",
            LangEnum.Cantonese => "cht",
            LangEnum.English => "eng",
            LangEnum.Japanese => "jpn",
            LangEnum.Korean => "kor",
            LangEnum.French => "fre",
            LangEnum.Spanish => "spa",
            LangEnum.Russian => "rus",
            LangEnum.German => "ger",
            LangEnum.Italian => "ita",
            LangEnum.Turkish => "tur",
            LangEnum.PortuguesePortugal => "por",
            LangEnum.PortugueseBrazil => "por",
            LangEnum.Vietnamese => "vnm",
            LangEnum.Thai => "tha",
            LangEnum.Arabic => "ara",
            LangEnum.Swedish => "swe",
            LangEnum.Dutch => "dut",
            LangEnum.Polish => "pol",
            LangEnum.Ukrainian => "ukr",
            // OCR.Space 无以下语言码，回退 auto（Engine 2/3 自动检测兜底）
            LangEnum.Malay => "auto",
            LangEnum.Hindi => "auto",
            LangEnum.Indonesian => "auto",
            LangEnum.MongolianCyrillic => "auto",
            LangEnum.MongolianTraditional => "auto",
            LangEnum.Khmer => "auto",
            LangEnum.NorwegianBokmal => "auto",
            LangEnum.NorwegianNynorsk => "auto",
            LangEnum.Persian => "auto",
            LangEnum.Uzbek => "auto",
            _ => "auto"
        };
    }

    /// <summary>
    ///     根据图片魔数检测内容类型前缀，OCR.Space 要求 base64 字符串以 data:&lt;type&gt;;base64, 开头。
    /// </summary>
    internal static string GetBase64ImagePrefix(byte[] imageData)
    {
        if (imageData.Length >= 4 &&
            imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
            return "data:image/png;base64,";

        if (imageData.Length >= 3 &&
            imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
            return "data:image/jpeg;base64,";

        return "data:image/jpeg;base64,";
    }

    /// <summary>
    ///     将 OCR.Space 的 TextOverlay.Lines 还原为带像素坐标的 OcrContent 列表。
    ///     每个行作为一个 OcrContent：文本取 LineText，坐标为该行所有词的外接矩形（4 点）。
    /// </summary>
    internal static List<OcrContent> BuildContentsFromLines(List<Line>? lines)
    {
        var contents = new List<OcrContent>();
        if (lines is null || lines.Count == 0)
            return contents;

        foreach (var line in lines)
        {
            // 行文本为空则跳过（LineText 缺失时也跳过，避免空内容）
            if (string.IsNullOrWhiteSpace(line.LineText))
                continue;

            // 无词坐标时仅保留行文本，不添加坐标框
            if (line.Words is null || line.Words.Count == 0)
            {
                contents.Add(new OcrContent { Text = line.LineText.Trim() });
                continue;
            }

            // 求该行所有词的外接矩形：min Left/Top, max(Left+Width)/max(Top+Height)
            var minLeft = float.MaxValue;
            var minTop = float.MaxValue;
            var maxRight = float.MinValue;
            var maxBottom = float.MinValue;
            foreach (var word in line.Words)
            {
                if (word.Width <= 0 || word.Height <= 0)
                    continue;

                minLeft = Math.Min(minLeft, word.Left);
                minTop = Math.Min(minTop, word.Top);
                maxRight = Math.Max(maxRight, word.Left + word.Width);
                maxBottom = Math.Max(maxBottom, word.Top + word.Height);
            }

            // 所有词坐标无效时仅保留行文本
            if (minLeft == float.MaxValue)
            {
                contents.Add(new OcrContent { Text = line.LineText.Trim() });
                continue;
            }

            contents.Add(new OcrContent
            {
                Text = line.LineText.Trim(),
                BoxPoints =
                [
                    new BoxPoint(minLeft, minTop),      // 左上
                    new BoxPoint(maxRight, minTop),     // 右上
                    new BoxPoint(maxRight, maxBottom),  // 右下
                    new BoxPoint(minLeft, maxBottom)    // 左下
                ]
            });
        }

        return contents;
    }

    #region Response DTO

#pragma warning disable IDE1006 // 命名样式
    public class Word
    {
        [JsonPropertyName("WordText")] public string WordText { get; set; } = string.Empty;

        [JsonPropertyName("Left")] public float Left { get; set; }

        [JsonPropertyName("Top")] public float Top { get; set; }

        [JsonPropertyName("Height")] public float Height { get; set; }

        [JsonPropertyName("Width")] public float Width { get; set; }
    }

    public class Line
    {
        [JsonPropertyName("LineText")] public string LineText { get; set; } = string.Empty;

        [JsonPropertyName("Words")] public List<Word> Words { get; set; } = [];

        [JsonPropertyName("MaxHeight")] public float MaxHeight { get; set; }

        [JsonPropertyName("MinTop")] public float MinTop { get; set; }
    }

    public class TextOverlay
    {
        [JsonPropertyName("Lines")] public List<Line> Lines { get; set; } = [];

        [JsonPropertyName("HasOverlay")] public bool HasOverlay { get; set; }

        [JsonPropertyName("Message")] public string? Message { get; set; }
    }

    public class ParsedResult
    {
        [JsonPropertyName("TextOverlay")] public TextOverlay? TextOverlay { get; set; }

        [JsonPropertyName("FileParseExitCode")] public int FileParseExitCode { get; set; }

        [JsonPropertyName("ParsedText")] public string? ParsedText { get; set; }

        [JsonPropertyName("ErrorMessage")] public string? ErrorMessage { get; set; }

        [JsonPropertyName("ErrorDetails")] public string? ErrorDetails { get; set; }
    }

    public class Root
    {
        [JsonPropertyName("ParsedResults")] public List<ParsedResult> ParsedResults { get; set; } = [];

        [JsonPropertyName("OCRExitCode")] public int OCRExitCode { get; set; }

        [JsonPropertyName("IsErroredOnProcessing")] public bool IsErroredOnProcessing { get; set; }

        [JsonPropertyName("ErrorMessage")] public string? ErrorMessage { get; set; }

        [JsonPropertyName("ErrorDetails")] public string? ErrorDetails { get; set; }

        [JsonPropertyName("ProcessingTimeInMilliseconds")] public string? ProcessingTimeInMilliseconds { get; set; }
    }
#pragma warning restore IDE1006 // 命名样式

    #endregion Response DTO
}
