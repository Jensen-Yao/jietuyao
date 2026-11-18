using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace STranslate.Controls;

public static class TextBoxBehaviors
{
    public static readonly DependencyProperty SelectAllOnClickProperty =
        DependencyProperty.RegisterAttached(
            "SelectAllOnClick",
            typeof(bool),
            typeof(TextBoxBehaviors),
            new PropertyMetadata(false, OnSelectAllOnClickChanged));

    public static bool GetSelectAllOnClick(DependencyObject obj)
    {
        return (bool)obj.GetValue(SelectAllOnClickProperty);
    }

    public static void SetSelectAllOnClick(DependencyObject obj, bool value)
    {
        obj.SetValue(SelectAllOnClickProperty, value);
    }

    private static void OnSelectAllOnClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox textBox)
        {
            if ((bool)e.NewValue)
            {
                textBox.GotKeyboardFocus += TextBox_GotKeyboardFocus;
            }
            else
            {
                textBox.GotKeyboardFocus -= TextBox_GotKeyboardFocus;
            }
        }
    }

    private static void TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            // 延迟执行，确保在文本框完全获得焦点后再全选
            textBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                textBox.SelectAll();
            }), DispatcherPriority.Input);
        }
    }
}
