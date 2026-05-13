// Port of DynamicData to R3.
#pragma warning disable SA1516, SA1515, SA1503, SA1502, SA1107, SA1513, SA1518

using R3;
using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public class FilterWithStateTests
{
    [Fact]
    public void FilterWithState_InitialState_FiltersCorrectly()
    {
        var list = new SourceList<int>();
        var stateSubject = new Subject<int>();
        var results = new List<IChangeSet<int>>();

        using var sub = list.Connect()
            .Filter(stateSubject, (item, minValue) => item >= minValue)
            .Subscribe(results.Add);

        stateSubject.OnNext(5);
        list.AddRange(new[] { 1, 3, 5, 7, 10 });

        int adds = results.Sum(cs => cs.Adds);
        Assert.Equal(3, adds); // 5, 7, 10
    }

    [Fact]
    public void FilterWithState_StateChange_ReEvaluatesExistingItems()
    {
        var list = new SourceList<int>();
        var stateSubject = new Subject<int>();
        var results = new List<IChangeSet<int>>();

        using var sub = list.Connect()
            .Filter(stateSubject, (item, minValue) => item >= minValue)
            .Subscribe(results.Add);

        stateSubject.OnNext(5);
        list.AddRange(new[] { 3, 5, 8 }); // 5 and 8 pass

        results.Clear();
        stateSubject.OnNext(8); // only 8 passes now

        int removes = results.Sum(cs => cs.Removes);
        Assert.Equal(1, removes); // 5 removed
    }

    [Fact]
    public void FilterWithState_StateChange_AddsItemsThatNowPass()
    {
        var list = new SourceList<int>();
        var stateSubject = new Subject<int>();
        var results = new List<IChangeSet<int>>();

        using var sub = list.Connect()
            .Filter(stateSubject, (item, minValue) => item >= minValue)
            .Subscribe(results.Add);

        stateSubject.OnNext(10);
        list.AddRange(new[] { 3, 7, 15 }); // only 15 passes

        results.Clear();
        stateSubject.OnNext(3); // all pass now

        int adds = results.Sum(cs => cs.Adds);
        Assert.Equal(2, adds); // 3 and 7 newly added
    }

    [Fact]
    public void FilterWithState_NewItemsAdded_FilteredByCurrentState()
    {
        var list = new SourceList<int>();
        var stateSubject = new Subject<int>();
        var results = new List<IChangeSet<int>>();

        using var sub = list.Connect()
            .Filter(stateSubject, (item, minValue) => item >= minValue)
            .Subscribe(results.Add);

        stateSubject.OnNext(10);
        list.AddRange(new[] { 1, 2, 15 });

        results.Clear();
        list.AddRange(new[] { 5, 20 });

        int adds = results.Sum(cs => cs.Adds);
        Assert.Equal(1, adds); // only 20 passes threshold of 10
    }

    [Fact]
    public void FilterWithState_NoStateEmitted_NoItemsPass()
    {
        var list = new SourceList<int>();
        var stateSubject = new Subject<int>();
        var results = new List<IChangeSet<int>>();

        using var sub = list.Connect()
            .Filter(stateSubject, (item, minValue) => item >= minValue)
            .Subscribe(results.Add);

        list.AddRange(new[] { 1, 5, 10 });

        Assert.Equal(0, results.Sum(cs => cs.Adds));
    }
}
