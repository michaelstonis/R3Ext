// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

namespace R3.DynamicData.List.Internal;

internal sealed class DynamicFilter<T>
{
    private readonly Observable<IChangeSet<T>> _source;
    private readonly Observable<Func<T, bool>> _predicateChanged;
    private Func<T, bool> _currentPredicate;

    private sealed class Slot
    {
        public required T Item;
        public bool Passes;
    }

    public DynamicFilter(Observable<IChangeSet<T>> source, Observable<Func<T, bool>> predicateChanged)
    {
        _source = source;
        _predicateChanged = predicateChanged;
        _currentPredicate = _ => true; // default until first emission
    }

    public Observable<IChangeSet<T>> Run()
    {
        return Observable.Create<IChangeSet<T>>(observer =>
        {
            var slots = new List<Slot>();
            var filtered = new ChangeAwareList<T>();
            var disp = new CompositeDisposable();

            // Subscribe to predicate changes
            _predicateChanged.Subscribe(p =>
            {
                try
                {
                    var previousPassing = slots.Where(s => s.Passes).Select(s => s.Item).ToList();
                    _currentPredicate = p;
                    // Re-evaluate all
                    for (int i = 0; i < slots.Count; i++)
                    {
                        slots[i].Passes = _currentPredicate(slots[i].Item);
                    }
                    var currentPassing = slots.Where(s => s.Passes).Select(s => s.Item).ToList();

                    // Diff
                    // Removed items
                    foreach (var removed in previousPassing.Where(x => !currentPassing.Contains(x)))
                    {
                        int oldIndex = previousPassing.IndexOf(removed); // original filtered index
                        filtered.RemoveAt(oldIndex);
                        // Adjust previousPassing list indices after removal
                        previousPassing.RemoveAt(oldIndex);
                    }

                    // Added items (in order of currentPassing relative to slots)
                    foreach (var added in currentPassing.Where(x => !previousPassing.Contains(x)))
                    {
                        // Determine new index in filtered list by counting passing items before in slots
                        int sourceIndex = slots.FindIndex(s => ReferenceEquals(s.Item, added) || EqualityComparer<T>.Default.Equals(s.Item, added));
                        int filteredIndex = CountPassingBefore(slots, sourceIndex, includeSourceIndex: true);
                        // Adjust for previously existing items already in filtered (some earlier adds might have shifted index)
                        // Recompute filteredIndex by building current filtered sequence so far
                        var existingFiltered = filtered.ToList();
                        // If item already exists skip (shouldn't happen)
                        if (existingFiltered.Contains(added))
                        {
                            continue;
                        }
                        // Insert
                        filtered.Insert(filteredIndex, added);
                        // Update previousPassing to reflect addition for subsequent index calculations
                        previousPassing.Insert(filteredIndex, added);
                    }

                    var output = filtered.CaptureChanges();
                    if (output.Count > 0)
                    {
                        observer.OnNext(output);
                    }
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }, observer.OnErrorResume).AddTo(disp);

            // Subscribe to source changes
            _source.Subscribe(changes =>
            {
                try
                {
                    ProcessSourceChanges(slots, filtered, changes);
                    var output = filtered.CaptureChanges();
                    if (output.Count > 0)
                    {
                        observer.OnNext(output);
                    }
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }, observer.OnErrorResume, observer.OnCompleted).AddTo(disp);

            return disp;
        });
    }

    private void ProcessSourceChanges(List<Slot> slots, ChangeAwareList<T> filtered, IChangeSet<T> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    HandleAdd(slots, filtered, change.Item, change.CurrentIndex);
                    break;
                case ListChangeReason.AddRange:
                    if (change.Range.Count > 0)
                    {
                        int idx = change.CurrentIndex;
                        foreach (var item in change.Range)
                        {
                            HandleAdd(slots, filtered, item, idx++);
                        }
                    }
                    else
                    {
                        HandleAdd(slots, filtered, change.Item, change.CurrentIndex);
                    }
                    break;
                case ListChangeReason.Remove:
                    HandleRemove(slots, filtered, change.CurrentIndex);
                    break;
                case ListChangeReason.RemoveRange:
                    if (change.Range.Count > 0)
                    {
                        for (int i = 0; i < change.Range.Count; i++)
                        {
                            HandleRemove(slots, filtered, change.CurrentIndex);
                        }
                    }
                    else
                    {
                        HandleRemove(slots, filtered, change.CurrentIndex);
                    }
                    break;
                case ListChangeReason.Replace:
                    HandleReplace(slots, filtered, change.CurrentIndex, change.Item);
                    break;
                case ListChangeReason.Moved:
                    HandleMove(slots, filtered, change.PreviousIndex, change.CurrentIndex);
                    break;
                case ListChangeReason.Clear:
                    slots.Clear();
                    filtered.Clear();
                    break;
                case ListChangeReason.Refresh:
                    HandleRefresh(slots, filtered, change.CurrentIndex);
                    break;
            }
        }
    }

