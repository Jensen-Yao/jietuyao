using CommunityToolkit.Mvvm.DependencyInjection;
using STranslate.Core;
using STranslate.Plugin;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace STranslate.Converters;

/// <summary>
/// 历史记录语言显示转换：当原始语言为自动识别时优先展示有效语言。
/// </summary>
public class HistoryLanguageDisplayConverter : MarkupExtension, IMultiValueConverter
{
    /// <summary>
    /// 将原始语言和有效语言转换为历史页面展示文案。
    /// </summary>
    /// <param name="values">索引0为原始语言，索引1为有效语言。</param>
    /// <param name="targetType">目标类型。</param>
    /// <param name="parameter">附加参数。</param>
    /// <param name="culture">区域信息。</param>
    /// <returns>用于展示的语言文本。</returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var rawLanguage = values.Length > 0 ? values[0] as string : null;
        var effectiveLanguage = values.Length > 1 ? values[1] as string : null;

        if (string.IsNullOrWhiteSpace(rawLanguage))
            return string.Empty;

        var autoText = TranslateLanguage(nameof(LangEnum.Auto));
        if (!rawLanguage.Equals(nameof(LangEnum.Auto), StringComparison.OrdinalIgnoreCase))
            return TranslateLanguage(rawLanguage);

        if (!Enum.TryParse<LangEnum>(effectiveLanguage, ignoreCase: true, out var effectiveLangEnum) ||
            effectiveLangEnum == LangEnum.Auto)
            return autoText;

        var effectiveText = TranslateLanguage(effectiveLangEnum.ToString());
        return $"{autoText}({effectiveText})";
    }

    /// <summary>
    /// 反向转换不支持。
    /// </summary>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();

    /// <summary>
    /// 返回当前转换器实例，供 XAML 复用。
    /// </summary>
    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    private static string TranslateLanguage(string languageCode)
    {
        var key = $"LangEnum{languageCode}";
        var translated = Ioc.Default.GetRequiredService<Internationalization>().GetTranslation(key);
        return string.IsNullOrWhiteSpace(translated) || string.Equals(translated, key, StringComparison.Ordinal)
            ? languageCode
            : translated;
    }
}
