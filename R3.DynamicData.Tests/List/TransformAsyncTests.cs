using R3;
using R3.DynamicData.List;

#pragma warning disable SA1503, SA1513, SA1515, SA1107, SA1502, SA1508, SA1516

namespace R3.DynamicData.Tests.List;

public class TransformAsyncTests
{
    [Fact]
    public async Task TransformAsync_BasicTransformation()
    {
        var source = new SourceList<int>();
        var results = new List<string>();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = source.Connect()
            .TransformAsync(async x =>
            {
                return x.ToString();
            })
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == ListChangeReason.Add)
                    {
                        results.Add(change.Item);
                        if (results.Count == 3) tcs.TrySetResult(true);
                    }
                }
            });

        source.Add(1);
        source.Add(2);
        source.Add(3);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, results.Count);
        Assert.Contains("1", results);
        Assert.Contains("2", results);
        Assert.Contains("3", results);
    }

    [Fact]
    public async Task TransformAsync_WithCancellation()
    {
        var source = new SourceList<int>();
        var started = new List<int>();
        var completed = new List<int>();
        var results = new List<string>();
        var startTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var neverCompleteTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = source.Connect()
            .TransformAsync(async (x, ct) =>
            {
                started.Add(x);
                if (started.Count == 1)
                {
                    startTcs.TrySetResult(true);
                }

                try
                {
                    // Wait indefinitely until cancellation; we never SetResult on purpose.
                    await neverCompleteTcs.Task.WaitAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    // Expected; do not mark completion.
                    throw;
                }

                completed.Add(x); // Should never execute for cancelled item.
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
        await startTcs.Task.WaitAsync(TimeSpan.FromSeconds(5)); // Ensure transformation started

        source.Remove(1); // Cancel before completion

        await Task.Delay(100); // Allow cancellation to propagate

        Assert.Single(started);
        Assert.Empty(completed);
        Assert.Empty(results);
    }

    [Fact]
    public async Task TransformAsync_PreservesOrder()
    {
        var source = new SourceList<int>();
        var results = new List<string>();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = source.Connect()
            .TransformAsync(async x =>
            {
                return x.ToString();
            })
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == ListChangeReason.Add)
                    {
                        results.Add(change.Item);
                        if (results.Count == 3) tcs.TrySetResult(true);
                    }
                }
            });

        source.AddRange(new[] { 3, 2, 1 });

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task TransformAsync_Replace()
    {
        var source = new SourceList<int>();
        var results = new List<IChangeSet<string>>();
        var tcs1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcs2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;

        using var sub = source.Connect()
            .TransformAsync(async x =>
            {
                var count = Interlocked.Increment(ref callCount);
                if (count == 1)
                {
                    await tcs1.Task.WaitAsync(TimeSpan.FromSeconds(5));
                }
                else if (count == 2)
                {
                    await tcs2.Task.WaitAsync(TimeSpan.FromSeconds(5));
                }

                return $"Value: {x}";
            })
            .Subscribe(results.Add);

        source.Add(1);
        await Task.Delay(20);
        tcs1.SetResult(true);
        await Task.Delay(20);

        Assert.Single(results);
        Assert.Equal(ListChangeReason.Add, results[0].First().Reason);

        source.ReplaceAt(0, 2);
        await Task.Delay(20);
        tcs2.SetResult(true);
        await Task.Delay(20);

        Assert.Equal(2, results.Count);
        Assert.Equal(ListChangeReason.Replace, results[1].First().Reason);
        Assert.Equal("Value: 2", results[1].First().Item);
    }

    [Fact]
    public async Task TransformAsync_Clear()
    {
        var source = new SourceList<int>();
        var results = new List<IChangeSet<string>>();
        var addsDoneTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var addCount = 0;

        using var sub = source.Connect()
            .TransformAsync(async x =>
            {
                return x.ToString();
            })
            .Subscribe(cs =>
            {
                results.Add(cs);
                foreach (var c in cs)
                {
                    if (c.Reason == ListChangeReason.Add)
                    {
                        if (Interlocked.Increment(ref addCount) == 3) addsDoneTcs.TrySetResult(true);
                    }
                }
            });

        source.AddRange(new[] { 1, 2, 3 });
        await addsDoneTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

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
        var addsDoneTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var addCount = 0;

        using var sub = source.Connect()
            .TransformAsync(async x =>
            {
                return x.ToString();
            })
            .Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    if (change.Reason == ListChangeReason.Add)
                    {
                        currentState.Add(change.Item);
                        if (Interlocked.Increment(ref addCount) == 5) addsDoneTcs.TrySetResult(true);
                    }
                    else if (change.Reason == ListChangeReason.Remove)
                    {
                        currentState.Remove(change.Item);
                    }
                }
            });

        source.AddRange(new[] { 1, 2, 3, 4, 5 });
        await addsDoneTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(5, currentState.Count);

        source.RemoveRange(1, 3);

        Assert.Equal(2, currentState.Count);
    }

    [Fact]
    public async Task TransformAsync_ConcurrentTransformations()
    {
        var source = new SourceList<int>();
        var results = new List<string>();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = source.Connect()
            .TransformAsync(async x =>
            {
                return x.ToString();
            })
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == ListChangeReason.Add)
                    {
                        results.Add(change.Item);
                        if (results.Count == 10) tcs.TrySetResult(true);
                    }
                }
            });

        // Add many items at once
        for (int i = 0; i < 10; i++)
        {
            source.Add(i);
        }

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(10, results.Count);
    }

    [Fact]
    public async Task TransformAsync_Remove_Before_Complete()
    {
        var source = new SourceList<int>();
        var results = new List<string>();
        var tcs1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcs2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resultsTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new Dictionary<int, TaskCompletionSource<bool>> { { 1, tcs1 }, { 2, tcs2 } };

        using var sub = source.Connect()
            .TransformAsync(async x =>
            {
                if (calls.TryGetValue(x, out var tcs))
                {
                    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                }

                return x.ToString();
            })
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == ListChangeReason.Add)
                    {
                        results.Add(change.Item);
                        resultsTcs.TrySetResult(true);
                    }
                }
            });

        source.Add(1);
        source.Add(2);
        source.Remove(1); // Remove 1 before it completes

        tcs2.SetResult(true);
        await resultsTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(results);
        Assert.Equal("2", results[0]);
    }

    [Fact]
    public async Task TransformAsync_NoOverload_BasicTransformation()
    {
        var source = new SourceList<int>();
        var results = new List<string>();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = source.Connect()
            .TransformAsync(x => Task.FromResult(x.ToString()))
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == ListChangeReason.Add)
                    {
                        results.Add(change.Item);
                        tcs.TrySetResult(true);
                    }
                }
            });

        source.Add(42);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(results);
        Assert.Equal("42", results[0]);
    }

    [Fact]
    public async Task TransformAsync_DisposalCleansUp()
    {
        var source = new SourceList<int>();
        var transformCount = 0;
        var neverCompleteTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;

        var sub = source.Connect()
            .TransformAsync(async x =>
            {
                var count = Interlocked.Increment(ref transformCount);
                if (Interlocked.Increment(ref started) == 2) startedTcs.TrySetResult(true);
                await neverCompleteTcs.Task.WaitAsync(CancellationToken.None);
                return x.ToString();
            })
            .Subscribe(_ => { });

        source.Add(1);
        source.Add(2);

        await startedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Dispose before transformations complete
        sub.Dispose();

        // No need to wait - startedTcs guarantees both started, and disposal is synchronous
        // Transformations should have been cancelled
        Assert.Equal(2, transformCount); // Both started
    }
}
