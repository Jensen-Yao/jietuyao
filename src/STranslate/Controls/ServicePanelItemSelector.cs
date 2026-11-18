using STranslate.Plugin;
using System.Windows;
using System.Windows.Controls;

namespace STranslate.Controls;

public class ServicePanelItemSelector : DataTemplateSelector
{
    public DataTemplate? NormalTemplate { get; set; }
    public DataTemplate? DictionaryTemplate { get; set; }
    public DataTemplate? TranslateTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        => item is Service service
            ? service.Plugin switch
            {
                ITranslatePlugin => TranslateTemplate,
                IDictionaryPlugin => DictionaryTemplate,
                _ => NormalTemplate,
            }
            : NormalTemplate;
}