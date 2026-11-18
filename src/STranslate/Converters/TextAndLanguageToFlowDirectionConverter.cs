using STranslate.Helpers;
using STranslate.Plugin;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace STranslate.Converters;

/// <summary>
/// 将文本、显式语种和识别语种转换为 WPF 文本流向。
/// </summary>
public class TextAndLanguageToFlowDirectionConverter : MarkupExtension, IMultiValueConverter
{
    /// <summary>
    /// 根据绑定值解析文本流向。
    /// </summary>
    /// <param name="values">依次包含文本、显式语种和可选识别语种。</param>
    /// <param name="targetType">绑定目标类型。</param>
    /// <param name="parameter">转换参数。</param>
    /// <param name="culture">绑定区域性。</param>
    /// <returns>解析后的 <see cref="FlowDirection"/>。</returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 0)
            return FlowDirection.LeftToRight;

        var text = values[0] as string;
        var language = values.Length > 1 && values[1] is LangEnum lang ? lang : LangEnum.Auto;
        var detectedLanguage = values.Length > 2 && values[2] is LangEnum identified ? identified : LangEnum.Auto;

        return BidiDirectionHelper.GetFlowDirection(text, language, detectedLanguage);
    }

    /// <summary>
    /// 禁止将文本流向反向写回源绑定。
    /// </summary>
    /// <param name="value">绑定目标值。</param>
    /// <param name="targetTypes">各源绑定的目标类型。</param>
    /// <param name="parameter">转换参数。</param>
    /// <param name="culture">绑定区域性。</param>
    /// <returns>与源绑定数量一致的 <see cref="Binding.DoNothing"/> 数组。</returns>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => Array.ConvertAll(targetTypes, _ => Binding.DoNothing);

    /// <summary>
    /// 返回当前无状态转换器实例供 XAML 标记扩展使用。
    /// </summary>
    /// <param name="serviceProvider">XAML 服务提供程序。</param>
    /// <returns>当前转换器实例。</returns>
    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
