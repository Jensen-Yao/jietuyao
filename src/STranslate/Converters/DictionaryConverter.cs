using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using STranslate.Plugin;

namespace STranslate.Converters;

public class DictionaryResultTypeVisibilityConverter : EnumToVisibilityConverter<DictionaryResultType>
{
}

public class DictionaryResultSuccessErrorBoolConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is DictionaryResultType dictionaryResultType &&
        (dictionaryResultType == DictionaryResultType.Success || dictionaryResultType == DictionaryResultType.Error);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

public class DictionaryResultNoResultInverseBoolConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is DictionaryResultType dictionaryResultType && dictionaryResultType != DictionaryResultType.NoResult;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

public class DictionaryWordFormsToVisibilityConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DictionaryResult dictionaryResult)
            return Visibility.Collapsed;

        // 检查是否有任何词汇变形数据
        return HasAnyWordForms(dictionaryResult) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    /// <summary>
    /// 检查字典结果是否包含任何词汇变形信息
    /// </summary>
    /// <param name="result">字典结果对象</param>
    /// <returns>如果包含任何词汇变形信息则返回 true</returns>
    private static bool HasAnyWordForms(DictionaryResult result)
    {
        return HasItems(result.Plurals) ||
               HasItems(result.PastTense) ||
               HasItems(result.PastParticiple) ||
               HasItems(result.PresentParticiple) ||
               HasItems(result.ThirdPersonSingular) ||
               HasItems(result.Comparative) ||
               HasItems(result.Superlative) ||
               HasItems(result.Tags);
    }

    /// <summary>
    /// 检查集合是否有有效项目
    /// </summary>
    /// <param name="collection">要检查的集合</param>
    /// <returns>如果集合不为 null 且包含项目则返回 true</returns>
    private static bool HasItems(ObservableCollection<string>? collection)
    {
        return collection is { Count: > 0 };
    }
}