using System.Globalization;
using System.Text;
using System.Windows;
using STranslate.Plugin;

namespace STranslate.Helpers;

/// <summary>
/// 根据显式语种、识别语种或文本中的首个强方向字符确定文本流向。
/// </summary>
public static class BidiDirectionHelper
{
    /// <summary>
    /// 按显式语种、识别语种、文本内容的优先级解析文本流向。
    /// </summary>
    /// <param name="text">用于兜底检测方向的文本。</param>
    /// <param name="language">用户显式选择的语种；<see cref="LangEnum.Auto"/> 表示未知。</param>
    /// <param name="detectedLanguage">自动识别出的语种；<see cref="LangEnum.Auto"/> 表示未知。</param>
    /// <returns>适用于 WPF 控件的文本流向。</returns>
    public static FlowDirection GetFlowDirection(
        string? text,
        LangEnum? language = null,
        LangEnum? detectedLanguage = null)
    {
        if (TryGetLanguageDirection(language, out var direction))
            return direction;

        if (TryGetLanguageDirection(detectedLanguage, out direction))
            return direction;

        return GetDirectionFromText(text);
    }

    private static bool TryGetLanguageDirection(
        LangEnum? language,
        out FlowDirection direction)
    {
        direction = FlowDirection.LeftToRight;

        if (language is null or LangEnum.Auto)
            return false;

        direction = IsRightToLeftLanguage(language.Value)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

        return true;
    }

    private static FlowDirection GetDirectionFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return FlowDirection.LeftToRight;

        foreach (var rune in text.EnumerateRunes())
        {
            if (IsRightToLeftRune(rune))
                return FlowDirection.RightToLeft;

            if (IsLeftToRightRune(rune))
                return FlowDirection.LeftToRight;
        }

        return FlowDirection.LeftToRight;
    }

    private static bool IsRightToLeftLanguage(LangEnum language)
        => language is LangEnum.Arabic or LangEnum.Persian or LangEnum.Uyghur;

    private static bool IsRightToLeftRune(Rune rune)
    {
        var value = rune.Value;

        var isRightToLeftBlock =
            value is >= 0x0590 and <= 0x05FF ||
            value is >= 0x0600 and <= 0x06FF ||
            value is >= 0x0750 and <= 0x077F ||
            value is >= 0x0870 and <= 0x089F ||
            value is >= 0x08A0 and <= 0x08FF ||
            value is >= 0xFB1D and <= 0xFB4F ||
            value is >= 0xFB50 and <= 0xFDFF ||
            value is >= 0xFE70 and <= 0xFEFF ||
            value is >= 0x10EC0 and <= 0x10EFF ||
            value is >= 0x1EE00 and <= 0x1EEFF;

        // 区段还包含数字、标点与组合符；它们不是首个强方向字符，必须继续向后查找。
        return isRightToLeftBlock && IsLetter(rune);
    }

    private static bool IsLeftToRightRune(Rune rune) => IsLetter(rune);

    private static bool IsLetter(Rune rune)
        => Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter;
}
