using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;

namespace STranslate.Converters;

public class StringToKeyBindingConverter : MarkupExtension, IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hotkeyStr || parameter is not HotkeyMode mode)
            return null;

        var converter = new KeyGestureConverter();
        var key = converter.ConvertFromString(hotkeyStr) as KeyGesture;
        return mode switch
        {
            HotkeyMode.Key => key?.Key,
            HotkeyMode.Modifiers => key?.Modifiers,
            _ => null
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

public enum HotkeyMode
{
    Key,
    Modifiers
}
