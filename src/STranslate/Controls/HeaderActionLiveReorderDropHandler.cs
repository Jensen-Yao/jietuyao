using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections;
using System.Windows;

namespace STranslate.Controls;

public class HeaderActionLiveReorderDropHandler : DefaultDropHandler
{
    private const int StableHoverThresholdMs = 45;

    private object? _trackedItem;
    private IList? _trackedCollection;
    private int _pendingIndex = -1;
    private long _pendingSinceTicks;
    private int _lastCommittedIndex = -1;

    public override void DragOver(IDropInfo dropInfo)
    {
        base.DragOver(dropInfo);

        if (dropInfo.DragInfo?.SourceCollection is not IList sourceCollection ||
            dropInfo.TargetCollection is not IList targetCollection ||
            !ReferenceEquals(sourceCollection, targetCollection) ||
            dropInfo.Data is null)
        {
            ResetTracking();
            return;
        }

        var fromIndex = sourceCollection.IndexOf(dropInfo.Data);
        if (fromIndex < 0)
        {
            ResetTracking();
            return;
        }

        if (!ReferenceEquals(_trackedCollection, sourceCollection) || !ReferenceEquals(_trackedItem, dropInfo.Data))
        {
            StartTracking(sourceCollection, dropInfo.Data, fromIndex);
        }

        var toIndex = Math.Clamp(dropInfo.InsertIndex, 0, sourceCollection.Count);
        if (toIndex > fromIndex)
        {
            toIndex--;
        }

        if (toIndex == fromIndex)
        {
            _pendingIndex = -1;
            _pendingSinceTicks = 0;
            _lastCommittedIndex = fromIndex;
            dropInfo.DropTargetAdorner = null;
            return;
        }

        if (toIndex == _lastCommittedIndex)
        {
            dropInfo.DropTargetAdorner = null;
            return;
        }

        var nowTicks = Environment.TickCount64;
        if (toIndex != _pendingIndex)
        {
            _pendingIndex = toIndex;
            _pendingSinceTicks = nowTicks;
            dropInfo.DropTargetAdorner = null;
            return;
        }

        if (nowTicks - _pendingSinceTicks < StableHoverThresholdMs)
        {
            dropInfo.DropTargetAdorner = null;
            return;
        }

        MoveItem(sourceCollection, fromIndex, toIndex);

        _lastCommittedIndex = toIndex;
        _pendingIndex = -1;
        _pendingSinceTicks = 0;

        dropInfo.Effects = DragDropEffects.Move;
        dropInfo.DropTargetAdorner = null;
    }

    public override void Drop(IDropInfo dropInfo)
    {
        try
        {
            if (dropInfo.DragInfo?.SourceCollection is IList sourceCollection &&
                dropInfo.TargetCollection is IList targetCollection &&
                ReferenceEquals(sourceCollection, targetCollection))
            {
                return;
            }

            base.Drop(dropInfo);
        }
        finally
        {
            ResetTracking();
        }
    }

    private static void MoveItem(IList collection, int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex ||
            fromIndex < 0 ||
            toIndex < 0 ||
            fromIndex >= collection.Count ||
            toIndex > collection.Count)
        {
            return;
        }

        var item = collection[fromIndex];
        collection.RemoveAt(fromIndex);

        if (toIndex > collection.Count)
        {
            toIndex = collection.Count;
        }

        collection.Insert(toIndex, item);
    }

    private void StartTracking(IList collection, object data, int currentIndex)
    {
        _trackedCollection = collection;
        _trackedItem = data;
        _pendingIndex = -1;
        _pendingSinceTicks = 0;
        _lastCommittedIndex = currentIndex;
    }

    private void ResetTracking()
    {
        _trackedCollection = null;
        _trackedItem = null;
        _pendingIndex = -1;
        _pendingSinceTicks = 0;
        _lastCommittedIndex = -1;
    }
}
