using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace STranslate.Controls;

public static class ListBoxReorderAnimationBehavior
{
    private sealed class AnimationState
    {
        public Dictionary<FrameworkElement, Point> LastPositions { get; } = [];
        public bool HasSnapshot { get; set; }
    }

    private static readonly DependencyProperty AnimationStateProperty =
        DependencyProperty.RegisterAttached(
            "AnimationState",
            typeof(AnimationState),
            typeof(ListBoxReorderAnimationBehavior),
            new PropertyMetadata(null));

    public static readonly DependencyProperty EnableAnimationProperty =
        DependencyProperty.RegisterAttached(
            "EnableAnimation",
            typeof(bool),
            typeof(ListBoxReorderAnimationBehavior),
            new PropertyMetadata(false, OnEnableAnimationChanged));

    public static readonly DependencyProperty AnimationDurationProperty =
        DependencyProperty.RegisterAttached(
            "AnimationDuration",
            typeof(TimeSpan),
            typeof(ListBoxReorderAnimationBehavior),
            new PropertyMetadata(TimeSpan.FromMilliseconds(180)));

    public static bool GetEnableAnimation(DependencyObject obj)
        => (bool)obj.GetValue(EnableAnimationProperty);

    public static void SetEnableAnimation(DependencyObject obj, bool value)
        => obj.SetValue(EnableAnimationProperty, value);

    public static TimeSpan GetAnimationDuration(DependencyObject obj)
        => (TimeSpan)obj.GetValue(AnimationDurationProperty);

    public static void SetAnimationDuration(DependencyObject obj, TimeSpan value)
        => obj.SetValue(AnimationDurationProperty, value);

    private static AnimationState? GetAnimationState(DependencyObject obj)
        => (AnimationState?)obj.GetValue(AnimationStateProperty);

    private static void SetAnimationState(DependencyObject obj, AnimationState? value)
        => obj.SetValue(AnimationStateProperty, value);

    private static AnimationState GetOrCreateAnimationState(DependencyObject obj)
    {
        var state = GetAnimationState(obj);
        if (state != null)
        {
            return state;
        }

        state = new AnimationState();
        SetAnimationState(obj, state);
        return state;
    }

    private static void OnEnableAnimationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox) return;

        Detach(listBox);

        if ((bool)e.NewValue)
        {
            listBox.Loaded += OnListBoxLoaded;
            listBox.Unloaded += OnListBoxUnloaded;

            if (listBox.IsLoaded)
            {
                Attach(listBox);
            }
        }
        else
        {
            ResetState(listBox);
        }
    }

    private static void OnListBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            Attach(listBox);
        }
    }

    private static void OnListBoxUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            listBox.LayoutUpdated -= OnListBoxLayoutUpdated;
            ResetState(listBox);
        }
    }

    private static void Attach(ListBox listBox)
    {
        listBox.LayoutUpdated -= OnListBoxLayoutUpdated;
        listBox.LayoutUpdated += OnListBoxLayoutUpdated;

        ResetState(listBox);
    }

    private static void Detach(ListBox listBox)
    {
        listBox.Loaded -= OnListBoxLoaded;
        listBox.Unloaded -= OnListBoxUnloaded;
        listBox.LayoutUpdated -= OnListBoxLayoutUpdated;
    }

    private static void ResetState(ListBox listBox)
    {
        var state = GetAnimationState(listBox);
        if (state == null)
        {
            return;
        }

        state.LastPositions.Clear();
        state.HasSnapshot = false;
    }

    private static void OnListBoxLayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is not ListBox listBox || !GetEnableAnimation(listBox))
        {
            return;
        }

        var state = GetOrCreateAnimationState(listBox);
        if (listBox.Items.Count == 0)
        {
            state.LastPositions.Clear();
            state.HasSnapshot = false;
            return;
        }

        var currentPositions = new Dictionary<FrameworkElement, Point>(listBox.Items.Count);
        var duration = new Duration(GetAnimationDuration(listBox));
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        for (var index = 0; index < listBox.Items.Count; index++)
        {
            if (listBox.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement container ||
                !container.IsLoaded)
            {
                continue;
            }

            var slot = LayoutInformation.GetLayoutSlot(container);
            var currentPosition = slot.Location;
            currentPositions[container] = currentPosition;

            if (!state.HasSnapshot || !state.LastPositions.TryGetValue(container, out var previousPosition))
            {
                continue;
            }

            var deltaX = previousPosition.X - currentPosition.X;
            var deltaY = previousPosition.Y - currentPosition.Y;

            if (Math.Abs(deltaX) < 0.5 && Math.Abs(deltaY) < 0.5)
            {
                continue;
            }

            var translateTransform = EnsureTranslateTransform(container);
            translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
            translateTransform.BeginAnimation(TranslateTransform.YProperty, null);

            var animationX = new DoubleAnimation
            {
                From = deltaX,
                To = 0,
                Duration = duration,
                EasingFunction = easing
            };

            var animationY = new DoubleAnimation
            {
                From = deltaY,
                To = 0,
                Duration = duration,
                EasingFunction = easing
            };

            translateTransform.BeginAnimation(TranslateTransform.XProperty, animationX, HandoffBehavior.SnapshotAndReplace);
            translateTransform.BeginAnimation(TranslateTransform.YProperty, animationY, HandoffBehavior.SnapshotAndReplace);
        }

        state.LastPositions.Clear();
        foreach (var (element, position) in currentPositions)
        {
            state.LastPositions[element] = position;
        }

        state.HasSnapshot = true;
    }

    private static TranslateTransform EnsureTranslateTransform(UIElement element)
    {
        if (element.RenderTransform is TranslateTransform translateTransform)
        {
            return translateTransform;
        }

        if (element.RenderTransform is TransformGroup transformGroup)
        {
            foreach (var child in transformGroup.Children)
            {
                if (child is TranslateTransform existingTranslate)
                {
                    return existingTranslate;
                }
            }

            var appendedTranslate = new TranslateTransform();
            transformGroup.Children.Add(appendedTranslate);
            return appendedTranslate;
        }

        if (element.RenderTransform == null || element.RenderTransform == Transform.Identity)
        {
            var newTranslate = new TranslateTransform();
            element.RenderTransform = newTranslate;
            return newTranslate;
        }

        var groupedTransform = new TransformGroup();
        groupedTransform.Children.Add(element.RenderTransform);

        var translate = new TranslateTransform();
        groupedTransform.Children.Add(translate);

        element.RenderTransform = groupedTransform;
        return translate;
    }
}
