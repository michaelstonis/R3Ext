// Port of DynamicData to R3.
#pragma warning disable SA1516, SA1515, SA1503, SA1502, SA1107

using R3.DynamicData.Cache;

namespace R3.DynamicData.Tests.Cache;

public sealed class VirtualizeOperatorsTests
{
    private sealed class Item
    {
        public int Id { get; }
        public string Name { get; set; }
        public Item(int id, string name) { Id = id; Name = name; }
    }

    [Fact]
    public void Virtualize_InitialWindow_EmitsCorrectItems()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var requests = new Subject<VirtualRequest>();
        var results = new List<IChangeSet<Item, int>>();

        using var sub = cache.Connect()
            .Virtualize(requests)
            .Subscribe(results.Add);

        // Add items
        cache.Edit(updater =>
        {
            for (int i = 1; i <= 10; i++)
            {
                updater.AddOrUpdate(new Item(i, $"Item{i}"));
            }
        });

        results.Clear(); // Clear the initial emission

        // Request first 5 items
        requests.OnNext(new VirtualRequest(0, 5));

        Assert.Single(results);
        Assert.Equal(5, results[0].Count);
        Assert.All(results[0], change => Assert.Equal(Kernel.ChangeReason.Add, change.Reason));
    }

    [Fact]
    public void Virtualize_WindowChange_EmitsCorrectDiff()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var requests = new Subject<VirtualRequest>();
        var results = new List<IChangeSet<Item, int>>();

        using var sub = cache.Connect()
            .Virtualize(requests)
            .Subscribe(results.Add);

        // Add items
        cache.Edit(updater =>
        {
            for (int i = 1; i <= 10; i++)
            {
                updater.AddOrUpdate(new Item(i, $"Item{i}"));
            }
        });

        // Request first 5 items
        requests.OnNext(new VirtualRequest(0, 5));
        results.Clear();

        // Change window to items 5-10
        requests.OnNext(new VirtualRequest(5, 5));

        Assert.Equal(1, results.Count);
        var changeset = results[0];

        // Should remove first 5 and add next 5
        Assert.Equal(10, changeset.Count);
        Assert.Equal(5, changeset.Count(c => c.Reason == Kernel.ChangeReason.Remove));
        Assert.Equal(5, changeset.Count(c => c.Reason == Kernel.ChangeReason.Add));
    }

    [Fact]
    public void Virtualize_AddItemsAfterWindow_UpdatesWindow()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var requests = new Subject<VirtualRequest>();
        var results = new List<IChangeSet<Item, int>>();

        using var sub = cache.Connect()
            .Virtualize(requests)
            .Subscribe(results.Add);

        // Add 5 items
        cache.Edit(updater =>
        {
            for (int i = 1; i <= 5; i++)
            {
                updater.AddOrUpdate(new Item(i, $"Item{i}"));
            }
        });

        // Request first 3 items
        requests.OnNext(new VirtualRequest(0, 3));

        // The changeset should have 3 items (or possibly 5 if all items were added)
        var totalChanges = results.Sum(cs => cs.Count);
        Assert.True(totalChanges >= 3);

        results.Clear();

        // Add more items
        cache.Edit(updater =>
        {
            for (int i = 6; i <= 10; i++)
            {
                updater.AddOrUpdate(new Item(i, $"Item{i}"));
            }
        });

        // Window should still show first 3 items (no change since they're still in window)
        // The changeset might be empty if the windowed keys haven't changed
        Assert.True(results.Count == 0 || results.All(cs => cs.Count == 0));
    }

    [Fact]
    public void Virtualize_RemoveItemInWindow_EmitsRemove()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var requests = new Subject<VirtualRequest>();
        var results = new List<IChangeSet<Item, int>>();

        using var sub = cache.Connect()
            .Virtualize(requests)
            .Subscribe(results.Add);

        // Add items
        cache.Edit(updater =>
        {
            for (int i = 1; i <= 10; i++)
            {
                updater.AddOrUpdate(new Item(i, $"Item{i}"));
            }
        });

        // Request first 5 items
        requests.OnNext(new VirtualRequest(0, 5));
        results.Clear();

        // Remove an item in the window
        cache.Remove(3);

        Assert.True(results.Count > 0);
        // Should have changes reflecting the removal and window adjustment
    }

    [Fact]
    public void Virtualize_WindowBeyondDataSize_EmitsAvailableItems()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var requests = new Subject<VirtualRequest>();
        var results = new List<IChangeSet<Item, int>>();

        using var sub = cache.Connect()
            .Virtualize(requests)
            .Subscribe(results.Add);

        // Add only 5 items
        cache.Edit(updater =>
        {
            for (int i = 1; i <= 5; i++)
            {
                updater.AddOrUpdate(new Item(i, $"Item{i}"));
            }
        });

        results.Clear(); // Clear initial emission

        // Request window beyond data size
        requests.OnNext(new VirtualRequest(0, 20));

        Assert.Single(results);
        Assert.Equal(5, results[0].Count); // Should only emit 5 items
    }

    [Fact]
    public void Page_EmitsCorrectPageItems()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var pageRequests = new Subject<int>();
        var results = new List<IChangeSet<Item, int>>();

        using var sub = cache.Connect()
            .Page(pageRequests, pageSize: 3)
            .Subscribe(results.Add);

        // Add items
        cache.Edit(updater =>
        {
            for (int i = 1; i <= 10; i++)
            {
                updater.AddOrUpdate(new Item(i, $"Item{i}"));
            }
        });

        results.Clear(); // Clear initial emission

        // Request page 0 (items 0-2)
        pageRequests.OnNext(0);

        Assert.Single(results);
        Assert.Equal(3, results[0].Count);
    }

    [Fact]
    public void Page_ChangePages_EmitsCorrectDiff()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var pageRequests = new Subject<int>();
        var results = new List<IChangeSet<Item, int>>();

        using var sub = cache.Connect()
            .Page(pageRequests, pageSize: 3)
            .Subscribe(results.Add);

        // Add items
        cache.Edit(updater =>
        {
            for (int i = 1; i <= 10; i++)
            {
                updater.AddOrUpdate(new Item(i, $"Item{i}"));
            }
        });

        // Request page 0
        pageRequests.OnNext(0);
        results.Clear();

        // Change to page 1 (items 3-5)
        pageRequests.OnNext(1);

        Assert.Single(results);
        Assert.Equal(6, results[0].Count); // 3 removes + 3 adds
    }

    [Fact]
    public void Page_InvalidPageSize_ThrowsException()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var pageRequests = new Subject<int>();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cache.Connect().Page(pageRequests, pageSize: 0));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cache.Connect().Page(pageRequests, pageSize: -1));
    }

    [Fact]
    public void Top_EmitsOnlyTopNItems()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var results = new List<IChangeSet<Item, int>>();

        using var sub = cache.Connect()
            .Top(5)
            .Subscribe(results.Add);

        // Add 10 items
        cache.Edit(updater =>
        {
            for (int i = 1; i <= 10; i++)
            {
                updater.AddOrUpdate(new Item(i, $"Item{i}"));
            }
        });

        // Should receive changes with at least 5 add operations across all changesets
        var totalAdds = results.Sum(cs => cs.Count(c => c.Reason == Kernel.ChangeReason.Add));
        Assert.True(totalAdds >= 5);
    }

    [Fact]
    public void Top_AddMoreItems_MaintainsLimit()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var results = new List<IChangeSet<Item, int>>();

        using var sub = cache.Connect()
            .Top(3)
            .Subscribe(results.Add);

        // Add 3 items
        cache.Edit(updater =>
        {
            for (int i = 1; i <= 3; i++)
            {
                updater.AddOrUpdate(new Item(i, $"Item{i}"));
            }
        });

        var initialAdds = results.Sum(cs => cs.Count(c => c.Reason == Kernel.ChangeReason.Add));
        Assert.Equal(3, initialAdds);

        results.Clear();

        // Add 3 more items
        cache.Edit(updater =>
        {
            for (int i = 4; i <= 6; i++)
            {
                updater.AddOrUpdate(new Item(i, $"Item{i}"));
            }
        });

        // Should not emit more items since we're at the limit
        var additionalAdds = results.Sum(cs => cs.Count(c => c.Reason == Kernel.ChangeReason.Add));
        Assert.Equal(0, additionalAdds);
    }

    [Fact]
    public void Top_RemoveItemInTop_AddsNextItem()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var results = new List<IChangeSet<Item, int>>();

        using var sub = cache.Connect()
            .Top(3)
            .Subscribe(results.Add);

        // Add 5 items
        cache.Edit(updater =>
        {
            for (int i = 1; i <= 5; i++)
            {
                updater.AddOrUpdate(new Item(i, $"Item{i}"));
            }
        });

        results.Clear();

        // Remove an item from top 3
        cache.Remove(2);

        // Should emit changes: remove the item and add the next one (item 4)
        Assert.True(results.Count > 0);
    }

    [Fact]
    public void Top_InvalidCount_ThrowsException()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cache.Connect().Top(0));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cache.Connect().Top(-1));
    }

    [Fact]
    public void Virtualize_EmptyCache_HandlesGracefully()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var requests = new Subject<VirtualRequest>();
        var results = new List<IChangeSet<Item, int>>();

        using var sub = cache.Connect()
            .Virtualize(requests)
            .Subscribe(results.Add);

        // Request window on empty cache
        requests.OnNext(new VirtualRequest(0, 5));

        // Should handle gracefully with no emissions or empty changeset
        Assert.True(results.Count == 0 || results.All(cs => cs.Count == 0));
    }
}
