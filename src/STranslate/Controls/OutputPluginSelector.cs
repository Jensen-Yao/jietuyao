using STranslate.Plugin;
using System.Windows;
using System.Windows.Controls;

namespace STranslate.Controls;

public class OutputPluginSelector : DataTemplateSelector
{
    public DataTemplate? DictionaryTemplate { get; set; }

    public DataTemplate? TranslateTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        => item is Service service
            ? service.Plugin switch
            {
                IDictionaryPlugin => DictionaryTemplate,
                ITranslatePlugin => TranslateTemplate,
                _ => base.SelectTemplate(item, container),
            }
            : TranslateTemplate;
}