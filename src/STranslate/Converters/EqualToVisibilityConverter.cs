using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace STranslate.Converters;

public abstract class BaseEqualToVisibilityConverter : MarkupExtension, IValueConverter
{
    protected abstract bool Invert { get; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool equals = string.Equals(value?.ToString() ?? "", parameter?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);

        if (Invert)
            equals = !equals;

        return equals ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

public class EqualToVisibilityConverter : BaseEqualToVisibilityConverter
{
    protected override bool Invert => false;
}

public class EqualToVisibilityInverseConverter : BaseEqualToVisibilityConverter
{
    protected override bool Invert => true;
}
