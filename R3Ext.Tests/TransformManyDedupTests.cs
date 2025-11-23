using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;
using R3.DynamicData.List;
using Xunit;

namespace R3Ext.Tests;

public class TransformManyDedupTests
{
    private sealed class Item
    {
        public int Id { get; set; }

        public List<int> Values { get; set; } = new();
    }

    [Fact]
    public async Task Dedup_AddsAndRemovesReferenceCounted()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var changesReceived = new List<IChangeSet<int>>();

        using var sub = cache
            .Connect()
            .TransformMany(i => i.Values, EqualityComparer<int>.Default)
            .Subscribe(cs => changesReceived.Add(cs));

        cache.AddOrUpdate(new Item { Id = 1, Values = new List<int> { 1, 2 } });
        await Task.Delay(10);
        Assert.Equal(1, changesReceived.Count);
        Assert.Equal(new[] { 1, 2 }, changesReceived[0].Select(c => c.Current).ToArray());
        Assert.All(changesReceived[0], c => Assert.Equal(ListChangeReason.Add, c.Reason));

        cache.AddOrUpdate(new Item { Id = 2, Values = new List<int> { 1, 2, 3 } });
        await Task.Delay(10);
        Assert.Equal(2, changesReceived.Count); // Only new value 3 added
        Assert.Single(changesReceived[1]);
        Assert.Equal(3, changesReceived[1].First().Current);
        Assert.Equal(ListChangeReason.Add, changesReceived[1].First().Reason);

        cache.Remove(1); // Decrement counts for 1 and 2, but not removed yet
        await Task.Delay(10);
        Assert.Equal(2, changesReceived.Count); // No new change set emitted

        cache.Remove(2); // Final removal of 1,2,3
        await Task.Delay(10);
        Assert.Equal(3, changesReceived.Count);
        var removalSet = changesReceived[2];
        Assert.Equal(3, removalSet.Count);
        Assert.True(removalSet.All(c => c.Reason == ListChangeReason.Remove));
        Assert.Equal(new[] { 1, 2, 3 }, removalSet.Select(c => c.Current).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task Dedup_UpdateAddsNewValue()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var changesReceived = new List<IChangeSet<int>>();

        using var sub = cache
            .Connect()
            .TransformMany(i => i.Values, EqualityComparer<int>.Default)
            .Subscribe(cs => changesReceived.Add(cs));

        cache.AddOrUpdate(new Item { Id = 1, Values = new List<int> { 1, 2 } });
        cache.AddOrUpdate(new Item { Id = 2, Values = new List<int> { 1, 2, 3 } });
        await Task.Delay(20);
        Assert.Equal(2, changesReceived.Count); // Adds for [1,2] then [3]

        cache.AddOrUpdate(new Item { Id = 1, Values = new List<int> { 2, 4 } }); // Update introducing 4
        await Task.Delay(20);
        Assert.Equal(3, changesReceived.Count);
        var addSet = changesReceived[2];
        Assert.Single(addSet);
        Assert.Equal(ListChangeReason.Add, addSet.First().Reason);
        Assert.Equal(4, addSet.First().Current);
    }

    [Fact]
    public async Task Dedup_UpdateRemovesLastOccurrence()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var changesReceived = new List<IChangeSet<int>>();

        using var sub = cache
            .Connect()
            .TransformMany(i => i.Values, EqualityComparer<int>.Default)
            .Subscribe(cs => changesReceived.Add(cs));

        // Setup so that value 1 only exists in item with Id=2, ensuring update removes last occurrence.
        cache.AddOrUpdate(new Item { Id = 1, Values = new List<int> { 2 } });
        cache.AddOrUpdate(new Item { Id = 2, Values = new List<int> { 1, 3 } });
        await Task.Delay(30);
        Assert.Equal(2, changesReceived.Count); // First adds 2, second adds 1 and 3
        Assert.Single(changesReceived[0]);
        Assert.Equal(new[] { 1, 3 }, changesReceived[1].Select(c => c.Current).OrderBy(x => x).ToArray());

        cache.AddOrUpdate(new Item { Id = 2, Values = new List<int> { 3 } }); // Remove last occurrence of 1
        await Task.Delay(30);
        Assert.Equal(3, changesReceived.Count);
        var removalSet = changesReceived[2];
        Assert.Single(removalSet);
        Assert.Equal(ListChangeReason.Remove, removalSet.First().Reason);
        Assert.Equal(1, removalSet.First().Current);
    }
}
