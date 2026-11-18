using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace STranslate.Converters;

public class PrePluginBoolMultiConverter : MarkupExtension, IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;
        if (values[0] is not bool isPrePlugin || values[1] is not bool selected)
            return false;

        return !isPrePlugin && selected;
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => [];
    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
