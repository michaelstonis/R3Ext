// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

using R3;

namespace R3.DynamicData.List.Internal;

internal sealed class AutoRefresh<TObject, TAny>
    where TObject : notnull
{
    private readonly Observable<IChangeSet<TObject>> _source;
    private readonly Func<TObject, Observable<TAny>> _reEvaluator;
    private readonly TimeSpan? _buffer;
    private readonly TimeProvider? _timeProvider;

    public AutoRefresh(Observable<IChangeSet<TObject>> source, Func<TObject, Observable<TAny>> reEvaluator, TimeSpan? buffer = null, TimeProvider? timeProvider = null)
    {
        _source = source;
        _reEvaluator = reEvaluator;
        _buffer = buffer;
        _timeProvider = timeProvider;
    }

    public Observable<IChangeSet<TObject>> Run()
    {
        var merged = _source
            .Select(changeSet =>
            {
                var itemObservables = changeSet
                    .Where(change => change.Reason == ListChangeReason.Add || change.Reason == ListChangeReason.Replace)
                    .Select(change => _reEvaluator(change.Current));

                return itemObservables.Merge();
            })
            .Merge();

        return ApplyBufferIfNeeded(merged
            .Select(_ =>
            {
                var refreshChange = Change<TObject>.Refresh;
                return (IChangeSet<TObject>)new ChangeSet<TObject>(new[] { refreshChange });
            }));
    }

    private Observable<IChangeSet<TObject>> ApplyBufferIfNeeded(Observable<IChangeSet<TObject>> source)
    {
        if (_buffer.HasValue)
        {
            var timeProvider = _timeProvider ?? ObservableSystem.DefaultTimeProvider;
            return source.Debounce(_buffer.Value, timeProvider);
        }

        return source;
    }
}
