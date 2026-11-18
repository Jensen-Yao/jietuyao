using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace STranslate.Converters;

/// <summary>
///     <see href="https://www.cnblogs.com/linxmouse/p/18266455"/>
/// </summary>
public class EventArgsConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => new Tuple<object, EventArgs>(parameter, (EventArgs)value);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}