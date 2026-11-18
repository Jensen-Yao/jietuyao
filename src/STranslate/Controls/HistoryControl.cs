using System.Windows;
using System.Windows.Controls;

namespace STranslate.Controls;

public class HistoryControl : Control
{
    static HistoryControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HistoryControl),
            new FrameworkPropertyMetadata(typeof(HistoryControl)));
    }
}