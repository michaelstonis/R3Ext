// Tests for SuppressRefresh cache operator.
#pragma warning disable SA1516, SA1515, SA1503, SA1502, SA1107
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;

namespace R3.DynamicData.Tests.Cache;

public class SuppressRefreshCacheTests
{
    private sealed record Item(int Id, int Value);

    [Fact]
    public void SuppressRefresh_SuppressesRefreshNotifications()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var results = new List<IChangeSet<Item, int>>();
        using var sub = cache.Connect().SuppressRefresh<Item, int>().Subscribe(results.Add);

        cache.AddOrUpdate(new Item(1, 10)); // Add emitted
        cache.Edit(inner => inner.Refresh(1)); // Refresh suppressed
        cache.AddOrUpdate(new Item(2, 20)); // Add emitted
        cache.Edit(inner => inner.Refresh(2)); // Refresh suppressed
        cache.Remove(1); // Remove emitted

        // Expect 3 change sets: Add(1), Add(2), Remove(1)
        Assert.Equal(3, results.Count);
        Assert.Equal(ChangeReason.Add, results[0].Single().Reason);
        Assert.Equal(ChangeReason.Add, results[1].Single().Reason);
        Assert.Equal(ChangeReason.Remove, results[2].Single().Reason);
    }

    [Fact]
    public void SuppressRefresh_BatchWithAddAndRefresh_OnlyAddEmitted()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var results = new List<IChangeSet<Item, int>>();
        using var sub = cache.Connect().SuppressRefresh<Item, int>().Subscribe(results.Add);

        cache.Edit(inner =>
        {
            inner.AddOrUpdate(new Item(1, 5));
            inner.Refresh(1);
        });

        Assert.Single(results);
        var change = results[0].Single();
        Assert.Equal(ChangeReason.Add, change.Reason);
        Assert.Equal(5, change.Current.Value);
    }
}
