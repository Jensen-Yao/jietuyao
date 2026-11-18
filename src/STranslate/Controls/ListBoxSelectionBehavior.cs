using System.Windows;
using System.Windows.Controls;
using STranslate.Plugin;

namespace STranslate.Controls;

public static class ListBoxSelectionBehavior
{
    public static readonly DependencyProperty AutoDeselectPrePluginsProperty =
        DependencyProperty.RegisterAttached(
            "AutoDeselectPrePlugins",
            typeof(bool),
            typeof(ListBoxSelectionBehavior),
            new PropertyMetadata(false, OnAutoDeselectPrePluginsChanged));

    public static bool GetAutoDeselectPrePlugins(DependencyObject obj)
        => (bool)obj.GetValue(AutoDeselectPrePluginsProperty);

    public static void SetAutoDeselectPrePlugins(DependencyObject obj, bool value)
        => obj.SetValue(AutoDeselectPrePluginsProperty, value);

    private static void OnAutoDeselectPrePluginsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox) return;

        listBox.Loaded -= OnListBoxLoaded;
        listBox.SelectionChanged -= OnSelectionChanged;

        if ((bool)e.NewValue)
        {
            listBox.Loaded += OnListBoxLoaded;
            listBox.SelectionChanged += OnSelectionChanged;
        }
    }

    private static void OnListBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            // 清除初始选中的预装插件
            RemovePrePluginsFromSelection(listBox);
        }
    }

    private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox) return;

        // 从添加的项中移除预装插件
        var prePluginsToRemove = e.AddedItems.Cast<PluginMetaData>()
            .Where(p => p.IsPrePlugin)
            .ToList();

        if (listBox.SelectionMode == SelectionMode.Single)
            listBox.SelectedItem = null;
        else if (listBox.SelectionMode == SelectionMode.Multiple)
            foreach (var plugin in prePluginsToRemove)
                listBox.SelectedItems.Remove(plugin);
    }

    private static void RemovePrePluginsFromSelection(ListBox listBox)
    {
        var itemsToRemove = listBox.SelectedItems.Cast<PluginMetaData>()
            .Where(p => p.IsPrePlugin)
            .ToList();

        if (listBox.SelectionMode == SelectionMode.Single)
            listBox.SelectedItem = null;
        else if (listBox.SelectionMode == SelectionMode.Multiple)
            foreach (var plugin in itemsToRemove)
                listBox.SelectedItems.Remove(plugin);
    }
}