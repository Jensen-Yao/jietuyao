using System.IO;
using System.Windows.Controls;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using STranslate.Plugin.Ocr.WindowsOcr.ViewModel;

namespace STranslate.Plugin.Ocr.WindowsOcr;

/// <summary>
/// Windows 本地 OCR（Windows.Media.Ocr）：系统内置引擎，零依赖零上传；
/// 行级包围盒（词框聚合，移植自 SnipTrans OcrService）。
/// </summary>
public class Main : IOcrPlugin
{
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
        LangEnum.German,
        LangEnum.Italian,
        LangEnum.Spanish,
        LangEnum.Russian,
        LangEnum.PortuguesePortugal,
        LangEnum.PortugueseBrazil,
    ];

    public bool SupportBoxPoints() => true;

    public Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new View.SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
    }

    public void Dispose() { }

    /// <summary>LangEnum → BCP-47（Auto/未映射返回 null = 跟随系统）</summary>
    private static string? GetBcp47(LangEnum lang) => lang switch
    {
        LangEnum.ChineseSimplified => "zh-CN",
        LangEnum.ChineseTraditional => "zh-TW",
        LangEnum.Cantonese => "zh-HK",
        LangEnum.English => "en-US",
        LangEnum.Japanese => "ja",
        LangEnum.Korean => "ko",
        LangEnum.French => "fr-FR",
        LangEnum.German => "de-DE",
        LangEnum.Italian => "it-IT",
        LangEnum.Spanish => "es-ES",
        LangEnum.Russian => "ru-RU",
        LangEnum.PortuguesePortugal => "pt-PT",
        LangEnum.PortugueseBrazil => "pt-BR",
        _ => null,
    };

    public async Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken = default)
    {
        var result = new OcrResult();
        try
        {
            var engine = CreateEngine(GetBcp47(request.Language) ?? Settings.LanguageTag);
            if (engine == null)
                return result.Fail("系统无可用 OCR 语言包：设置 → 时间和语言 → 语言 → 添加语言并勾选\"联机语音/OCR\"可选功能");

            using var bitmap = Decode(request.ImageData);
            if (bitmap == null)
                return result.Fail("图片解码失败");

            var software = await CreateSoftwareBitmapAsync(bitmap);
            var ocr = await engine.RecognizeAsync(software);

            // 一个区域：每行一个段落（段落内一行），行包围盒 = 词框 min/max 聚合
            var region = new OcrRegion();
            foreach (var line in ocr.Lines)
            {
                double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                foreach (var word in line.Words)
                {
                    var r = word.BoundingRect;
                    minX = Math.Min(minX, r.X); minY = Math.Min(minY, r.Y);
                    maxX = Math.Max(maxX, r.Right); maxY = Math.Max(maxY, r.Bottom);
                }
                if (minX > maxX) continue;

                var content = new OcrContent { Text = line.Text };
                content.BoxPoints.Add(new BoxPoint((float)minX, (float)minY));
                content.BoxPoints.Add(new BoxPoint((float)maxX, (float)minY));
                content.BoxPoints.Add(new BoxPoint((float)maxX, (float)maxY));
                content.BoxPoints.Add(new BoxPoint((float)minX, (float)maxY));
                result.OcrContents.Add(content);

                var paragraph = new OcrParagraph();
                paragraph.Lines.Add(content);
                region.Paragraphs.Add(paragraph);
            }
            result.Regions.Add(region);
            return result;
        }
        catch (Exception ex)
        {
            return result.Fail($"Windows OCR 失败：{ex.Message}");
        }
    }

    internal static OcrEngine? CreateEngine(string? bcp47)
    {
        if (!string.IsNullOrWhiteSpace(bcp47))
        {
            var lang = new Language(bcp47);
            if (OcrEngine.IsLanguageSupported(lang))
            {
                var byLang = OcrEngine.TryCreateFromLanguage(lang);
                if (byLang != null) return byLang;
            }
        }
        // 设置页手填优先 → 指定语言 → 用户系统语言列表
        return OcrEngine.TryCreateFromUserProfileLanguages();
    }

    internal static System.Drawing.Bitmap? Decode(byte[] data)
    {
        using var ms = new MemoryStream(data);
        try { return new System.Drawing.Bitmap(ms); }
        catch { return null; }
    }

    internal static async Task<SoftwareBitmap> CreateSoftwareBitmapAsync(System.Drawing.Bitmap bitmap)
    {
        using var png = new MemoryStream();
        bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
        png.Position = 0;
        var decoder = await BitmapDecoder.CreateAsync(png.AsRandomAccessStream());
        return await decoder.GetSoftwareBitmapAsync();
    }
}
