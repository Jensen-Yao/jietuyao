using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace STranslate.Converters;

public class SaveToVocabularyMultiConverter : MarkupExtension, IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2)
        {
            return Visibility.Collapsed;
        }
        if (values[0] is not bool hasActivedVocabulary || values[1] is not string text)
        {
            return Visibility.Collapsed;
        }
        return (hasActivedVocabulary && !string.IsNullOrWhiteSpace(text)) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