    private static int CountPassingBefore(List<Slot> slots, int untilIndex, bool includeSourceIndex = false)
    {
        int count = 0;
        for (int i = 0; i < slots.Count && i < untilIndex; i++)
        {
            if (slots[i].Passes)
            {
                count++;
            }
        }
        if (includeSourceIndex && untilIndex >= 0 && untilIndex < slots.Count && slots[untilIndex].Passes)
        {
            count++;
        }
        return count;
    }

    private void HandleAdd(List<Slot> slots, ChangeAwareList<T> filtered, T item, int sourceIndex)
    {
        bool passes = _currentPredicate(item);
        slots.Insert(sourceIndex, new Slot { Item = item, Passes = passes });
        if (!passes)
        {
            return;
        }
        int filteredIndex = CountPassingBefore(slots, sourceIndex);
        filtered.Insert(filteredIndex, item);
    }

    private void HandleRemove(List<Slot> slots, ChangeAwareList<T> filtered, int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= slots.Count)
        {
            return;
        }
        bool passes = slots[sourceIndex].Passes;
        if (passes)
        {
            int filteredIndex = CountPassingBefore(slots, sourceIndex);
            filtered.RemoveAt(filteredIndex);
        }
        slots.RemoveAt(sourceIndex);
    }

    private void HandleReplace(List<Slot> slots, ChangeAwareList<T> filtered, int sourceIndex, T newItem)
    {
        if (sourceIndex < 0 || sourceIndex >= slots.Count)
        {
            return;
        }
        var slot = slots[sourceIndex];
        bool newPass = _currentPredicate(newItem);
        if (slot.Passes && newPass)
        {
            int filteredIndex = CountPassingBefore(slots, sourceIndex);
            filtered[filteredIndex] = newItem;
            slot.Item = newItem;
            return;
        }
        if (slot.Passes && !newPass)
        {
            int filteredIndex = CountPassingBefore(slots, sourceIndex);
            filtered.RemoveAt(filteredIndex);
            slot.Item = newItem;
            slot.Passes = false;
            return;
        }
        if (!slot.Passes && newPass)
        {
            slot.Item = newItem;
            slot.Passes = true;
            int filteredIndex = CountPassingBefore(slots, sourceIndex);
            filtered.Insert(filteredIndex, newItem);
            return;
        }
        slot.Item = newItem; // remains non-passing
    }

    private void HandleMove(List<Slot> slots, ChangeAwareList<T> filtered, int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex)
        {
            return;
        }
        if (oldIndex < 0 || oldIndex >= slots.Count || newIndex < 0 || newIndex > slots.Count)
        {
            return;
        }
        var slot = slots[oldIndex];
        slots.RemoveAt(oldIndex);
        slots.Insert(newIndex, slot);
        if (!slot.Passes)
        {
            return;
        }
        int oldFilteredIndex = CountPassingBefore(slots, oldIndex < newIndex ? newIndex : oldIndex) - (oldIndex < newIndex ? 1 : 0);
        int newFilteredIndex = CountPassingBefore(slots, newIndex);
        if (oldFilteredIndex == newFilteredIndex)
        {
            return;
        }
        filtered.Move(oldFilteredIndex, newFilteredIndex);
    }

    private void HandleRefresh(List<Slot> slots, ChangeAwareList<T> filtered, int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= slots.Count)
        {
            return;
        }
        var slot = slots[sourceIndex];
        bool newPass = _currentPredicate(slot.Item);
        if (slot.Passes && newPass)
        {
            int filteredIndex = CountPassingBefore(slots, sourceIndex);
            filtered[filteredIndex] = slot.Item; // treat as replace
            return;
        }
        if (slot.Passes && !newPass)
        {
            int filteredIndex = CountPassingBefore(slots, sourceIndex);
            filtered.RemoveAt(filteredIndex);
            slot.Passes = false;
            return;
        }
        if (!slot.Passes && newPass)
        {
            slot.Passes = true;
            int filteredIndex = CountPassingBefore(slots, sourceIndex);
            filtered.Insert(filteredIndex, slot.Item);
        }
    }
}
