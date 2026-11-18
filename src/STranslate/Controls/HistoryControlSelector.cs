using STranslate.Core;
using System.Windows;
using System.Windows.Controls;

namespace STranslate.Controls;

public class HistoryControlSelector : DataTemplateSelector
{
    public DataTemplate? DictionaryTemplate { get; set; }

    public DataTemplate? TranslateTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not HistoryData data)
            return base.SelectTemplate(item, container);

        if (data.DictResult != null)
            return DictionaryTemplate ?? base.SelectTemplate(item, container);

        if (data.TransResult != null || data.TransBackResult != null)
            return TranslateTemplate ?? base.SelectTemplate(item, container);

        return base.SelectTemplate(item, container);
    }
}
