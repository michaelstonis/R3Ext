// Port of DynamicData ChangeKey operator to R3.
// Emits change sets projected to a new key space using a selector.

using System;
using System.Collections.Generic;
using R3.DynamicData.Kernel;

namespace R3.DynamicData.Cache.Internal;

internal sealed class ChangeKeyOperator<TObject, TOldKey, TNewKey>
    where TObject : notnull
    where TOldKey : notnull
    where TNewKey : notnull
{
    private readonly Observable<IChangeSet<TObject, TOldKey>> _source;
    private readonly Func<TObject, TNewKey> _keySelector;

    public ChangeKeyOperator(Observable<IChangeSet<TObject, TOldKey>> source, Func<TObject, TNewKey> keySelector)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
    }

    public Observable<IChangeSet<TObject, TNewKey>> Run()
    {
        return Observable.Create<IChangeSet<TObject, TNewKey>>(observer =>
        {
            // Map from upstream key to projected key.
            var projectedKeyByUpstream = new Dictionary<TOldKey, TNewKey>();
            return _source.Subscribe(
                changes =>
            {
                if (changes.Count == 0)
                {
                    return;
                }

                var result = new ChangeSet<TObject, TNewKey>();

                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                        {
                            var newKey = _keySelector(change.Current);
                            projectedKeyByUpstream[change.Key] = newKey;
                            result.Add(new Change<TObject, TNewKey>(ChangeReason.Add, newKey, change.Current));
                            break;
                        }

                        case ChangeReason.Update:
                        {
                            var newKey = _keySelector(change.Current);
                            var hadPrevious = change.Previous.HasValue;
                            var oldProjected = hadPrevious && projectedKeyByUpstream.TryGetValue(change.Key, out var prevProj)
                                ? prevProj
                                : _keySelector(hadPrevious ? change.Previous.Value : change.Current);

                            if (EqualityComparer<TNewKey>.Default.Equals(oldProjected, newKey))
                            {
                                // Key unchanged -> emit Update with same projected key.
                                var prevVal = hadPrevious ? change.Previous.Value : default!;
                                result.Add(new Change<TObject, TNewKey>(ChangeReason.Update, newKey, change.Current, prevVal));
                            }
                            else
                            {
                                // Key changed -> emit Remove (old projected) then Add (new projected).
                                if (projectedKeyByUpstream.ContainsKey(change.Key))
                                {
                                    // Emit remove for previous projected key.
                                    var prevVal = hadPrevious ? change.Previous.Value : change.Current;
                                    result.Add(new Change<TObject, TNewKey>(ChangeReason.Remove, oldProjected, prevVal, prevVal));
                                }

                                projectedKeyByUpstream[change.Key] = newKey;
                                result.Add(new Change<TObject, TNewKey>(ChangeReason.Add, newKey, change.Current));
                            }

                            break;
                        }

                        case ChangeReason.Remove:
                        {
                            if (projectedKeyByUpstream.TryGetValue(change.Key, out var proj))
                            {
                                var prevVal = change.Previous.HasValue ? change.Previous.Value : change.Current;
                                result.Add(new Change<TObject, TNewKey>(ChangeReason.Remove, proj, prevVal, prevVal));
                                projectedKeyByUpstream.Remove(change.Key);
                            }

                            // else ignore remove for unknown upstream key.
                            break;
                        }

                        case ChangeReason.Refresh:
                        {
                            // Refresh does not change value but we still re-emit with projected key.
                            if (projectedKeyByUpstream.TryGetValue(change.Key, out var proj))
                            {
                                result.Add(new Change<TObject, TNewKey>(ChangeReason.Refresh, proj, change.Current));
                            }
                            else
                            {
                                // Treat refresh for unseen upstream key as add to maintain consistency.
                                var newKey = _keySelector(change.Current);
                                projectedKeyByUpstream[change.Key] = newKey;
                                result.Add(new Change<TObject, TNewKey>(ChangeReason.Add, newKey, change.Current));
                            }

                            break;
                        }

                        case ChangeReason.Moved:
                        {
                            // Cache move not meaningful in projected key space; suppress.
                            break;
                        }
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
}
