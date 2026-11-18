using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace STranslate.Views;

/// <summary>
/// 简单文本输入对话框（新建/重命名用）
/// </summary>
public sealed class InputDialog : Window
{
    private readonly TextBox _textBox;

    private InputDialog(string title, string hint, string defaultValue)
    {
        WindowStyle = WindowStyle.ToolWindow;
        Title = title;
        Width = 360;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
        Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235));

        var panel = new StackPanel { Margin = new Thickness(16) };
        if (!string.IsNullOrEmpty(hint))
        {
            panel.Children.Add(new TextBlock
            {
                Text = hint,
                Margin = new Thickness(0, 0, 0, 6),
                Foreground = new SolidColorBrush(Color.FromRgb(187, 187, 187)),
            });
        }
        _textBox = new TextBox
        {
            Text = defaultValue,
            Background = new SolidColorBrush(Color.FromArgb(51, 0, 0, 0)),
            Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
            CaretBrush = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            Padding = new Thickness(6, 4, 6, 4),
        };
        _textBox.SelectAll();
        _textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                DialogResult = true;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                e.Handled = true;
            }
        };
        panel.Children.Add(_textBox);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };
        var ok = new Button
        {
            Content = "确定",
            Padding = new Thickness(16, 4, 16, 4),
            Margin = new Thickness(6, 0, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
        };
        ok.Click += (_, _) => DialogResult = true;
        var cancel = new Button
        {
            Content = "取消",
            Padding = new Thickness(16, 4, 16, 4),
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
        };
        cancel.Click += (_, _) => DialogResult = false;
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        Content = panel;
        Loaded += (_, _) => _textBox.Focus();
    }

    /// <summary>显示并返回输入（取消返回 null）</summary>
    public static string? Show(string title, string hint, string defaultValue = "", Window? owner = null)
    {
        var dialog = new InputDialog(title, hint, defaultValue);
        if (owner != null)
            dialog.Owner = owner;
        else if (Application.Current?.MainWindow is { IsVisible: true } main)
            dialog.Owner = main;
        return dialog.ShowDialog() == true ? dialog._textBox.Text : null;
    }
}
