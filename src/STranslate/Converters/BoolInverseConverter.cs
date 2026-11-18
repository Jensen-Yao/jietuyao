using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace STranslate.Converters;

public class BoolInverseConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not bool boolValue || !boolValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not bool boolValue || !boolValue;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}