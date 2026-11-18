using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace STranslate.Converters;

public class CountToVisibilityConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int count ? count > 0 ? Visibility.Visible : Visibility.Collapsed : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
