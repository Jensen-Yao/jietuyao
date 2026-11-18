using System.Windows;
using System.Windows.Controls;

namespace STranslate.Controls;

public class STranslateBadge : Control
{
    static STranslateBadge()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(STranslateBadge), new FrameworkPropertyMetadata(typeof(STranslateBadge)));
    }
}
