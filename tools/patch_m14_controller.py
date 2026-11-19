#!/usr/bin/env python3
"""M14：TranslatedImageController 重构 —— 暴露行集/按目标重渲染/纯文本翻译"""
from pathlib import Path

p = Path("src/STranslate/Core/Screenshot/TranslatedImageController.cs")
t = p.read_text(encoding="utf-8")

# Result 记录扩展：携带行集与目标语
old = """    public sealed record Result(Bitmap? Image, string Info, string Error);"""
new = """    public sealed record Result(Bitmap? Image, string Info, string Error)
    {
        public List<TranslatedLine> Lines { get; init; } = [];
        public LangEnum Target { get; init; } = LangEnum.Auto;
    }"""
assert old in t
t = t.replace(old, new, 1)

# CreateAsync 主体重构：翻译段抽公共方法，返回行集与目标
old = """        var capped = lines.Take(MaxLines).ToList();

        // 目标语启发：原文 ASCII 为主 → 中；否则 → 英
        var target = targetOverride ?? InferTarget(string.Concat(capped.Take(6).Select(l => l.text)));

        // 逐行并发翻译
        var translations = new string[capped.Count];
        using var semaphore = new SemaphoreSlim(Concurrency);
        var tasks = capped.Select(async (line, i) =>
        {
            await semaphore.WaitAsync();
            try
            {
                var result = new TranslateResult();
                await translator.TranslateAsync(new TranslateRequest(line.text, LangEnum.Auto, target), result);
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

        // 渲染
        var translated = capped.Select((l, i) => new TranslatedLine(l.bounds, l.text, translations[i])).ToList();
        var output = ImageTranslateOverlay.Render(cleanImage, translated);
        var info = $"{ocrSvc.DisplayName} → {traSvc.DisplayName} → {Describe(target)} | {capped.Count} 行";
        return new Result(output, info, string.Empty);
    }"""
new = """        var capped = lines.Take(MaxLines).ToList();
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
    }"""
assert old in t
t = t.replace(old, new, 1)
p.write_text(t, encoding="utf-8")
print("controller refactored")
