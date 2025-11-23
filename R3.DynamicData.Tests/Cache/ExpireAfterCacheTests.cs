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
        var cache = new SourceCache<Item, int>(i => i.Id);
        var removed = new List<int>();
        var adds = 0;

        using var sub = cache.Connect()
            .ExpireAfter<Item, int>(i => i.Name == "fast" ? TimeSpan.FromMilliseconds(50) : TimeSpan.FromMilliseconds(120))
            .Subscribe(changes =>
            {
                foreach (var c in changes)
                {
                    if (c.Reason == Kernel.ChangeReason.Add) adds++;
                    if (c.Reason == Kernel.ChangeReason.Remove) removed.Add(c.Key);
                }
            });

        cache.AddOrUpdate(new Item(1, "fast"));
        cache.AddOrUpdate(new Item(2, "slow"));

        await Task.Delay(80); // fast should expire
        Assert.Contains(1, removed);
        Assert.DoesNotContain(2, removed);

        await Task.Delay(70); // slow should now expire
        Assert.Contains(2, removed);
    }

    [Fact]
    public async Task ExpireAfter_UpdateResetsTimer()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var removed = new List<int>();

        using var sub = cache.Connect()
            .ExpireAfter<Item, int>(i => i.Name == "expire" ? TimeSpan.FromMilliseconds(200) : TimeSpan.FromMilliseconds(600))
            .Subscribe(changes =>
            {
                foreach (var c in changes)
                {
                    if (c.Reason == Kernel.ChangeReason.Remove) removed.Add(c.Key);
                }
            });

        cache.AddOrUpdate(new Item(1, "expire"));
        await Task.Delay(100); // Advance to 100ms, well before 200ms expiry
        // Update to extended expiry before original expiry fires
        cache.AddOrUpdate(new Item(1, "extended"));
        await Task.Delay(200); // Advance to 300ms total, past original 200ms expiry but within 600ms from update (at 100ms)
        Assert.DoesNotContain(1, removed);
        await Task.Delay(400); // Advance to 700ms total, past 600ms from update (100ms + 600ms)
        Assert.Contains(1, removed);
    }

    [Fact]
    public async Task ExpireAfter_NullSkipsExpiration()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var removed = false;

        using var sub = cache.Connect()
            .ExpireAfter<Item, int>(i => i.Name == "keep" ? null : TimeSpan.FromMilliseconds(30))
            .Subscribe(changes =>
            {
                if (changes.Any(c => c.Reason == Kernel.ChangeReason.Remove)) removed = true;
            });

        cache.AddOrUpdate(new Item(1, "keep"));
        await Task.Delay(80);
        Assert.False(removed);
    }
}
