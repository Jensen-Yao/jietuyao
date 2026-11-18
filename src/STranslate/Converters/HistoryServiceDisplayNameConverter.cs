using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace STranslate.Converters;

/// <summary>
/// 将历史中的服务快照名称转换为可展示文本；若快照缺失则使用 ServiceID 构造兜底文案。
/// </summary>
public class HistoryServiceDisplayNameConverter : MarkupExtension, IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var serviceDisplayName = values.Length > 0 ? values[0] as string : null;
        if (!string.IsNullOrWhiteSpace(serviceDisplayName))
            return serviceDisplayName;

        return "Unknown";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
