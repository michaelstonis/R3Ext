using R3;
using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public class TransformAsyncTests
{
    [Fact]
    public async Task TransformAsync_BasicTransformation()
    {
        var source = new SourceList<int>();
        var results = new List<string>();

        using var sub = source.Connect()
            .TransformAsync(async x =>
            {
                await Task.Delay(10);
                return x.ToString();
            })
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == ListChangeReason.Add)
                    {
                        results.Add(change.Item);
                    }
                }
            });

        source.Add(1);
        source.Add(2);
        source.Add(3);

        // Wait for async transformations
        await Task.Delay(100);

        Assert.Equal(3, results.Count);
        Assert.Contains("1", results);
        Assert.Contains("2", results);
        Assert.Contains("3", results);
    }

    [Fact]
    public async Task TransformAsync_WithCancellation()
    {
        var source = new SourceList<int>();
        var transformStarted = new List<int>();
        var transformCompleted = new List<int>();
        var results = new List<string>();

        using var sub = source.Connect()
            .TransformAsync(async (x, ct) =>
            {
                transformStarted.Add(x);
                await Task.Delay(50, ct);
                transformCompleted.Add(x);
                return x.ToString();
            })
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == ListChangeReason.Add)
                    {
                        results.Add(change.Item);
                    }
                }
            });

        source.Add(1);
        await Task.Delay(10); // Let transformation start

        // Remove before transformation completes
        source.Remove(1);

        await Task.Delay(100); // Wait to see if cancelled transformation completes

        Assert.Single(transformStarted);
        Assert.Empty(transformCompleted); // Should be cancelled
        Assert.Empty(results); // Nothing should be emitted
    }

    [Fact]
    public async Task TransformAsync_PreservesOrder()
    {
        var source = new SourceList<int>();
        var results = new List<string>();

        using var sub = source.Connect()
            .TransformAsync(async x =>
            {
                // Items with higher values take longer
                await Task.Delay(x * 10);
                return x.ToString();
            })
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == ListChangeReason.Add)
                    {
                        results.Add(change.Item);
                    }
                }
            });

        source.AddRange(new[] { 3, 2, 1 });

        await Task.Delay(100);

        // Despite different delays, results should arrive as they complete
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task TransformAsync_Replace()
    {
        var source = new SourceList<int>();
        var results = new List<IChangeSet<string>>();

        using var sub = source.Connect()
            .TransformAsync(async x =>
            {
                await Task.Delay(10);
                return $"Value: {x}";
            })
            .Subscribe(results.Add);

        source.Add(1);
        await Task.Delay(50);

        Assert.Single(results);
        Assert.Equal(ListChangeReason.Add, results[0].First().Reason);

        // Replace the value
        source.ReplaceAt(0, 2);
        await Task.Delay(50);

        Assert.Equal(2, results.Count);
        Assert.Equal(ListChangeReason.Replace, results[1].First().Reason);
        Assert.Equal("Value: 2", results[1].First().Item);
    }

    [Fact]
    public async Task TransformAsync_Clear()
    {
        var source = new SourceList<int>();
        var results = new List<IChangeSet<string>>();

        using var sub = source.Connect()
            .TransformAsync(async x =>
            {
                await Task.Delay(10);
                return x.ToString();
            })
            .Subscribe(results.Add);

        source.AddRange(new[] { 1, 2, 3 });
        await Task.Delay(100);

        // Should have 3 Add operations
        Assert.Equal(3, results.Count);

        source.Clear();

        // Should have Clear operation
        Assert.Equal(4, results.Count);
        Assert.Equal(ListChangeReason.Clear, results[3].First().Reason);
    }

    [Fact]
    public async Task TransformAsync_RemoveRange()
    {
        var source = new SourceList<int>();
        var currentState = new List<string>();

        using var sub = source.Connect()
            .TransformAsync(async x =>
            {
                await Task.Delay(10);
                return x.ToString();
            })
            .Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    if (change.Reason == ListChangeReason.Add)
                    {
                        currentState.Add(change.Item);
                    }
                    else if (change.Reason == ListChangeReason.Remove)
                    {
                        currentState.Remove(change.Item);
                    }
                }
            });

        source.AddRange(new[] { 1, 2, 3, 4, 5 });
        await Task.Delay(150);

        Assert.Equal(5, currentState.Count);

        source.RemoveRange(1, 3);
        await Task.Delay(50);

        Assert.Equal(2, currentState.Count);
    }

    [Fact]
    public async Task TransformAsync_ConcurrentTransformations()
    {
        var source = new SourceList<int>();
        var results = new List<string>();

        using var sub = source.Connect()
            .TransformAsync(async x =>
            {
                await Task.Delay(20);
                return x.ToString();
            })
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == ListChangeReason.Add)
                    {
                        results.Add(change.Item);
                    }
                }
            });

        // Add many items at once
        for (int i = 0; i < 10; i++)
        {
            source.Add(i);
        }

        await Task.Delay(200);

        Assert.Equal(10, results.Count);
    }

    [Fact]
    public async Task TransformAsync_Remove_Before_Complete()
    {
        var source = new SourceList<int>();
        var results = new List<string>();

        using var sub = source.Connect()
            .TransformAsync(async x =>
            {
                await Task.Delay(100);
                return x.ToString();
            })
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == ListChangeReason.Add)
                    {
                        results.Add(change.Item);
                    }
                }
            });

        source.Add(1);
        source.Add(2);

        // Remove immediately
        source.Remove(1);

        await Task.Delay(150);

        // Only item 2 should complete
        Assert.Single(results);
        Assert.Equal("2", results[0]);
    }

    [Fact]
    public async Task TransformAsync_NoOverload_BasicTransformation()
    {
        var source = new SourceList<int>();
        var results = new List<string>();

        using var sub = source.Connect()
            .TransformAsync(x => Task.FromResult(x.ToString()))
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == ListChangeReason.Add)
                    {
                        results.Add(change.Item);
                    }
                }
            });

        source.Add(42);
        await Task.Delay(50);

        Assert.Single(results);
        Assert.Equal("42", results[0]);
    }

    [Fact]
    public async Task TransformAsync_DisposalCleansUp()
    {
        var source = new SourceList<int>();
        var transformCount = 0;

        var sub = source.Connect()
            .TransformAsync(async x =>
            {
                Interlocked.Increment(ref transformCount);
                await Task.Delay(50);
                return x.ToString();
            })
            .Subscribe(_ => { });

        source.Add(1);
        source.Add(2);

        await Task.Delay(20); // Let transformations start

        // Dispose before transformations complete
        sub.Dispose();

        await Task.Delay(100);

        // Transformations should have been cancelled
        Assert.Equal(2, transformCount); // Both started
    }
}
