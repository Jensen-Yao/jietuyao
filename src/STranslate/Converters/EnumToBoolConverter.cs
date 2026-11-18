using STranslate.Core;
using STranslate.Plugin;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace STranslate.Converters;

public class BackupTypeBoolConverter: EnumToBoolConverter<BackupType> { }

public class ExecutionModeBoolConverter : EnumToBoolConverter<ExecutionMode>
{
    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is string str && Enum.TryParse<ExecutionMode>(str, out var @enum))
        {
            // 返回解析后的枚举值，这将更新绑定的源属性
            return @enum;
        }
        return base.ConvertBack(value, targetType, parameter, culture);
    }
}

public class EnumToBoolConverter<T> : MarkupExtension, IValueConverter where T : struct, Enum
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not T currentValue || parameter is not string targetValueStr)
            return false;

        if (!Enum.TryParse<T>(targetValueStr, out var targetValue))
            return false;

        return currentValue.Equals(targetValue);
    }

    public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
