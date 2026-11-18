using iNKORE.UI.WPF.Modern;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace STranslate.Converters;

internal sealed class ThemeToIndexConverter : MarkupExtension, IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            ApplicationTheme.Dark => 1,
            ApplicationTheme.Light => 2,
            _ => 0 // Default to Light theme
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            1 => ApplicationTheme.Dark,
            2 => ApplicationTheme.Light,
            _ => ApplicationTheme.Light
        };

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}