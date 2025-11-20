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
                    result.Clear();
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
        if (children.Count == 1)
        {
            result.Insert(insertAt, children[0]);
        }
        else
        {
            result.InsertRange(children, insertAt);
        }
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

        if (count == 1)
        {
            result.RemoveAt(start);
        }
        else
        {
            result.RemoveRange(start, count);
        }
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

        if (totalChildCount == 1)
        {
            result.RemoveAt(start);
        }
        else
        {
            result.RemoveRange(start, totalChildCount);
        }
    }

    private void HandleReplace(List<ParentEntry> parents, ChangeAwareList<TDestination> result, int parentIndex, TSource newParent, TSource? previousParent)
    {
        if (parentIndex < 0 || parentIndex >= parents.Count)
        {
            return;
        }

        var oldEntry = parents[parentIndex];
        var oldChildren = oldEntry.Children;
        var newChildren = _manySelector(newParent)?.ToList() ?? new List<TDestination>();
        parents[parentIndex] = new ParentEntry { Source = newParent, Children = newChildren };

        int start = ComputeChildStartIndex(parents, parentIndex);

        // Diff
        var removed = oldChildren.Where(c => !newChildren.Contains(c, _comparer)).ToList();
        var added = newChildren.Where(c => !oldChildren.Contains(c, _comparer)).ToList();

        if (removed.Count == 0 && added.Count == 0)
        {
            return; // no changes
        }

        // Remove in descending index order for stability.
        if (removed.Count > 0)
        {
            var removalIndices = removed.Select(r => oldChildren.FindIndex(x => _comparer.Equals(x, r))).Where(i => i >= 0).OrderByDescending(i => i).ToList();
            foreach (var idx in removalIndices)
            {
                result.RemoveAt(start + idx);
            }
        }

        if (added.Count > 0)
        {
            // Insert all added at end of current block (after removals). Compute current block length.
            int currentBlockLength = parents[parentIndex].Children.Count - removed.Count; // approximate length after removals
            int insertAt = start + currentBlockLength;
            if (added.Count == 1)
            {
                result.Insert(insertAt, added[0]);
            }
            else
            {
                result.InsertRange(added, insertAt);
            }
        }
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
        parents.RemoveAt(oldParentIndex);
        parents.Insert(newParentIndex, entry);

        // Move block of children.
        var blockSize = entry.Children.Count;
        if (blockSize == 0)
        {
            return;
        }

        int oldStart = ComputeChildStartIndex(parents, oldParentIndex < newParentIndex ? newParentIndex : oldParentIndex);

        // After removal+insert, indices shift; recompute positions.
        oldStart = ComputeChildStartIndex(parents, newParentIndex);
        int newStart = ComputeChildStartIndex(parents, newParentIndex);
        if (oldStart == newStart)
        {
            return;
        }

        // Move each child individually to preserve ordering.
        // If moving forward (oldStart < newStart) indices shift after each move; adjust.
        if (oldStart < newStart)
        {
            for (int i = 0; i < blockSize; i++)
            {
                result.Move(oldStart, newStart + i);
            }
        }
        else
        {
            for (int i = 0; i < blockSize; i++)
            {
                result.Move(oldStart + i, newStart + i);
            }
        }
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
}
