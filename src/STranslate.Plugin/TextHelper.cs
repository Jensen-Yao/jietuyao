namespace STranslate.Plugin;

/// <summary>
/// 文本处理辅助工具。
/// </summary>
public static class TextHelper
{
    /// <summary>
    /// 判断字符是否属于 CJK（中日韩）文字区域，包含 CJK 统一表意、扩展A、兼容、日文假名、韩文谚文。
    /// </summary>
    public static bool IsCjk(char ch) =>
        (ch >= '\u3400' && ch <= '\u9fff') ||
        (ch >= '\uf900' && ch <= '\ufaff') ||
        (ch >= '\u3040' && ch <= '\u30ff') ||
        (ch >= '\uac00' && ch <= '\ud7af');
}
