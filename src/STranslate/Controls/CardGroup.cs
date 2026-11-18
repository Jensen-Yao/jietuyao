using System.Windows;
using System.Windows.Controls;

namespace STranslate.Controls;

public class CardGroup : ItemsControl
{
    public enum CardGroupPosition
    {
        Default,
        Top,
    }

    static CardGroup()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(CardGroup),
            new FrameworkPropertyMetadata(typeof(CardGroup)));
    }

    public static readonly DependencyProperty PositionProperty = DependencyProperty.RegisterAttached(
        "Position", typeof(CardGroupPosition), typeof(CardGroup),
        new FrameworkPropertyMetadata(CardGroupPosition.Default, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    public static void SetPosition(UIElement element, CardGroupPosition value)
        => element.SetValue(PositionProperty, value);

    public static CardGroupPosition GetPosition(UIElement element)
        => (CardGroupPosition)element.GetValue(PositionProperty);
}

public class CardGroupCardStyleSelector : StyleSelector
{
    public required Style TopStyle { get; set; }
    public required Style DefaultStyle { get; set; }

    public override Style SelectStyle(object item, DependencyObject container)
    {
        var itemsControl = ItemsControl.ItemsControlFromItemContainer(container);
        var index = itemsControl.ItemContainerGenerator.IndexFromContainer(container);

        return index == 0 ? TopStyle : DefaultStyle;
    }
}