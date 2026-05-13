// Port of DynamicData to R3.
namespace R3.DynamicData.List.Internal;

// Audited DD #941: StatefulFilter avoids allocating a new predicate delegate on each state change
// by accepting the state as a separate observable and a (T, TState) → bool predicate.
internal sealed class StatefulFilter<T, TState>
{
    private readonly Observable<IChangeSet<T>> _source;
    private readonly Observable<TState> _stateStream;
    private readonly Func<T, TState, bool> _predicate;
    private TState _currentState;

    private sealed class Slot
    {
        public T Item = default!;
        public bool Passes;
    }

    public StatefulFilter(
        Observable<IChangeSet<T>> source,
        Observable<TState> stateStream,
        Func<T, TState, bool> predicate)
    {
        _source = source;
        _stateStream = stateStream;
        _predicate = predicate;
        _currentState = default!;
    }

    public Observable<IChangeSet<T>> Run()
    {
        return Observable.Create<IChangeSet<T>>(observer =>
        {
            var slots = new List<Slot>();
            var filtered = new ChangeAwareList<T>();
            var disp = new CompositeDisposable();
            bool hasState = false;

            _stateStream.Subscribe(
                state =>
                {
                    try
                    {
                        hasState = true;
                        _currentState = state;

                        // Re-evaluate all items
                        for (int i = 0; i < slots.Count; i++)
                        {
                            slots[i].Passes = _predicate(slots[i].Item, _currentState);
                        }

                        ReconcileFiltered(slots, filtered);
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
                observer.OnCompleted).AddTo(disp);

            _source.Subscribe(
                changes =>
                {
                    try
                    {
                        if (!hasState)
                        {
                            // Buffer source items before first state emission; default to not passing
                            ProcessSourceChanges(slots, filtered, changes, _ => false);
                        }
                        else
                        {
                            ProcessSourceChanges(slots, filtered, changes, item => _predicate(item, _currentState));
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
                },
                observer.OnErrorResume,
                observer.OnCompleted).AddTo(disp);

            return disp;
        });
    }

    private static void ReconcileFiltered(List<Slot> slots, ChangeAwareList<T> filtered)
    {
        var newFiltered = slots.Where(s => s.Passes).Select(s => s.Item).ToList();

        // Build a multiset of the required items so duplicate-equal values are
        // handled correctly (contains/IndexOf would match the wrong instance).
        // CS8714 suppressed: T may not carry notnull constraint, but Dictionary with
        // an explicit EqualityComparer is safe regardless — nulls compare equal only to
        // themselves, which is the desired behavior.
#pragma warning disable CS8714
        var requiredCounts = new Dictionary<T, int>(EqualityComparer<T>.Default);
#pragma warning restore CS8714
        foreach (var item in newFiltered)
        {
            requiredCounts[item] = requiredCounts.TryGetValue(item, out var c) ? c + 1 : 1;
        }

        // Remove surplus items from the end, decrementing the allowed count as each
        // item that should stay is encountered.
        for (int i = filtered.Count - 1; i >= 0; i--)
        {
            var item = filtered[i];
            if (requiredCounts.TryGetValue(item, out var remaining) && remaining > 0)
            {
                requiredCounts[item] = remaining - 1;
            }
            else
            {
                filtered.RemoveAt(i);
            }
        }

        // Ensure every position in filtered matches newFiltered.  If the slot at
        // position i differs, insert the desired item there (the old item and any
        // duplicates created by the inserts are trimmed in the step below).
        for (int i = 0; i < newFiltered.Count; i++)
        {
            var desired = newFiltered[i];
            if (i < filtered.Count && EqualityComparer<T>.Default.Equals(filtered[i], desired))
            {
                continue;
            }

            filtered.Insert(i, desired);
        }

        // Trim any excess items that were pushed to the end by the insertions above.
        while (filtered.Count > newFiltered.Count)
        {
            filtered.RemoveAt(filtered.Count - 1);
        }
    }

    private static void ProcessSourceChanges(List<Slot> slots, ChangeAwareList<T> filtered, IChangeSet<T> changes, Func<T, bool> passes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    HandleAdd(slots, filtered, change.Item, change.CurrentIndex, passes);
                    break;

                case ListChangeReason.AddRange:
                    if (change.Range.Count > 0)
                    {
                        int idx = change.CurrentIndex;
                        foreach (var item in change.Range)
                        {
                            HandleAdd(slots, filtered, item, idx++, passes);
                        }
                    }
                    else
                    {
                        HandleAdd(slots, filtered, change.Item, change.CurrentIndex, passes);
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
                    HandleReplace(slots, filtered, change.CurrentIndex, change.Item, passes);
                    break;

                case ListChangeReason.Moved:
                    HandleMove(slots, filtered, change.PreviousIndex, change.CurrentIndex);
                    break;

                case ListChangeReason.Clear:
                    slots.Clear();
                    filtered.Clear();
                    break;

                case ListChangeReason.Refresh:
                    HandleRefresh(slots, filtered, change.CurrentIndex, passes);
                    break;
            }
        }
    }

    private static int CountPassingBefore(List<Slot> slots, int untilIndex)
    {
        int count = 0;
        for (int i = 0; i < slots.Count && i < untilIndex; i++)
        {
            if (slots[i].Passes)
            {
                count++;
            }
        }

        return count;
    }

    private static void HandleAdd(List<Slot> slots, ChangeAwareList<T> filtered, T item, int sourceIndex, Func<T, bool> passes)
    {
        bool p = passes(item);
        slots.Insert(sourceIndex, new Slot { Item = item, Passes = p });
        if (!p)
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

    private static void HandleReplace(List<Slot> slots, ChangeAwareList<T> filtered, int sourceIndex, T newItem, Func<T, bool> passes)
    {
        if (sourceIndex < 0 || sourceIndex >= slots.Count)
        {
            return;
        }

        var slot = slots[sourceIndex];
        bool newPass = passes(newItem);
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

        int oldFilteredIndex = CountPassingBefore(slots, oldIndex < newIndex ? newIndex : oldIndex) - (oldIndex < newIndex ? 1 : 0);
        int newFilteredIndex = CountPassingBefore(slots, newIndex);
        if (oldFilteredIndex == newFilteredIndex)
        {
            return;
        }

        filtered.Move(oldFilteredIndex, newFilteredIndex);
    }

    private static void HandleRefresh(List<Slot> slots, ChangeAwareList<T> filtered, int sourceIndex, Func<T, bool> passes)
    {
        if (sourceIndex < 0 || sourceIndex >= slots.Count)
        {
            return;
        }

        var slot = slots[sourceIndex];
        bool newPass = passes(slot.Item);
        if (slot.Passes && newPass)
        {
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
