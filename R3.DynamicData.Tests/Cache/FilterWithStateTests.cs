// Port of DynamicData to R3.
#pragma warning disable SA1516, SA1515, SA1503, SA1502, SA1107, SA1513, SA1518

using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;

namespace R3.DynamicData.Tests.Cache;

public class FilterWithStateTests
{
    [Fact]
    public void FilterWithState_InitialState_FiltersCorrectly()
    {
        var cache = new SourceCache<int, int>(x => x);
        var stateSubject = new Subject<int>();
        var results = new List<IChangeSet<int, int>>();

        using var sub = cache.Connect()
            .Filter(stateSubject, (item, minValue) => item >= minValue)
            .Subscribe(results.Add);

        // Emit initial state threshold = 5
        stateSubject.OnNext(5);
        cache.AddOrUpdate(new[] { 1, 3, 5, 7, 10 });

        // Only items >= 5 should be included
        int adds = results.Sum(cs => cs.Adds);
        Assert.Equal(3, adds); // 5, 7, 10
    }

    [Fact]
    public void FilterWithState_StateChange_ReEvaluatesItems()
    {
        var cache = new SourceCache<int, int>(x => x);
        var stateSubject = new Subject<int>();
        var results = new List<IChangeSet<int, int>>();

        using var sub = cache.Connect()
            .Filter(stateSubject, (item, minValue) => item >= minValue)
            .Subscribe(results.Add);

        stateSubject.OnNext(5);
        cache.AddOrUpdate(new[] { 3, 5, 8 });
        // At this point: 5 and 8 included (2 adds)

        results.Clear();
        stateSubject.OnNext(8); // now only 8 qualifies: should remove 5

        Assert.True(results.Count > 0);
        int removes = results.Sum(cs => cs.Removes);
        Assert.Equal(1, removes); // 5 removed
    }

    [Fact]
    public void FilterWithState_StateChange_AddsItemsThatNowPass()
    {
        var cache = new SourceCache<int, int>(x => x);
        var stateSubject = new Subject<int>();
        var results = new List<IChangeSet<int, int>>();

        using var sub = cache.Connect()
            .Filter(stateSubject, (item, minValue) => item >= minValue)
            .Subscribe(results.Add);

        stateSubject.OnNext(10);
        cache.AddOrUpdate(new[] { 3, 7, 15 });
        // Only 15 passes

        results.Clear();
        stateSubject.OnNext(3); // now 3, 7, 15 all pass

        int adds = results.Sum(cs => cs.Adds);
        Assert.Equal(2, adds); // 3 and 7 newly added
    }

    [Fact]
    public void FilterWithState_NewItemsAfterStateChange_FilteredByCurrentState()
    {
        var cache = new SourceCache<int, int>(x => x);
        var stateSubject = new Subject<int>();
        var results = new List<IChangeSet<int, int>>();

        using var sub = cache.Connect()
            .Filter(stateSubject, (item, minValue) => item >= minValue)
            .Subscribe(results.Add);

        stateSubject.OnNext(10);
        cache.AddOrUpdate(new[] { 1, 2, 15 });

        results.Clear();
        cache.AddOrUpdate(new[] { 5, 20 });

        int adds = results.Sum(cs => cs.Adds);
        Assert.Equal(1, adds); // only 20 passes the threshold of 10
    }

    [Fact]
    public void FilterWithState_NoState_ItemsNotEmitted()
    {
        var cache = new SourceCache<int, int>(x => x);
        var stateSubject = new Subject<int>();
        var results = new List<IChangeSet<int, int>>();

        using var sub = cache.Connect()
            .Filter(stateSubject, (item, minValue) => item >= minValue)
            .Subscribe(results.Add);

        // Add items before any state is emitted
        cache.AddOrUpdate(new[] { 1, 2, 3 });

        // Nothing should pass since no state has been set
        Assert.Equal(0, results.Sum(cs => cs.Adds));
    }
}
