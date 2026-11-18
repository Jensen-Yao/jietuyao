using STranslate.Core;
using STranslate.Plugin;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace STranslate.Converters;

public class LanguageDetectorTypeVisibilityConverter : EnumToVisibilityConverter<LanguageDetectorType> { }
public class BackupTypeVisibilityConverter : EnumToVisibilityConverter<BackupType> { }
public class ExecutionModeVisibilityConverter : EnumToVisibilityConverter<ExecutionMode> { }
public class OcrResultShowingTypeVisibilityConverter : EnumToVisibilityConverter<OcrResultShowingType> { }

/// <summary>
/// 将识别语言状态转换为可见性，供控件模板根据来源状态展示提示元素。
/// </summary>
public class IdentifiedLanguageStateKindVisibilityConverter : EnumToVisibilityConverter<IdentifiedLanguageStateKind> { }

public class EnumToVisibilityConverter<T> : MarkupExtension, IValueConverter where T : struct, Enum
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not T currentValue || parameter is not string targetValueStr)
            return Visibility.Collapsed;

        if (!Enum.TryParse<T>(targetValueStr, out var targetValue))
            return Visibility.Collapsed;

        return currentValue.Equals(targetValue) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
