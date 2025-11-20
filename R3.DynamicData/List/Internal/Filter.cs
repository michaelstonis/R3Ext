// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

namespace R3.DynamicData.List.Internal;

internal sealed class Filter<T>
{
    private readonly Observable<IChangeSet<T>> _source;
    private readonly Func<T, bool> _predicate;

    private sealed class Slot
    {
        public T Item = default!;
        public bool Passes;
    }

    public Filter(Observable<IChangeSet<T>> source, Func<T, bool> predicate)
    {
        _source = source;
        _predicate = predicate;
    }

    public Observable<IChangeSet<T>> Run()
    {
        return Observable.Create<IChangeSet<T>>(observer =>
        {
            var slots = new List<Slot>(); // mirrors source ordering
            var filtered = new ChangeAwareList<T>();

            var disp = _source.Subscribe(
                changes =>
                {
                    try
                    {
                        Process(slots, filtered, changes);
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
                },
                observer.OnErrorResume,
                observer.OnCompleted);

            return disp;
        });
    }

    private void Process(List<Slot> slots, ChangeAwareList<T> filtered, IChangeSet<T> changes)
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
                    if (slots.Count > 0)
                    {
                        for (int i = slots.Count - 1; i >= 0; i--)
                        {
                            HandleRemove(slots, filtered, i);
                        }
                    }

                    break;

                case ListChangeReason.Refresh:
                    HandleRefresh(slots, filtered, change.CurrentIndex);
                    break;
            }
        }
    }

    private static int CountPassingBefore(List<Slot> slots, int untilIndex)
    {
        int count = 0;
        for (int i = 0; i < untilIndex && i < slots.Count; i++)
        {
            if (slots[i].Passes)
            {
                count++;
            }
        }

        return count;
    }

    private void HandleAdd(List<Slot> slots, ChangeAwareList<T> filtered, T item, int sourceIndex)
    {
        bool passes = _predicate(item);
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
        bool newPass = _predicate(newItem);
        if (slot.Passes && newPass)
        {
            // replace in filtered
            int filteredIndex = CountPassingBefore(slots, sourceIndex);
            filtered[filteredIndex] = newItem;
            slot.Item = newItem;
            return;
        }

        if (slot.Passes && !newPass)
        {
            // removal
            int filteredIndex = CountPassingBefore(slots, sourceIndex);
            filtered.RemoveAt(filteredIndex);
            slot.Item = newItem;
            slot.Passes = false;
            return;
        }

        if (!slot.Passes && newPass)
        {
            // addition
            slot.Item = newItem;
            slot.Passes = true;
            int filteredIndex = CountPassingBefore(slots, sourceIndex);
            filtered.Insert(filteredIndex, newItem);
            return;
        }

        slot.Item = newItem;
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

        int oldFilteredIndex = CountPassingBefore(slots, oldIndex < newIndex ? newIndex : oldIndex) - (oldIndex < newIndex ? 1 : 0); // approximate original position

        // Recompute precise new position
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
        bool newPass = _predicate(slot.Item);
        if (slot.Passes && newPass)
        {
            // emit refresh as replace
            int filteredIndex = CountPassingBefore(slots, sourceIndex);
            filtered[filteredIndex] = slot.Item;
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
