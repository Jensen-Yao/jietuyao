using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace STranslate.Controls;

public class WordExchange : ItemsControl
{
    static WordExchange()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(WordExchange),
            new FrameworkPropertyMetadata(typeof(WordExchange)));
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(WordExchange),
            new PropertyMetadata(string.Empty));

    public ICommand? ExecuteCommand
    {
        get => (ICommand?)GetValue(ExecuteCommandProperty);
        set => SetValue(ExecuteCommandProperty, value);
    }

    public static readonly DependencyProperty ExecuteCommandProperty =
        DependencyProperty.Register(
            nameof(ExecuteCommand),
            typeof(ICommand),
            typeof(WordExchange));

}