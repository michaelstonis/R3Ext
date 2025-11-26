// Port of DynamicData EnsureUniqueKeys (UniquenessEnforcer) to R3.
using System;
using System.Collections.Generic;
using System.Linq;
using R3.DynamicData.Kernel;

namespace R3.DynamicData.Cache.Internal;

internal sealed class EnsureUniqueKeys<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Observable<IChangeSet<TObject, TKey>> _source;

    public EnsureUniqueKeys(Observable<IChangeSet<TObject, TKey>> source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public Observable<IChangeSet<TObject, TKey>> Run()
    {
        // Collapse multiple changes for the same key in a single batch to a single net change.
        // Semantics (mirrors DynamicData tests):
        //  - Multiple Add/Update for new key -> single Add with last value.
        //  - Add + Remove (and any intervening updates/refresh) for new key -> no net change.
        //  - Refresh combined with Add/Update in same batch is ignored (net Add/Update).
        //  - Multiple Refresh only -> single Refresh.
        //  - Update for existing key emitted as Update; if key did not exist prior to batch treat as Add.
        //  - Remove for existing key emitted; if key new in batch (Add then Remove) suppressed.
        var opState = new EnsureUniqueKeysState(_source);
        return Observable.Create<IChangeSet<TObject, TKey>, EnsureUniqueKeysState>(
            opState,
            static (observer, opState) =>
            {
                var state = new Dictionary<TKey, TObject>(); // Tracks cache state across batches.
                return opState.Source.Subscribe(
                    changes =>
                    {
                        if (changes.Count == 0)
                        {
                            return; // nothing to process
                        }

                        var result = new ChangeSet<TObject, TKey>();

                        foreach (var grouping in changes.GroupBy(c => c.Key))
                        {
                            var key = grouping.Key;
                            var all = grouping.ToList();
                            var existedBeforeBatch = state.ContainsKey(key);

                            // Determine candidate change (last non-refresh if any, else first refresh)
                            Change<TObject, TKey> candidate;
                            var idx = all.FindLastIndex(c => c.Reason != Kernel.ChangeReason.Refresh);
                            candidate = idx >= 0 ? all[idx] : all[0]; // all refresh case

                            bool hadAddOrUpdateInBatch = all.Any(c => c.Reason == Kernel.ChangeReason.Add || c.Reason == Kernel.ChangeReason.Update);
                            bool hadRemoveInBatch = all.Any(c => c.Reason == Kernel.ChangeReason.Remove);

                            // Cancel out Add+Remove sequence for new key (no emission)
                            if (!existedBeforeBatch && hadAddOrUpdateInBatch && hadRemoveInBatch && candidate.Reason == Kernel.ChangeReason.Remove)
                            {
                                continue; // suppressed
                            }

                            var finalReason = candidate.Reason;
                            var finalValue = candidate.Current;
                            var previousValue = candidate.Previous.HasValue ? candidate.Previous.Value : default;

                            // Treat Update for brand new key as Add (DynamicData behaviour inside batch)
                            if (!existedBeforeBatch && finalReason == Kernel.ChangeReason.Update)
                            {
                                finalReason = Kernel.ChangeReason.Add;
                                previousValue = default!; // no previous
                            }

                            // If candidate is Refresh but there was an Add/Update earlier, convert to Add/Update behaviour
                            if (finalReason == Kernel.ChangeReason.Refresh && hadAddOrUpdateInBatch)
                            {
                                // Determine last non-refresh add/update value
                                var hasLastNonRefresh = all.Any(c => c.Reason == Kernel.ChangeReason.Add || c.Reason == Kernel.ChangeReason.Update);
                                if (hasLastNonRefresh)
                                {
                                    var lastNonRefresh = all.Last(c => c.Reason == Kernel.ChangeReason.Add || c.Reason == Kernel.ChangeReason.Update);
                                    finalReason = !existedBeforeBatch && lastNonRefresh.Reason == Kernel.ChangeReason.Update
                                        ? Kernel.ChangeReason.Add
                                        : lastNonRefresh.Reason;
                                    finalValue = lastNonRefresh.Current;
                                    previousValue = lastNonRefresh.Previous.HasValue ? lastNonRefresh.Previous.Value : default!;
                                }
                                else
                                {
                                    // all were refresh: keep single refresh
                                    finalReason = Kernel.ChangeReason.Refresh;
                                }
                            }

                            // Build consolidated change
                            switch (finalReason)
                            {
                                case Kernel.ChangeReason.Add:
                                    state[key] = finalValue;
                                    result.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Add, key, finalValue));
                                    break;
                                case Kernel.ChangeReason.Update:
                                    if (existedBeforeBatch)
                                    {
                                        var prev = previousValue!;
                                        state[key] = finalValue;
                                        result.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Update, key, finalValue, prev));
                                    }
                                    else
                                    {
                                        // safety: treat as add if state missing
                                        state[key] = finalValue;
                                        result.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Add, key, finalValue));
                                    }

                                    break;
                                case Kernel.ChangeReason.Remove:
                                    if (existedBeforeBatch)
                                    {
                                        var prevVal = state[key];
                                        state.Remove(key);
                                        result.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Remove, key, prevVal, prevVal));
                                    }
                                    else
                                    {
                                        // remove for non-existent key or canceled add+remove -> ignore
                                    }

                                    break;
                                case Kernel.ChangeReason.Refresh:
                                    if (state.TryGetValue(key, out var existing))
                                    {
                                        result.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Refresh, key, existing));
                                    }

                                    // else ignore refresh for missing key
                                    break;
                                case Kernel.ChangeReason.Moved:

                                    // Not applicable for cache uniqueness consolidation.
                                    break;
                            }
                        }

                        if (result.Count > 0)
                        {
                            observer.OnNext(result);
                        }
                    },
                    observer.OnErrorResume,
                    observer.OnCompleted);
            });
    }

    private readonly struct EnsureUniqueKeysState
    {
        public readonly Observable<IChangeSet<TObject, TKey>> Source;

        public EnsureUniqueKeysState(Observable<IChangeSet<TObject, TKey>> source) => Source = source;
    }
}
