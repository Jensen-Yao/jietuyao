using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace STranslate.Plugin;

/// <summary>
///     Databind the Password Property of a WPF PasswordBox
///     Source <see href="https://www.wpftutorial.net/PasswordBox.html" />
///     1. 处理 PasswordBox 无法绑定 Password 属性的问题
///     2. 处理导航时 PasswordBox 内容丢失的问题
/// </summary>
public static class PasswordBoxAssistant
{
    /// <summary>
    /// Password
    /// </summary>
    public static readonly DependencyProperty PasswordProperty = DependencyProperty.RegisterAttached("Password",
        typeof(string), typeof(PasswordBoxAssistant),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnPasswordPropertyChanged,
            null,
            false,
            UpdateSourceTrigger.LostFocus)
        );

    /// <summary>
    /// Attach
    /// </summary>
    public static readonly DependencyProperty AttachProperty = DependencyProperty.RegisterAttached("Attach",
        typeof(bool), typeof(PasswordBoxAssistant), new PropertyMetadata(false, Attach));

    private static readonly DependencyProperty IsUpdatingProperty = DependencyProperty.RegisterAttached("IsUpdating",
        typeof(bool),
        typeof(PasswordBoxAssistant));

    /// <summary>
    /// SetAttach
    /// </summary>
    /// <param name="dp"></param>
    /// <param name="value"></param>
    public static void SetAttach(DependencyObject dp, bool value)
    {
        dp.SetValue(AttachProperty, value);
    }

    /// <summary>
    /// GetAttach
    /// </summary>
    /// <param name="dp"></param>
    /// <returns></returns>
    public static bool GetAttach(DependencyObject dp)
    {
        return (bool)dp.GetValue(AttachProperty);
    }

    /// <summary>
    /// GetPassword
    /// </summary>
    /// <param name="dp"></param>
    /// <returns></returns>
    public static string GetPassword(DependencyObject dp)
    {
        return (string)dp.GetValue(PasswordProperty);
    }

    /// <summary>
    /// SetPassword
    /// </summary>
    /// <param name="dp"></param>
    /// <param name="value"></param>
    public static void SetPassword(DependencyObject dp, string value)
    {
        dp.SetValue(PasswordProperty, value);
    }

    private static bool GetIsUpdating(DependencyObject dp)
    {
        return (bool)dp.GetValue(IsUpdatingProperty);
    }

    private static void SetIsUpdating(DependencyObject dp, bool value)
    {
        dp.SetValue(IsUpdatingProperty, value);
    }

    private static void OnPasswordPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox) return;

        passwordBox.PasswordChanged -= PasswordChanged;

        if (!GetIsUpdating(passwordBox)) passwordBox.Password = (string)e.NewValue;
        passwordBox.PasswordChanged += PasswordChanged;
    }

    private static void Attach(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox) return;

        if ((bool)e.OldValue) passwordBox.PasswordChanged -= PasswordChanged;

        if ((bool)e.NewValue) passwordBox.PasswordChanged += PasswordChanged;
    }

    private static void PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox) return;

        if (_isNavigate)
        {
            _isNavigate = false;
            return;
        }

        if (Application.Current is INavigation navigation && navigation.IsNavigated)
        {
            _isNavigate = true;
            passwordBox.Password = GetPassword(passwordBox);
            return;
        }
        SetIsUpdating(passwordBox, true);
        SetPassword(passwordBox, passwordBox.Password);
        SetIsUpdating(passwordBox, false);
    }

    /// <summary>
    ///     <see href="https://stackoverflow.com/a/31940993/18478256"/>
    ///     <see href="https://stackoverflow.com/questions/10783583/passwordbox-loses-its-content-on-navigation"/>
    ///     <see href="https://www.wpfsharp.com/2011/04/08/wpf-navigationservice-blanks-passwordbox-password-which-breaks-the-mvvm-passwordhelper/"/>
    /// </summary>
    private static bool _isNavigate = false;
}