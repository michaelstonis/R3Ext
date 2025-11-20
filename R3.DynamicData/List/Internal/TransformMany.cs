// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

namespace R3.DynamicData.List.Internal;

internal sealed class TransformMany<TSource, TDestination>
    where TDestination : notnull
{
    private readonly Observable<IChangeSet<TSource>> _source;
    private readonly Func<TSource, IEnumerable<TDestination>> _manySelector;
    private readonly IEqualityComparer<TDestination> _comparer;

    private sealed class ParentEntry
    {
        public TSource Source = default!;
        public List<TDestination> Children = new();
    }

    public TransformMany(
        Observable<IChangeSet<TSource>> source,
        Func<TSource, IEnumerable<TDestination>> manySelector,
        IEqualityComparer<TDestination>? comparer = null)
    {
        _source = source;
        _manySelector = manySelector;
        _comparer = comparer ?? EqualityComparer<TDestination>.Default;
    }

    public Observable<IChangeSet<TDestination>> Run()
    {
        return Observable.Create<IChangeSet<TDestination>>(observer =>
        {
            var parents = new List<ParentEntry>();
            var result = new ChangeAwareList<TDestination>();

            var disp = _source.Subscribe(
                changes =>
                {
                    try
                    {
                        ProcessChanges(parents, result, changes);
                        var output = result.CaptureChanges();
                        if (output.Count > 0)
                        {
                            observer.OnNext(output);
                        }
                    }
                    catch (Exception ex)
                    {
                        observer.OnErrorResume(ex);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);

            return disp;
        });
    }

    private void ProcessChanges(List<ParentEntry> parents, ChangeAwareList<TDestination> result, IChangeSet<TSource> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    HandleAdd(parents, result, change.Item, change.CurrentIndex);
                    break;

                case ListChangeReason.AddRange:
                    if (change.Range.Count > 0)
                    {
                        int insertIndex = change.CurrentIndex;
                        foreach (var parent in change.Range)
                        {
                            HandleAdd(parents, result, parent, insertIndex++);
                        }
                    }
                    else
                    {
                        HandleAdd(parents, result, change.Item, change.CurrentIndex);
                    }

                    break;

                case ListChangeReason.Remove:
                    HandleRemove(parents, result, change.CurrentIndex);
                    break;

                case ListChangeReason.RemoveRange:
                    if (change.Range.Count > 0)
                    {
                        HandleRemoveRange(parents, result, change.CurrentIndex, change.Range.Count);
                    }
                    else
                    {
                        HandleRemove(parents, result, change.CurrentIndex);
                    }

                    break;

                case ListChangeReason.Replace:
                    HandleReplace(parents, result, change.CurrentIndex, change.Item, change.PreviousItem);
                    break;

                case ListChangeReason.Moved:
                    // Movement of parent requires moving its children block.
                    HandleMove(parents, result, change.PreviousIndex, change.CurrentIndex);
                    break;

                case ListChangeReason.Clear:
                    parents.Clear();
                    if (result.Count > 0)
                    {
                        for (int i = result.Count - 1; i >= 0; i--)
                        {
                            result.RemoveAt(i);
                        }
                    }

                    break;

                case ListChangeReason.Refresh:
                    // Re-evaluate children for parent at index; treat diff as replace semantics.
                    HandleRefresh(parents, result, change.CurrentIndex);
                    break;
            }
        }
    }

    private static int ComputeChildStartIndex(List<ParentEntry> parents, int parentIndex)
    {
        int idx = 0;
        for (int i = 0; i < parentIndex; i++)
        {
            idx += parents[i].Children.Count;
        }

        return idx;
    }

    private void HandleAdd(List<ParentEntry> parents, ChangeAwareList<TDestination> result, TSource parent, int parentIndex)
    {
        var children = _manySelector(parent)?.ToList() ?? new List<TDestination>();
        var entry = new ParentEntry { Source = parent, Children = children };
        parents.Insert(parentIndex, entry);
        if (children.Count == 0)
        {
            return;
        }

        int insertAt = ComputeChildStartIndex(parents, parentIndex);
        InsertChildren(result, children, insertAt);
    }

    private void HandleRemove(List<ParentEntry> parents, ChangeAwareList<TDestination> result, int parentIndex)
    {
        if (parentIndex < 0 || parentIndex >= parents.Count)
        {
            return;
        }

        int start = ComputeChildStartIndex(parents, parentIndex);
        var count = parents[parentIndex].Children.Count;
        parents.RemoveAt(parentIndex);
        if (count == 0)
        {
            return;
        }

        RemoveChildren(result, start, count);
    }

    private void HandleRemoveRange(List<ParentEntry> parents, ChangeAwareList<TDestination> result, int parentIndex, int parentCount)
    {
        if (parentCount <= 0)
        {
            return;
        }

        int start = ComputeChildStartIndex(parents, parentIndex);
        int totalChildCount = 0;
        for (int i = 0; i < parentCount && parentIndex + i < parents.Count; i++)
        {
            totalChildCount += parents[parentIndex + i].Children.Count;
        }

        parents.RemoveRange(parentIndex, parentCount);
        if (totalChildCount == 0)
        {
            return;
        }

        RemoveChildren(result, start, totalChildCount);
    }

    private void HandleReplace(List<ParentEntry> parents, ChangeAwareList<TDestination> result, int parentIndex, TSource newParent, TSource? previousParent)
    {
        if (parentIndex < 0 || parentIndex >= parents.Count)
        {
            return;
        }

        var oldEntry = parents[parentIndex];
        var oldChildren = new List<TDestination>(oldEntry.Children);
        var newChildren = _manySelector(newParent)?.ToList() ?? new List<TDestination>();
        int start = ComputeChildStartIndex(parents, parentIndex);

        AlignChildBlock(result, start, oldChildren, newChildren);

        parents[parentIndex] = new ParentEntry { Source = newParent, Children = newChildren };
    }

    private void HandleMove(List<ParentEntry> parents, ChangeAwareList<TDestination> result, int oldParentIndex, int newParentIndex)
    {
        if (oldParentIndex == newParentIndex)
        {
            return;
        }

        if (oldParentIndex < 0 || oldParentIndex >= parents.Count || newParentIndex < 0 || newParentIndex >= parents.Count)
        {
            return;
        }

        var entry = parents[oldParentIndex];
        var children = new List<TDestination>(entry.Children);

        int oldStart = ComputeChildStartIndex(parents, oldParentIndex);

        parents.RemoveAt(oldParentIndex);
        parents.Insert(newParentIndex, entry);

        if (children.Count == 0)
        {
            return;
        }

        RemoveChildren(result, oldStart, children.Count);

        int newStart = ComputeChildStartIndex(parents, newParentIndex);
        InsertChildren(result, children, newStart);
    }

    private void HandleRefresh(List<ParentEntry> parents, ChangeAwareList<TDestination> result, int parentIndex)
    {
        if (parentIndex < 0 || parentIndex >= parents.Count)
        {
            return;
        }

        var parent = parents[parentIndex].Source;
        HandleReplace(parents, result, parentIndex, parent, parent);
    }

    private void InsertChildren(ChangeAwareList<TDestination> result, IReadOnlyList<TDestination> children, int insertAt)
    {
        for (int i = 0; i < children.Count; i++)
        {
            result.Insert(insertAt + i, children[i]);
        }
    }

    private static void RemoveChildren(ChangeAwareList<TDestination> result, int startIndex, int count)
    {
        for (int i = 0; i < count; i++)
        {
            result.RemoveAt(startIndex);
        }
    }

    private void AlignChildBlock(ChangeAwareList<TDestination> result, int startIndex, List<TDestination> currentChildren, List<TDestination> targetChildren)
    {
        var working = new List<TDestination>(currentChildren);
        int position = 0;

        while (position < targetChildren.Count)
        {
            if (position < working.Count && _comparer.Equals(working[position], targetChildren[position]))
            {
                position++;
                continue;
            }

            int existingIndex = FindIndex(working, targetChildren[position], position + 1);
            if (existingIndex >= 0)
            {
                for (int removeIdx = existingIndex - 1; removeIdx >= position; removeIdx--)
                {
                    result.RemoveAt(startIndex + removeIdx);
                    working.RemoveAt(removeIdx);
                }

                position++;
                continue;
            }

            result.Insert(startIndex + position, targetChildren[position]);
            working.Insert(position, targetChildren[position]);
            position++;
        }

        for (int removeIdx = working.Count - 1; removeIdx >= targetChildren.Count; removeIdx--)
        {
            result.RemoveAt(startIndex + removeIdx);
            working.RemoveAt(removeIdx);
        }
    }

    private int FindIndex(List<TDestination> items, TDestination value, int startIndex)
    {
        for (int i = startIndex; i < items.Count; i++)
        {
            if (_comparer.Equals(items[i], value))
            {
                return i;
            }
        }

        return -1;
    }
}
