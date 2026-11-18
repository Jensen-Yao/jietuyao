using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace STranslate.Controls;

public class HotkeyDisplay : Button
{
    public enum DisplayType
    {
        Default,
        Small
    }

    static HotkeyDisplay()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HotkeyDisplay),
            new FrameworkPropertyMetadata(typeof(HotkeyDisplay)));
    }

    public string Keys
    {
        get => (string)GetValue(KeysProperty);
        set => SetValue(KeysProperty, value);
    }
    public static readonly DependencyProperty KeysProperty =
        DependencyProperty.Register(nameof(Keys), typeof(string), typeof(HotkeyDisplay),
            new PropertyMetadata(string.Empty, KeyChanged));

    private static void KeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Button) return; // This should not be possible

        if (e.NewValue is not string newValue) return;

        if (d is not HotkeyDisplay hotkeyDisplay) return;

        hotkeyDisplay.Values.Clear();
        foreach (var key in newValue.Split('+'))
        {
            hotkeyDisplay.Values.Add(key);
        }
    }

    public DisplayType Type
    {
        get => (DisplayType)GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }
    public static readonly DependencyProperty TypeProperty =
        DependencyProperty.Register(nameof(Type), typeof(DisplayType), typeof(HotkeyDisplay),
            new PropertyMetadata(DisplayType.Default));

    public ObservableCollection<string> Values { get; set; } = [];

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        var itemsControl = GetTemplateChild("PART_ItemsHost") as ItemsControl;
        itemsControl?.ItemsSource = Values;
    }
}