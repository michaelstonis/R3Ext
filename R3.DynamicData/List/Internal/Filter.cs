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
        return Observable.Create<IChangeSet<T>, FilterState<T>>(
            new FilterState<T>(_source, _predicate),
            static (observer, state) =>
            {
                var slots = new List<Slot>(); // mirrors source ordering
                var filtered = new ChangeAwareList<T>();

                var disp = state.Source.Subscribe(
                    (observer, state, slots, filtered),
                    static (changes, tuple) =>
                    {
                        try
                        {
                            Process(tuple.slots, tuple.filtered, changes, tuple.state.Predicate);
                            var output = tuple.filtered.CaptureChanges();
                            if (output.Count > 0)
                            {
                                tuple.observer.OnNext(output);
                            }
                        }
                        catch (Exception ex)
                        {
                            tuple.observer.OnErrorResume(ex);
                        }
                    },
                    static (ex, tuple) => tuple.observer.OnErrorResume(ex),
                    static (result, tuple) =>
                    {
                        if (result.IsSuccess)
                        {
                            tuple.observer.OnCompleted();
                        }
                        else
                        {
                            tuple.observer.OnCompleted(result);
                        }
                    });

                return disp;
            });
    }

    private static void Process(List<Slot> slots, ChangeAwareList<T> filtered, IChangeSet<T> changes, Func<T, bool> predicate)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    HandleAdd(slots, filtered, change.Item, change.CurrentIndex, predicate);
                    break;

                case ListChangeReason.AddRange:
                    if (change.Range.Count > 0)
                    {
                        int idx = change.CurrentIndex;
                        foreach (var item in change.Range)
                        {
                            HandleAdd(slots, filtered, item, idx++, predicate);
                        }
                    }
                    else
                    {
                        HandleAdd(slots, filtered, change.Item, change.CurrentIndex, predicate);
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
                    HandleReplace(slots, filtered, change.CurrentIndex, change.Item, predicate);
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
                    HandleRefresh(slots, filtered, change.CurrentIndex, predicate);
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

    private static void HandleAdd(List<Slot> slots, ChangeAwareList<T> filtered, T item, int sourceIndex, Func<T, bool> predicate)
    {
        bool passes = predicate(item);
        slots.Insert(sourceIndex, new Slot { Item = item, Passes = passes });
        if (!passes)
        {
            return;
        }

        int filteredIndex = CountPassingBefore(slots, sourceIndex);
        filtered.Insert(filteredIndex, item);
    }

    private static void HandleRemove(List<Slot> slots, ChangeAwareList<T> filtered, int sourceIndex)
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

    private static void HandleReplace(List<Slot> slots, ChangeAwareList<T> filtered, int sourceIndex, T newItem, Func<T, bool> predicate)
    {
        if (sourceIndex < 0 || sourceIndex >= slots.Count)
        {
            return;
        }

        var slot = slots[sourceIndex];
        bool newPass = predicate(newItem);
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

    private static void HandleMove(List<Slot> slots, ChangeAwareList<T> filtered, int oldIndex, int newIndex)
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

    private static void HandleRefresh(List<Slot> slots, ChangeAwareList<T> filtered, int sourceIndex, Func<T, bool> predicate)
    {
        if (sourceIndex < 0 || sourceIndex >= slots.Count)
        {
            return;
        }

        var slot = slots[sourceIndex];
        bool newPass = predicate(slot.Item);
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

    private readonly struct FilterState<TItem>
    {
        public readonly Observable<IChangeSet<TItem>> Source;
        public readonly Func<TItem, bool> Predicate;

        public FilterState(Observable<IChangeSet<TItem>> source, Func<TItem, bool> predicate)
        {
            Source = source;
            Predicate = predicate;
        }
    }
}
