using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace STranslate.Controls;

public class ServiceSwitcherPopup : Popup
{
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SetCurrentValue(IsOpenProperty, false);
            GetBindingExpression(IsOpenProperty)?.UpdateSource();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Child?.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }
}
