using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace STranslate.Controls;

/// <summary>
/// 确保 ToolBar 在父级布局宽度变化后重新计算溢出项目。
/// </summary>
public static class ToolBarOverflowRefresh
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(ToolBarOverflowRefresh),
        new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty IsRefreshPendingProperty = DependencyProperty.RegisterAttached(
        "IsRefreshPending",
        typeof(bool),
        typeof(ToolBarOverflowRefresh),
        new PropertyMetadata(false));

    private static readonly DependencyProperty RefreshAgainProperty = DependencyProperty.RegisterAttached(
        "RefreshAgain",
        typeof(bool),
        typeof(ToolBarOverflowRefresh),
        new PropertyMetadata(false));

    public static bool GetIsEnabled(DependencyObject element)
        => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value)
        => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ToolBar toolBar)
            return;

        if ((bool)e.NewValue)
        {
            toolBar.Loaded += OnToolBarLoaded;
            toolBar.SizeChanged += OnToolBarSizeChanged;
        }
        else
        {
            toolBar.Loaded -= OnToolBarLoaded;
            toolBar.SizeChanged -= OnToolBarSizeChanged;
        }
    }

    private static void OnToolBarLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ToolBar toolBar)
            QueueRefresh(toolBar);
    }

    private static void OnToolBarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged && sender is ToolBar toolBar)
            QueueRefresh(toolBar);
    }

    private static void QueueRefresh(ToolBar toolBar)
    {
        if ((bool)toolBar.GetValue(IsRefreshPendingProperty))
        {
            toolBar.SetValue(RefreshAgainProperty, true);
            return;
        }

        toolBar.SetValue(IsRefreshPendingProperty, true);
        toolBar.Dispatcher.BeginInvoke(() =>
        {
            if (!toolBar.IsLoaded)
            {
                CompleteRefresh(toolBar);
                return;
            }

            toolBar.ApplyTemplate();
            var root = toolBar.Template.FindName("PART_ToolBarRoot", toolBar) as Grid;
            if (toolBar.Template.FindName("PART_ToolBarPanel", toolBar) is ToolBarPanel panel)
                panel.InvalidateMeasure();

            root?.InvalidateMeasure();
            root?.InvalidateArrange();
            toolBar.InvalidateMeasure();
            var tray = FindVisualAncestor<ToolBarTray>(toolBar);
            tray?.InvalidateMeasure();
            tray?.InvalidateArrange();
            tray?.UpdateLayout();

            toolBar.Dispatcher.BeginInvoke(
                () => CompleteRefresh(toolBar),
                DispatcherPriority.ContextIdle);
        }, DispatcherPriority.Loaded);
    }

    private static void CompleteRefresh(ToolBar toolBar)
    {
        var refreshAgain = (bool)toolBar.GetValue(RefreshAgainProperty);
        toolBar.SetValue(RefreshAgainProperty, false);
        toolBar.SetValue(IsRefreshPendingProperty, false);

        if (refreshAgain && toolBar.IsLoaded)
            QueueRefresh(toolBar);
    }

    private static T? FindVisualAncestor<T>(DependencyObject element)
        where T : DependencyObject
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is T result)
                return result;
        }

        return null;
    }
}
