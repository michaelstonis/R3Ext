// Port of DynamicData to R3.

using R3.DynamicData.Cache;

namespace R3.DynamicData.Tests.Cache;

public class FilterOnObservableCacheTests
{
    private sealed class Item
    {
        public int Id { get; }
        public Subject<bool> Active { get; } = new Subject<bool>();
        public Item(int id) => Id = id;
    }

    [Fact]
    public void FilterOnObservable_AddAndToggle()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var results = new List<IChangeSet<Item, int>>();

        using var sub = cache.Connect()
            .FilterOnObservable<Item, int>(i => i.Active)
            .Subscribe(results.Add);

        var a = new Item(1);
        cache.AddOrUpdate(a);
        a.Active.OnNext(true); // add event
        a.Active.OnNext(false); // remove event
        a.Active.OnNext(true); // re-add

        Assert.True(results.Count >= 3);
        Assert.Contains(results, cs => cs.Any(c => c.Reason == Kernel.ChangeReason.Add));
        Assert.Contains(results, cs => cs.Any(c => c.Reason == Kernel.ChangeReason.Remove));
    }
}
