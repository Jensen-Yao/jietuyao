using GongSolutions.Wpf.DragDrop;
using System.Collections;
using System.Windows;

namespace STranslate.Controls;

public class HeaderActionFixedOrderDropHandler : DefaultDropHandler
{
    public override void DragOver(IDropInfo dropInfo)
    {
        if (dropInfo.DragInfo?.SourceCollection is IList sourceCollection &&
            dropInfo.TargetCollection is IList targetCollection &&
            ReferenceEquals(sourceCollection, targetCollection))
        {
            // 下方池子保持固定顺序，不允许内部重排。
            dropInfo.Effects = DragDropEffects.None;
            dropInfo.DropTargetAdorner = null;
            return;
        }

        base.DragOver(dropInfo);
        dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
    }

    public override void Drop(IDropInfo dropInfo)
    {
        if (dropInfo.DragInfo?.SourceCollection is IList sourceCollection &&
            dropInfo.TargetCollection is IList targetCollection &&
            ReferenceEquals(sourceCollection, targetCollection))
        {
            return;
        }

        base.Drop(dropInfo);
    }
}
