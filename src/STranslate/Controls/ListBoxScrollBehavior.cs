using STranslate.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace STranslate.Controls;

public static class ListBoxScrollBehavior
{
    private sealed class ScrollBehaviorState
    {
        public ScrollViewer? ScrollViewer { get; set; }
        public ScrollChangedEventHandler? ScrollChangedHandler { get; set; }
    }

    private static readonly DependencyProperty ScrollBehaviorStateProperty =
        DependencyProperty.RegisterAttached(
            "ScrollBehaviorState",
            typeof(ScrollBehaviorState),
            typeof(ListBoxScrollBehavior),
            new PropertyMetadata(null));

    private static void SetScrollBehaviorState(DependencyObject obj, ScrollBehaviorState? value)
        => obj.SetValue(ScrollBehaviorStateProperty, value);

    private static ScrollBehaviorState? GetScrollBehaviorState(DependencyObject obj)
        => (ScrollBehaviorState?)obj.GetValue(ScrollBehaviorStateProperty);

    public static readonly DependencyProperty ScrollAtBottomCommandProperty =
        DependencyProperty.RegisterAttached(
            "ScrollAtBottomCommand",
            typeof(ICommand),
            typeof(ListBoxScrollBehavior),
            new PropertyMetadata(null, OnScrollAtBottomCommandChanged));

    public static ICommand GetScrollAtBottomCommand(DependencyObject obj)
    {
        return (ICommand)obj.GetValue(ScrollAtBottomCommandProperty);
    }

    public static void SetScrollAtBottomCommand(DependencyObject obj, ICommand value)
    {
        obj.SetValue(ScrollAtBottomCommandProperty, value);
    }

    private static void OnScrollAtBottomCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox) return;

        // 属性变化前先确保移除旧订阅，避免重复注册
        DetachScrollViewer(listBox);
        listBox.Loaded -= ListBox_Loaded;
        listBox.Unloaded -= ListBox_Unloaded;

        if (e.NewValue is not null)
        {
            listBox.Loaded += ListBox_Loaded;
            listBox.Unloaded += ListBox_Unloaded;

            // 控件已加载时立即挂接，避免依赖下一次 Loaded
            if (listBox.IsLoaded)
            {
                AttachScrollViewer(listBox);
            }
        }
    }

    private static void ListBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            AttachScrollViewer(listBox);
        }
    }

    private static void ListBox_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            DetachScrollViewer(listBox);
        }
    }

    private static void AttachScrollViewer(ListBox listBox)
    {
        var scrollViewer = Utilities.GetVisualChild<ScrollViewer>(listBox);
        if (scrollViewer == null) return;

        var state = GetScrollBehaviorState(listBox) ?? new ScrollBehaviorState();
        SetScrollBehaviorState(listBox, state);

        // 若模板切换导致 ScrollViewer 变化，先解绑旧实例
        if (state.ScrollViewer != null && state.ScrollChangedHandler != null)
        {
            state.ScrollViewer.ScrollChanged -= state.ScrollChangedHandler;
        }

        ScrollChangedEventHandler handler = (_, args) => OnScrollChanged(listBox, scrollViewer, args);
        state.ScrollViewer = scrollViewer;
        state.ScrollChangedHandler = handler;
        scrollViewer.ScrollChanged += handler;
    }

    private static void DetachScrollViewer(ListBox listBox)
    {
        var state = GetScrollBehaviorState(listBox);
        if (state == null) return;

        if (state.ScrollViewer != null && state.ScrollChangedHandler != null)
        {
            state.ScrollViewer.ScrollChanged -= state.ScrollChangedHandler;
        }

        state.ScrollViewer = null;
        state.ScrollChangedHandler = null;
    }

    private static void OnScrollChanged(ListBox listBox, ScrollViewer scrollViewer, ScrollChangedEventArgs args)
    {
        // 增加容差，避免浮点误差导致边界触发不稳定
        var isAtBottom = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 1.0 &&
                         scrollViewer.ScrollableHeight > 0;

        if (!isAtBottom) return;

        var command = GetScrollAtBottomCommand(listBox);
        if (command != null && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }
}
