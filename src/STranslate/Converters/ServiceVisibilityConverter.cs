using STranslate.Plugin;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;

namespace STranslate.Converters;

public class ServiceVisibilityConverter : MarkupExtension, IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3 || values.Any(v => v == DependencyProperty.UnsetValue))
            return Visibility.Collapsed;

        var isEnabled = (bool)values[0];
        var execMode = (ExecutionMode)values[1];
        var isTemporaryDisplay = (bool)values[2];

        // 当 IsEnabled 为 true 且 ExecutionMode 不为 Pinned 时可见
        if (isEnabled && (isTemporaryDisplay || execMode != ExecutionMode.Pinned))
        {
            return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => Array.Empty<object>();

    public override object ProvideValue(IServiceProvider serviceProvider)
        => this;
}

/// <summary>
/// 判断「设为图片翻译 OCR 服务」菜单项是否可见。
/// values[0] 为 ActiveImTranOcrCommand(仅在 OCR 页绑定,非空表示当前面板支持该操作),
/// values[1] 为目标 Service。仅当命令非空且服务插件实现 IOcrPlugin 并 SupportBoxPoints() 时返回 Visible,
/// 与拖拽处理 ImTranOcrServiceDropHandler 的判定保持一致。
/// </summary>
public class ImTranOcrEligibilityConverter : MarkupExtension, IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values.Any(v => v == DependencyProperty.UnsetValue))
            return Visibility.Collapsed;

        // 命令未绑定(非 OCR 页)或服务不符合图片翻译 OCR 能力时隐藏
        if (values[0] is not ICommand)
            return Visibility.Collapsed;

        if (values[1] is not Service { Plugin: IOcrPlugin plugin } || !plugin.SupportBoxPoints())
            return Visibility.Collapsed;

        return Visibility.Visible;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => Array.Empty<object>();

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

public class ServicePinnedVisibilityConverter : MarkupExtension, IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3 || values.Any(v => v == DependencyProperty.UnsetValue))
            return Visibility.Collapsed;

        var isEnabled = (bool)values[0];
        var execMode = (ExecutionMode)values[1];
        var isTemporaryDisplay = (bool)values[2];

        // 当 IsEnabled 为 true 且 ExecutionMode 为 Pinned 时可见
        if (isEnabled && execMode == ExecutionMode.Pinned && !isTemporaryDisplay)
        {
            return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => Array.Empty<object>();

    public override object ProvideValue(IServiceProvider serviceProvider)
        => this;
}
