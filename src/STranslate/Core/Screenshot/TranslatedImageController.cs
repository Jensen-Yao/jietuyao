using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Logging;
using STranslate.Plugin;
using STranslate.Services;

namespace STranslate.Core;

/// <summary>
/// 译图编排（移植自 SnipTrans TranslationWorkflowService）：
/// 行级 OCR（BoxPoints）→ 逐行并发翻译（3 并发，≤24 行）→ 译文回填原图。
/// </summary>
public static class TranslatedImageController
{
    private const int MaxLines = 24;
    private const int Concurrency = 3;

    public sealed record Result(Bitmap? Image, string Info, string Error)
    {
        public List<TranslatedLine> Lines { get; init; } = [];
        public LangEnum Target { get; init; } = LangEnum.Auto;
    }

    public static Task<Result> CreateAsync(Bitmap cleanImage) =>
        CreateAsync(cleanImage, ParseTarget(Ioc.Default.GetService<Settings>()?.ImageOverlayTargetLang));

    public static async Task<Result> CreateAsync(Bitmap cleanImage, LangEnum? targetOverride)
    {
        // OCR：优先支持坐标的已启用服务
        var ocrService = Ioc.Default.GetService<OcrService>();
        var ocrSvc = ocrService?.Services.FirstOrDefault(s => s.IsEnabled && (s.Plugin as IOcrPlugin)?.SupportBoxPoints() == true)
                     ?? ocrService?.Services.FirstOrDefault(s => s.IsEnabled);
        if (ocrSvc?.Plugin is not IOcrPlugin ocr)
            return new Result(null, string.Empty, "未启用 OCR 服务：设置 → OCR 启用一个（推荐 Windows OCR / PP-OCR）");

        // 翻译：第一个已启用服务
        var translateService = Ioc.Default.GetService<TranslateService>();
        var traSvc = translateService?.Services.FirstOrDefault(s => s.IsEnabled);
        if (traSvc?.Plugin is not ITranslatePlugin translator)
            return new Result(null, string.Empty, "未启用翻译服务：设置 → 翻译 启用一个");

        // OCR
        OcrResult ocrResult;
        await using (var stream = new MemoryStream())
        {
            cleanImage.Save(stream, ImageFormat.Png);
            ocrResult = await ocr.RecognizeAsync(new OcrRequest(stream.ToArray(), LangEnum.Auto, cleanImage.Width, cleanImage.Height));
        }
        if (!ocrResult.IsSuccess)
            return new Result(null, string.Empty, $"OCR 失败：{ocrResult.ErrorMessage}");
        var lines = CollectLines(ocrResult);
        if (lines.Count == 0)
            return new Result(null, string.Empty, "未识别到文本行");

        var capped = lines.Take(MaxLines).ToList();
        var target = targetOverride ?? InferTarget(string.Concat(capped.Take(6).Select(l => l.text)));
        var translations = await TranslateLinesAsync(translator, capped.Select(l => l.text).ToList(), target);
        var translated = capped.Select((l, i) => new TranslatedLine(l.bounds, l.text, translations[i])).ToList();
        var output = ImageTranslateOverlay.Render(cleanImage, translated);
        var info = $"{ocrSvc.DisplayName} → {traSvc.DisplayName} → {Describe(target)} | {capped.Count} 行";
        return new Result(output, info, string.Empty) { Lines = translated, Target = target };
    }

    /// <summary>工作台"重新生成"：用已有 OCR 行集按新目标语重翻译并重渲染（不重复识别）</summary>
    public static async Task<Result> RenderWithTargetAsync(
        System.Drawing.Bitmap original, IReadOnlyList<TranslatedLine> lines, LangEnum target)
    {
        var translateService = Ioc.Default.GetService<TranslateService>();
        var traSvc = translateService?.Services.FirstOrDefault(s => s.IsEnabled);
        if (traSvc?.Plugin is not ITranslatePlugin translator)
            return new Result(null, string.Empty, "未启用翻译服务");

        var capped = lines.Take(MaxLines).ToList();
        var translations = await TranslateLinesAsync(translator, capped.Select(l => l.Original).ToList(), target);
        var translated = capped.Select((l, i) => new TranslatedLine(l.Bounds, l.Original, translations[i])).ToList();
        var output = ImageTranslateOverlay.Render(original, translated);
        var info = $"{traSvc.DisplayName} → {Describe(target)} | {capped.Count} 行";
        return new Result(output, info, string.Empty) { Lines = translated, Target = target };
    }

