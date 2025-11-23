using System.Collections.Generic;
using System.Threading.Tasks;
using R3;
using R3.DynamicData.Cache;
#pragma warning disable SA1516, SA1503, SA1513, SA1107, SA1502, SA1515

namespace R3Ext.Tests;

public class TransformAsyncCacheTests
{
    private sealed record Person(int Id, string Name);

    [Fact]
    public async Task TransformAsync_BasicTransformation()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var results = new List<string>();

        using var sub = cache.Connect()
            .TransformAsync(async p =>
            {
                await Task.Delay(10);
                return p.Name.ToUpper();
            })
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == R3.DynamicData.Kernel.ChangeReason.Add)
                    {
                        results.Add(change.Current);
                    }
                }
            });

        cache.AddOrUpdate(new Person(1, "Alice"));
        cache.AddOrUpdate(new Person(2, "Bob"));

        await Task.Delay(100);

        Assert.Equal(2, results.Count);
        Assert.Contains("ALICE", results);
        Assert.Contains("BOB", results);
    }

    [Fact]
    public async Task TransformAsync_WithCancellation()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var transformStarted = new List<int>();
        var transformCompleted = new List<int>();
        var results = new List<string>();

        using var sub = cache.Connect()
            .TransformAsync(async (p, ct) =>
            {
                transformStarted.Add(p.Id);
                await Task.Delay(50, ct);
                transformCompleted.Add(p.Id);
                return p.Name.ToUpper();
            })
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == R3.DynamicData.Kernel.ChangeReason.Add)
                    {
                        results.Add(change.Current);
                    }
                }
            });

        cache.AddOrUpdate(new Person(1, "Alice"));
        await Task.Delay(10);

        // Remove before transformation completes
        cache.Remove(1);

        await Task.Delay(100);

        Assert.Single(transformStarted);
        Assert.Empty(transformCompleted); // Should be cancelled
        Assert.Empty(results); // Nothing should be emitted
    }

    [Fact]
    public async Task TransformAsync_Update()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var results = new List<R3.DynamicData.Cache.IChangeSet<string, int>>();

        using var sub = cache.Connect()
            .TransformAsync(async p =>
            {
                await Task.Delay(10);
                return p.Name.ToUpper();
            })
            .Subscribe(results.Add);

        cache.AddOrUpdate(new Person(1, "Alice"));
        await Task.Delay(50);

        Assert.Single(results);
        Assert.Equal(R3.DynamicData.Kernel.ChangeReason.Add, results[0].First().Reason);

        // Update the value
        cache.AddOrUpdate(new Person(1, "Alicia"));
        await Task.Delay(50);

        Assert.Equal(2, results.Count);
        Assert.Equal(R3.DynamicData.Kernel.ChangeReason.Update, results[1].First().Reason);
        Assert.Equal("ALICIA", results[1].First().Current);
    }
}
