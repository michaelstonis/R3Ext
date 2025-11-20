
// Port of DynamicData to R3.

using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public class DynamicFilterOperatorTests
{
    [Fact]
    public void DynamicFilter_PredicateAddsItems()
    {
        var source = new SourceList<int>();
        var predicate = new Subject<Func<int, bool>>();
        var results = new List<IChangeSet<int>>();
        using var sub = source.Connect().Filter(predicate).Subscribe(results.Add);

        predicate.OnNext(x => x > 5); // initial predicate
        source.AddRange(new[] { 2, 4, 6, 8 }); // adds 6,8
        results.Clear();

        predicate.OnNext(x => x > 3); // now 4 qualifies (and 2 still no)

        Assert.Single(results); // only additions 4
        Assert.Equal(1, results[0].Adds);
        Assert.Equal(4, results[0].First().Item);
    }

    [Fact]
    public void DynamicFilter_PredicateRemovesItems()
    {
        var source = new SourceList<int>();
        var predicate = new Subject<Func<int, bool>>();
        var results = new List<IChangeSet<int>>();
        using var sub = source.Connect().Filter(predicate).Subscribe(results.Add);

        predicate.OnNext(x => x > 3);
        source.AddRange(new[] { 2, 4, 6 }); // adds 4,6
        results.Clear();

        predicate.OnNext(x => x > 5); // removes 4, retains 6

        Assert.Single(results);
        Assert.Equal(1, results[0].Removes);
        Assert.Equal(4, results[0].First().Item);
    }

    [Fact]
    public void DynamicFilter_PredicateAddsAndRemovesInSameChange()
    {
        var source = new SourceList<int>();
        var predicate = new Subject<Func<int, bool>>();
        var results = new List<IChangeSet<int>>();
        using var sub = source.Connect().Filter(predicate).Subscribe(results.Add);

        predicate.OnNext(x => x % 2 == 0); // even
        source.AddRange(new[] { 1, 2, 3, 4 }); // adds 2,4
        results.Clear();

        predicate.OnNext(x => x > 2); // removes 2, adds 3

        Assert.Single(results); // combined remove + add
        Assert.Equal(1, results[0].Removes);
        Assert.Equal(1, results[0].Adds);
        var items = results[0].Select(c => c.Item).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 2, 3 }, items); // removed 2, added 3
    }
}