    /// <summary>纯文本翻译（工作台"翻译文本框内容"，吸收自 SnipTrans）</summary>
    public static async Task<string> TranslateTextAsync(string text, LangEnum target)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        var translateService = Ioc.Default.GetService<TranslateService>();
        if (translateService?.Services.FirstOrDefault(s => s.IsEnabled)?.Plugin is not ITranslatePlugin translator)
            return string.Empty;
        var result = new TranslateResult();
        await translator.TranslateAsync(new TranslateRequest(text, LangEnum.Auto, target), result);
        return result.IsSuccess ? result.Text : string.Empty;
    }

    private static async Task<string[]> TranslateLinesAsync(ITranslatePlugin translator, IReadOnlyList<string> sources, LangEnum target)
    {
        var translations = new string[sources.Count];
        using var semaphore = new SemaphoreSlim(Concurrency);
        var tasks = sources.Select(async (text, i) =>
        {
            await semaphore.WaitAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    translations[i] = string.Empty;
                    return;
                }
                var result = new TranslateResult();
                await translator.TranslateAsync(new TranslateRequest(text, LangEnum.Auto, target), result);
                translations[i] = result.IsSuccess ? result.Text : string.Empty;
            }
            catch
            {
                translations[i] = string.Empty;
            }
            finally
            {
                semaphore.Release();
            }
        });
        await Task.WhenAll(tasks);
        return translations;
    }

    private static List<(RectangleF bounds, string text)> CollectLines(OcrResult result)
    {
        var lines = new List<(RectangleF, string)>();
        if (result.OcrContents.Count > 0)
        {
            foreach (var content in result.OcrContents)
            {
                var rect = ToBounds(content.BoxPoints);
                if (rect.HasValue && !string.IsNullOrWhiteSpace(content.Text))
                    lines.Add((rect.Value, content.Text));
            }
            return lines;
        }
        foreach (var region in result.Regions)
        foreach (var paragraph in region.Paragraphs)
        foreach (var content in paragraph.Lines)
        {
            var rect = ToBounds(content.BoxPoints);
            if (rect.HasValue && !string.IsNullOrWhiteSpace(content.Text))
                lines.Add((rect.Value, content.Text));
        }
        return lines;
    }

    private static RectangleF? ToBounds(List<BoxPoint> points)
    {
        if (points is not { Count: > 0 })
            return null;
        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxX = points.Max(p => p.X);
        var maxY = points.Max(p => p.Y);
        var rect = RectangleF.FromLTRB(minX, minY, maxX, maxY);
        return rect.Width >= 4 && rect.Height >= 4 ? rect : null;
    }

    /// <summary>设置字符串 → 目标语（空/未知 = null = 自动推断）</summary>
    public static LangEnum? ParseTarget(string? tag) => (tag ?? "").Trim().ToLowerInvariant() switch
    {
        "" or "auto" => null,
        "zh" or "zh-hans" or "zh-cn" => LangEnum.ChineseSimplified,
        "zh-hant" or "zh-tw" => LangEnum.ChineseTraditional,
        "en" => LangEnum.English,
        "ja" or "jp" => LangEnum.Japanese,
        "ko" or "kr" => LangEnum.Korean,
        "fr" => LangEnum.French,
        "de" => LangEnum.German,
        "es" => LangEnum.Spanish,
        "ru" => LangEnum.Russian,
        _ => null,
    };

    private static LangEnum InferTarget(string sample)
    {
        var letters = sample.Count(char.IsLetter);
        var ascii = sample.Count(c => c < 128);
        // 拉丁字母占优 → 翻中文；否则（CJK 等）→ 翻英文
        return letters > 0 && ascii * 2 > sample.Length ? LangEnum.ChineseSimplified : LangEnum.English;
    }

    private static string Describe(LangEnum lang) => lang switch
    {
        LangEnum.ChineseSimplified => "中文(简)",
        LangEnum.ChineseTraditional => "中文(繁)",
        LangEnum.English => "英语",
        LangEnum.Japanese => "日语",
        LangEnum.Korean => "韩语",
        _ => lang.ToString(),
    };
}
