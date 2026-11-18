using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using STranslate.Core;

namespace STranslate.Converters;

/// <summary>
/// 代理类型到启用状态的转换器
/// 当代理类型为 System 或 None 时返回 False，其他情况返回 True
/// </summary>
public class ProxyTypeToEnabledConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ProxyType proxyType)
        {
            return proxyType != ProxyType.System && proxyType != ProxyType.None;
        }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}