namespace STranslate.Plugin;

/// <summary>
/// OCR 插件接口，定义了支持的语言和识别方法。
/// </summary>
public interface IOcrPlugin : IPlugin
{
    /// <summary>
    /// 获取插件支持的语言列表。
    /// </summary>
    IEnumerable<LangEnum> SupportedLanguages { get; }

    /// <summary>
    /// 是否返回图片像素坐标的文本框。图片翻译需要该能力，普通 OCR 可不支持。
    /// </summary>
    /// <returns>支持返回文本坐标框则为 true。</returns>
    bool SupportBoxPoints() => false;

    /// <summary>
    /// 异步识别图片中的文本。
    /// </summary>
    /// <param name="request">OCR 请求参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>识别结果。</returns>
    Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// OCR 请求参数，包含待识别图片数据和目标语言。
/// </summary>
/// <param name="ImageData">图片数据</param>
/// <param name="Language">语言</param>
/// <param name="PixelWidth">图片像素宽度。</param>
/// <param name="PixelHeight">图片像素高度。</param>
public record OcrRequest(byte[] ImageData, LangEnum Language, int PixelWidth = 0, int PixelHeight = 0);

/// <summary>
/// OCR 识别结果，包含识别出的文本、内容列表、语言、耗时、成功标志及错误信息。
/// </summary>
public class OcrResult
{
    /// <summary>
    /// 纯文本结果
    /// </summary>
    public string Text
    {
        get
        {
            if (OcrContents.Count > 0)
                return string.Join(Environment.NewLine, OcrContents.Select(x => x.Text).ToArray()).Trim();

            return string.Join(
                    Environment.NewLine,
                    Regions
                        .SelectMany(region => region.Paragraphs)
                        .Select(JoinParagraphLines)
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .ToArray())
                .Trim();
        }
    }

    /// <summary>
    /// 识别出的内容列表。
    /// </summary>
    public List<OcrContent> OcrContents { get; set; } = [];

    /// <summary>
    /// 识别出的结构化区域。
    /// </summary>
    public List<OcrRegion> Regions { get; set; } = [];

    /// <summary>
    /// 识别出的语言。
    /// </summary>
    public string Language { get; set; } = string.Empty;
    /// <summary>
    /// 识别耗时。
    /// </summary>
    public TimeSpan Duration { get; set; }
    /// <summary>
    /// 是否识别成功。
    /// </summary>
    public bool IsSuccess { get; set; } = true;
    /// <summary>
    /// 错误信息（如有）。
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 失败
    /// </summary>
    /// <param name="msg"></param>
    /// <returns></returns>
    public OcrResult Fail(string msg)
    {
        IsSuccess = false;
        ErrorMessage = msg;
        return this;
    }

    private static string JoinParagraphLines(OcrParagraph paragraph)
    {
        var lines = paragraph.Lines.Where(line => !string.IsNullOrWhiteSpace(line.Text)).ToList();
        if (lines.Count == 0)
            return string.Empty;

        var text = lines[0].Text;
        for (var i = 1; i < lines.Count; i++)
        {
            if (NeedsSpace(text, lines[i].Text))
                text += " ";

            text += lines[i].Text;
        }

        return text;
    }

    private static bool NeedsSpace(string previous, string current)
    {
        if (string.IsNullOrWhiteSpace(previous) || string.IsNullOrWhiteSpace(current))
            return false;

        var left = previous[^1];
        var right = current[0];
        return !char.IsWhiteSpace(left) &&
               !char.IsWhiteSpace(right) &&
               !char.IsPunctuation(left) &&
               !char.IsPunctuation(right) &&
               !TextHelper.IsCjk(left) &&
               !TextHelper.IsCjk(right);
    }
}

/// <summary>
/// OCR 区域，包含一个区域内的段落信息。
/// </summary>
public class OcrRegion
{
    /// <summary>
    /// 区域内的段落集合。
    /// </summary>
    public List<OcrParagraph> Paragraphs { get; set; } = [];

    /// <summary>
    /// 区域外接坐标框。
    /// </summary>
    public List<BoxPoint> BoxPoints { get; set; } = [];
}

/// <summary>
/// OCR 段落，包含段落内按阅读顺序排列的文本行。
/// </summary>
public class OcrParagraph
{
    /// <summary>
    /// 段落内按阅读顺序排列的文本行。
    /// </summary>
    public List<OcrContent> Lines { get; set; } = [];

    /// <summary>
    /// 段落外接坐标框。
    /// </summary>
    public List<BoxPoint> BoxPoints { get; set; } = [];
}

/// <summary>
/// OCR 内容，包含识别出的文本及其对应的包围盒坐标点。
/// </summary>
public class OcrContent
{
    /// <summary>
    /// 识别出的文本内容。
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 文本对应的包围盒坐标点集合。
    /// </summary>
    public List<BoxPoint> BoxPoints { get; set; } = [];
}

/// <summary>
/// 表示一个二维坐标点，用于描述 OCR 识别内容的包围盒顶点。
/// </summary>
public class BoxPoint(float x, float y)
{
    /// <summary>
    /// X 坐标值。
    /// </summary>
    public float X { get; set; } = x;

    /// <summary>
    /// Y 坐标值。
    /// </summary>
    public float Y { get; set; } = y;
}
