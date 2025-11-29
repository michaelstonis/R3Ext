using System.Collections.Generic;
using System.Threading.Tasks;
using R3;
using R3.DynamicData.Cache;
#pragma warning disable SA1516, SA1503, SA1513, SA1107, SA1502, SA1515, SA1508

namespace R3Ext.Tests;

public class TransformAsyncCacheTests
{
    private sealed record Person(int Id, string Name);

    [Fact]
    public async Task TransformAsync_BasicTransformation()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var results = new List<string>();
        var completionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Per-item TCS for controlled completion
        var itemTcs = new Dictionary<int, TaskCompletionSource<bool>>
        {
            [1] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            [2] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
        };

        using var sub = cache.Connect()
            .TransformAsync(async p =>
            {
                await itemTcs[p.Id].Task.WaitAsync(TimeSpan.FromSeconds(5));
                return p.Name.ToUpper();
            })
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == R3.DynamicData.Kernel.ChangeReason.Add)
                    {
                        results.Add(change.Current);
                        if (results.Count == 2) completionTcs.TrySetResult(true);
                    }
                }
            });

        cache.AddOrUpdate(new Person(1, "Alice"));
        cache.AddOrUpdate(new Person(2, "Bob"));

        // Complete transformations
        itemTcs[1].SetResult(true);
        itemTcs[2].SetResult(true);

        await completionTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

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
        var blockTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = cache.Connect()
            .TransformAsync(async (p, ct) =>
            {
                transformStarted.Add(p.Id);
                startedTcs.TrySetResult(true);
                try
                {
                    await blockTcs.Task.WaitAsync(ct);
                    transformCompleted.Add(p.Id);
                    return p.Name.ToUpper();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
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
        await startedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(transformStarted);
        cache.Remove(1);

        // Give cancellation a moment to propagate
        await Task.Yield();

        Assert.Empty(transformCompleted);
        Assert.Empty(results);
    }

    [Fact]
    public async Task TransformAsync_Update()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var results = new List<R3.DynamicData.Cache.IChangeSet<string, int>>();
        var emitCount = 0;
        var tcs1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcs2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Per-item TCS for controlled completion
        var itemTcs = new Dictionary<int, TaskCompletionSource<bool>>
        {
            [1] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
        };

        using var sub = cache.Connect()
            .TransformAsync(async p =>
            {
                await itemTcs[p.Id].Task.WaitAsync(TimeSpan.FromSeconds(5));
                return p.Name.ToUpper();
            })
            .Subscribe(changeset =>
            {
                results.Add(changeset);
                var count = ++emitCount;
                if (count == 1) tcs1.TrySetResult(true);
                else if (count == 2) tcs2.TrySetResult(true);
            });

        cache.AddOrUpdate(new Person(1, "Alice"));
        itemTcs[1].SetResult(true);
        await tcs1.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(results);
        Assert.Equal(R3.DynamicData.Kernel.ChangeReason.Add, results[0].First().Reason);

        // Update the value - need new TCS for update
        itemTcs[1] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        cache.AddOrUpdate(new Person(1, "Alicia"));
        itemTcs[1].SetResult(true);
        await tcs2.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, results.Count);
        Assert.Equal(R3.DynamicData.Kernel.ChangeReason.Update, results[1].First().Reason);
        Assert.Equal("ALICIA", results[1].First().Current);
    }
}
