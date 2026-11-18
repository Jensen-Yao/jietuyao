namespace STranslate.Plugin;

/// <summary>
/// 国际化键值对
/// </summary>
/// <param name="code"></param>
/// <param name="display"></param>
public class I18nPair(string code, string display)
{
    /// <summary>
    /// E.g. en or zh-cn
    /// </summary>
    public string LanguageCode { get; set; } = code;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string Display { get; set; } = display;
}
