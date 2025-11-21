// Port of DynamicData to R3.

using R3;
using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public class DynamicFilterListTests
{
    [Fact]
    public void DynamicFilter_ReevaluatesOnPredicateChange()
    {
        var list = new SourceList<int>();
        var predicateSubject = new Subject<Func<int, bool>>();
        var results = new List<IChangeSet<int>>();

        using var sub = list.Connect().Filter(predicateSubject).Subscribe(results.Add);

        // Emit initial predicate > 5
        predicateSubject.OnNext(x => x > 5);
        list.AddRange(new[] { 1, 6, 7 }); // Should add 6,7 only

        Assert.True(results.Count >= 2); // First emission from predicate, then adds
        var addsAfterItems = results.Last();
        Assert.Equal(2, addsAfterItems.Adds);

        // Change predicate to > 6 (removes 6)
        predicateSubject.OnNext(x => x > 6);
        var removalSet = results.Last();
        Assert.Equal(1, removalSet.Removes);
    }
}
