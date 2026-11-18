using CommunityToolkit.Mvvm.DependencyInjection;
using STranslate.Core;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace STranslate.Converters;

public class LangEnumToStringConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string str ?
        Ioc.Default.GetRequiredService<Internationalization>().GetTranslation($"LangEnum{str}") :
        value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
