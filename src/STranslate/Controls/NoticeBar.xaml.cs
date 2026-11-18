using STranslate.Plugin;
using System.Windows;
using System.Windows.Controls;

namespace STranslate.Controls;

public partial class NoticeBar : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(NoticeBar),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message),
        typeof(string),
        typeof(NoticeBar),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SeverityProperty = DependencyProperty.Register(
        nameof(Severity),
        typeof(Severity),
        typeof(NoticeBar),
        new PropertyMetadata(Severity.Informational));

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen),
        typeof(bool),
        typeof(NoticeBar),
        new PropertyMetadata(false, OnIsOpenChanged));

    public static readonly DependencyProperty IsClosableProperty = DependencyProperty.Register(
        nameof(IsClosable),
        typeof(bool),
        typeof(NoticeBar),
        new PropertyMetadata(false));

    public static readonly DependencyProperty ActionTextProperty = DependencyProperty.Register(
        nameof(ActionText),
        typeof(string),
        typeof(NoticeBar),
        new PropertyMetadata(string.Empty));

    public NoticeBar()
    {
        InitializeComponent();
        UpdateVisibility();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public Severity Severity
    {
        get => (Severity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public bool IsClosable
    {
        get => (bool)GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public event EventHandler? CloseRequested;

    public event EventHandler? ActionRequested;

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((NoticeBar)d).UpdateVisibility();

    private void UpdateVisibility() => Visibility = IsOpen ? Visibility.Visible : Visibility.Collapsed;

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        IsOpen = false;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnActionButtonClick(object sender, RoutedEventArgs e) =>
        ActionRequested?.Invoke(this, EventArgs.Empty);
}
