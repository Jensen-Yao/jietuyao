using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace STranslate.Converters;

public class StringEqualMultiConverter : MarkupExtension, IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;
        var str1 = values[0]?.ToString() ?? string.Empty;
        var str2 = values[1]?.ToString() ?? string.Empty;
        return string.Equals(str1, str2, StringComparison.Ordinal);
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => Array.Empty<object>();
    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
