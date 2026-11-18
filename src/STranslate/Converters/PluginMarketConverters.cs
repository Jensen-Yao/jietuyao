using CommunityToolkit.Mvvm.DependencyInjection;
using STranslate.Core;
using STranslate.ViewModels.Pages;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace STranslate.Converters;

/// <summary>
/// 插件操作状态到文本转换器
/// </summary>
public class PluginActionStatusToStringConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PluginActionStatus status)
        {
            var i18n = Ioc.Default.GetRequiredService<Internationalization>();
            return status switch
            {
                PluginActionStatus.Download => i18n.GetTranslation("PluginMarketDownload"),
                PluginActionStatus.Installed => i18n.GetTranslation("PluginMarketInstalled"),
                PluginActionStatus.Upgrade => i18n.GetTranslation("PluginMarketUpgrade"),
                PluginActionStatus.Downloading => $"{i18n.GetTranslation("PluginMarketDownloading")}...",
                PluginActionStatus.PendingRestart => i18n.GetTranslation("PendingRestart"),
                _ => string.Empty
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

/// <summary>
/// 插件操作状态到启用状态转换器
/// </summary>
public class PluginActionStatusToBoolConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PluginActionStatus status)
        {
            return status is PluginActionStatus.Download or PluginActionStatus.Upgrade;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

/// <summary>
/// 布尔值取反到可见性转换器
/// </summary>
public class BoolInverseToVisibilityConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool boolValue && boolValue ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

/// <summary>
/// 插件操作状态到可见性转换器
/// </summary>
public class PluginActionStatusToVisibilityConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PluginActionStatus status && parameter is string targetStatus)
        {
            return status.ToString() == targetStatus ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
