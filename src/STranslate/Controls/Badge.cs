using System.Windows;
using System.Windows.Controls;

namespace STranslate.Controls;

public class Badge : Control
{
    static Badge()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Badge),
            new FrameworkPropertyMetadata(typeof(Badge)));
    }

    public bool IsOfficial
    {
        get => (bool)GetValue(IsOfficialProperty);
        set => SetValue(IsOfficialProperty, value);
    }

    public static readonly DependencyProperty IsOfficialProperty =
        DependencyProperty.Register(nameof(IsOfficial), typeof(bool), typeof(Badge), new PropertyMetadata(false));


    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(Badge), new PropertyMetadata(new CornerRadius(6)));


}