using ObservableCollections;
using System.Runtime.CompilerServices;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace STranslate.Controls;

public static class ListBoxSelectedItemsBehavior
{
    private static readonly ConditionalWeakTable<ListBox, BehaviorState> _states = new();

    public static readonly DependencyProperty SelectedItemsProperty =
        DependencyProperty.RegisterAttached(
            "SelectedItems",
            typeof(ObservableList<object>),
            typeof(ListBoxSelectedItemsBehavior),
            new PropertyMetadata(null, OnSelectedItemsChanged));

    public static ObservableList<object>? GetSelectedItems(DependencyObject obj)
    {
        return (ObservableList<object>?)obj.GetValue(SelectedItemsProperty);
    }

    public static void SetSelectedItems(DependencyObject obj, ObservableList<object>? value)
    {
        obj.SetValue(SelectedItemsProperty, value);
    }

    private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox) return;

        if (_states.TryGetValue(listBox, out var oldState))
        {
            RemoveState(listBox, oldState);
        }

        if (e.NewValue is ObservableList<object> newList)
        {
            var state = new BehaviorState
            {
                ListBox = listBox,
                ViewModelList = newList,
                IsUpdating = false
            };

            state.Handler = (in NotifyCollectionChangedEventArgs<object> args) =>
                OnViewModelCollectionChanged(state, args);
            state.SelectionHandler = (s, args) =>
                OnSelectionChanged(state, args);
            state.LoadedHandler = (_, _) => AttachState(state);
            state.UnloadedHandler = (_, _) => DetachState(state);

            _states.Add(listBox, state);
            listBox.Loaded += state.LoadedHandler;
            listBox.Unloaded += state.UnloadedHandler;

            AttachState(state);
        }
    }

    private static void AttachState(BehaviorState state)
    {
        if (state.IsAttached)
            return;

        state.ViewModelList.CollectionChanged += state.Handler;
        state.ListBox.SelectionChanged += state.SelectionHandler;
        state.IsAttached = true;

        // 初始同步
        SyncFromViewModel(state);
    }

    private static void DetachState(BehaviorState state)
    {
        if (!state.IsAttached)
            return;

        state.ViewModelList.CollectionChanged -= state.Handler;
        state.ListBox.SelectionChanged -= state.SelectionHandler;
        state.IsAttached = false;
    }

    private static void RemoveState(ListBox listBox, BehaviorState state)
    {
        DetachState(state);
        listBox.Loaded -= state.LoadedHandler;
        listBox.Unloaded -= state.UnloadedHandler;
        _states.Remove(listBox);
    }

    private static void OnSelectionChanged(BehaviorState state, SelectionChangedEventArgs e)
    {
        if (state.IsUpdating) return;

        state.IsUpdating = true;

        foreach (var item in e.RemovedItems)
        {
            state.ViewModelList.Remove(item);
        }

        foreach (var item in e.AddedItems)
        {
            if (!state.ViewModelList.Contains(item))
            {
                state.ViewModelList.Add(item);
            }
        }

        state.IsUpdating = false;
    }

    private static void OnViewModelCollectionChanged(BehaviorState state, NotifyCollectionChangedEventArgs<object> e)
    {
        if (state.IsUpdating) return;

        state.IsUpdating = true;

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var item in e.NewItems)
                {
                    if (!state.ListBox.SelectedItems.Contains(item))
                    {
                        state.ListBox.SelectedItems.Add(item);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                foreach (var item in e.OldItems)
                {
                    state.ListBox.SelectedItems.Remove(item);
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                state.ListBox.SelectedItems.Clear();
                break;
        }

        state.IsUpdating = false;
    }

    private static void SyncFromViewModel(BehaviorState state)
    {
        state.IsUpdating = true;

        state.ListBox.SelectedItems.Clear();

        foreach (var item in state.ViewModelList)
        {
            state.ListBox.SelectedItems.Add(item);
        }

        state.IsUpdating = false;
    }

    private class BehaviorState
    {
        public required ListBox ListBox { get; set; }
        public required ObservableList<object> ViewModelList { get; set; }
        public bool IsUpdating { get; set; }
        public bool IsAttached { get; set; }
        public NotifyCollectionChangedEventHandler<object>? Handler { get; set; }
        public SelectionChangedEventHandler? SelectionHandler { get; set; }
        public RoutedEventHandler? LoadedHandler { get; set; }
        public RoutedEventHandler? UnloadedHandler { get; set; }
    }
}
