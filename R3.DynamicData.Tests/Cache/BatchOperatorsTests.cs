using System.Collections.Generic;
using Microsoft.Extensions.Time.Testing;
using R3.DynamicData.Cache;

namespace R3.DynamicData.Tests.Cache;

public sealed class BatchOperatorsTests
{
    [Fact]
    public void Batch_EmitsBatchedChangesAfterTimeSpan()
    {
        var cache = new SourceCache<int, int>(x => x);
        var results = new List<IChangeSet<int, int>>();
        var timeProvider = new FakeTimeProvider();

        using var sub = cache.Connect()
            .Batch(TimeSpan.FromSeconds(1), timeProvider)
            .Subscribe(results.Add);

        // Add multiple items
        cache.AddOrUpdate(1);
        cache.AddOrUpdate(2);
        cache.AddOrUpdate(3);

        // No emissions yet
        Assert.Empty(results);

        // Advance time
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        // Should emit batched changes
        Assert.Single(results);
        Assert.Equal(3, results[0].Adds);
    }

    [Fact]
    public void Batch_EmitsMultipleBatches()
    {
        var cache = new SourceCache<int, int>(x => x);
        var results = new List<IChangeSet<int, int>>();
        var timeProvider = new FakeTimeProvider();

        using var sub = cache.Connect()
            .Batch(TimeSpan.FromSeconds(1), timeProvider)
            .Subscribe(results.Add);

        // First batch
        cache.AddOrUpdate(1);
        cache.AddOrUpdate(2);
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        // Second batch
        cache.AddOrUpdate(3);
        cache.AddOrUpdate(4);
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(2, results.Count);
        Assert.Equal(2, results[0].Adds);
        Assert.Equal(2, results[1].Adds);
    }

    [Fact]
    public void Batch_DoesNotEmitEmptyBatch()
    {
        var cache = new SourceCache<int, int>(x => x);
        var results = new List<IChangeSet<int, int>>();
        var timeProvider = new FakeTimeProvider();

        using var sub = cache.Connect()
            .Batch(TimeSpan.FromSeconds(1), timeProvider)
            .Subscribe(results.Add);

        // No changes
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        Assert.Empty(results);
    }

    [Fact]
    public void BatchIf_BuffersWhenPaused()
    {
        var cache = new SourceCache<int, int>(x => x);
        var pauseSignal = new Subject<bool>();
        var results = new List<IChangeSet<int, int>>();

        using var sub = cache.Connect()
            .BatchIf(pauseSignal)
            .Subscribe(results.Add);

        // Pause
        pauseSignal.OnNext(true);

        // Add items while paused
        cache.AddOrUpdate(1);
        cache.AddOrUpdate(2);
        cache.AddOrUpdate(3);

        // No emissions while paused
        Assert.Empty(results);

        // Resume
        pauseSignal.OnNext(false);

        // Should emit batched changes
        Assert.Single(results);
        Assert.Equal(3, results[0].Adds);
    }

    [Fact]
    public void BatchIf_PassesThroughWhenNotPaused()
    {
        var cache = new SourceCache<int, int>(x => x);
        var pauseSignal = new Subject<bool>();
        var results = new List<IChangeSet<int, int>>();

        using var sub = cache.Connect()
            .BatchIf(pauseSignal)
            .Subscribe(results.Add);

        // Not paused initially
        cache.AddOrUpdate(1);
        cache.AddOrUpdate(2);

        // Should emit immediately
        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Adds);
        Assert.Equal(1, results[1].Adds);
    }

    [Fact]
    public void BatchIf_InitialPauseStateBuffers()
    {
        var cache = new SourceCache<int, int>(x => x);
        var pauseSignal = new Subject<bool>();
        var results = new List<IChangeSet<int, int>>();

        using var sub = cache.Connect()
            .BatchIf(pauseSignal, initialPauseState: true)
            .Subscribe(results.Add);

        // Add items (initially paused)
        cache.AddOrUpdate(1);
        cache.AddOrUpdate(2);

        // No emissions
        Assert.Empty(results);

        // Resume
        pauseSignal.OnNext(false);

        // Should emit batched changes
        Assert.Single(results);
        Assert.Equal(2, results[0].Adds);
    }

    [Fact]
    public void BatchIf_MultiplePauseResumeCycles()
    {
        var cache = new SourceCache<int, int>(x => x);
        var pauseSignal = new Subject<bool>();
        var results = new List<IChangeSet<int, int>>();

        using var sub = cache.Connect()
            .BatchIf(pauseSignal)
            .Subscribe(results.Add);

        // First cycle: pause and buffer
        pauseSignal.OnNext(true);
        cache.AddOrUpdate(1);
        cache.AddOrUpdate(2);
        pauseSignal.OnNext(false);

        // Second cycle: pause and buffer
        pauseSignal.OnNext(true);
        cache.AddOrUpdate(3);
        cache.AddOrUpdate(4);
        pauseSignal.OnNext(false);

        Assert.Equal(2, results.Count);
        Assert.Equal(2, results[0].Adds);
        Assert.Equal(2, results[1].Adds);
    }

    [Fact]
    public void BatchIf_EmptyBufferDoesNotEmit()
    {
        var cache = new SourceCache<int, int>(x => x);
        var pauseSignal = new Subject<bool>();
        var results = new List<IChangeSet<int, int>>();

        using var sub = cache.Connect()
            .BatchIf(pauseSignal)
            .Subscribe(results.Add);

        // Pause and resume without changes
        pauseSignal.OnNext(true);
        pauseSignal.OnNext(false);

        Assert.Empty(results);
    }
}
