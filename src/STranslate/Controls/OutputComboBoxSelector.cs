using STranslate.Plugin;
using System.Windows;
using System.Windows.Controls;

namespace STranslate.Controls;

public class OutputComboBoxSelector : DataTemplateSelector
{
    public DataTemplate? EmptyTemplate { get; set; }

    public DataTemplate? ComboBoxTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        => item is Service service
            ? service.Plugin switch
            {
                ILlm => ComboBoxTemplate,
                ITranslatePlugin => EmptyTemplate,
                _ => base.SelectTemplate(item, container),
            }
            : EmptyTemplate;
}