using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace STranslate.Converters;

/// <summary>
/// 排序方向到图标的转换器
/// </summary>
[Obsolete("未使用")]
public class SortDirectionToIconConverter : MarkupExtension, IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 3)
            return values[1]; // 默认返回升序图标

        if (values[0] is not ListSortDirection sortDirection)
            return values[1]; // 默认返回升序图标

        // values[1] = 升序图标
        // values[2] = 降序图标
        return sortDirection == ListSortDirection.Ascending ? values[1] : values[2];
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => [.. targetTypes.Select(t => Binding.DoNothing)];

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}