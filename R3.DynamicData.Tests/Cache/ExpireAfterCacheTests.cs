// Port of DynamicData to R3.
#pragma warning disable SA1516, SA1515, SA1503, SA1502, SA1107

using Microsoft.Extensions.Time.Testing;
using R3.DynamicData.Cache;

namespace R3.DynamicData.Tests.Cache;

public class ExpireAfterCacheTests
{
    private sealed class Item
    {
        public int Id { get; }
        public string Name { get; set; }
        public Item(int id, string name) { Id = id; Name = name; }
    }

    [Fact]
    public async Task ExpireAfter_RemovesItemsAfterDuration()
    {
        var fakeTimeProvider = new FakeTimeProvider();
        var cache = new SourceCache<Item, int>(i => i.Id);
        var removed = new List<int>();
        var adds = 0;

        using var sub = cache.Connect()
            .ExpireAfter<Item, int>(
                i => i.Name == "fast" ? TimeSpan.FromMilliseconds(50) : TimeSpan.FromMilliseconds(120),
                fakeTimeProvider)
            .Subscribe(changes =>
            {
                foreach (var c in changes)
                {
                    if (c.Reason == Kernel.ChangeReason.Add)
                    {
                        adds++;
                    }

                    if (c.Reason == Kernel.ChangeReason.Remove)
                    {
                        removed.Add(c.Key);
                    }
                }
            });

        cache.AddOrUpdate(new Item(1, "fast"));
        cache.AddOrUpdate(new Item(2, "slow"));

        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(51));
        await Task.Delay(10);
        Assert.Contains(1, removed);
        Assert.DoesNotContain(2, removed);

        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(70));
        await Task.Delay(10);
        Assert.Contains(2, removed);
    }

    [Fact]
    public async Task ExpireAfter_UpdateResetsTimer()
    {
        var fakeTimeProvider = new FakeTimeProvider();
        var cache = new SourceCache<Item, int>(i => i.Id);
        var removed = new List<int>();

        using var sub = cache.Connect()
            .ExpireAfter<Item, int>(
                i => i.Name == "expire" ? TimeSpan.FromMilliseconds(400) : TimeSpan.FromMilliseconds(1000),
                fakeTimeProvider)
            .Subscribe(changes =>
            {
                foreach (var c in changes)
                {
                    if (c.Reason == Kernel.ChangeReason.Remove)
                    {
                        removed.Add(c.Key);
                    }
                }
            });

        cache.AddOrUpdate(new Item(1, "expire"));
        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(200));
        await Task.Delay(10);

        cache.AddOrUpdate(new Item(1, "extended"));
        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(400));
        await Task.Delay(10);
        Assert.DoesNotContain(1, removed);

        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(700));
        await Task.Delay(10);
        Assert.Contains(1, removed);
    }

    [Fact]
    public async Task ExpireAfter_NullSkipsExpiration()
    {
        var fakeTimeProvider = new FakeTimeProvider();
        var cache = new SourceCache<Item, int>(i => i.Id);
        var removed = false;

        using var sub = cache.Connect()
            .ExpireAfter<Item, int>(
                i => i.Name == "keep" ? null : TimeSpan.FromMilliseconds(30),
                fakeTimeProvider)
            .Subscribe(changes =>
            {
                if (changes.Any(c => c.Reason == Kernel.ChangeReason.Remove))
                {
                    removed = true;
                }
            });

        cache.AddOrUpdate(new Item(1, "keep"));
        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(80));
        await Task.Delay(10);
        Assert.False(removed);
    }
}
