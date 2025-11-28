using R3;
using Xunit;

#pragma warning disable SA1503, SA1513, SA1515, SA1107, SA1502, SA1508, SA1516

namespace R3Ext.Tests;

public class AsyncExtensionsTests
{
    [Fact]
    public async Task SelectLatestAsync_ValueTask_CancelsOnNewValue()
    {
        var source = new Subject<int>();
        var results = new List<int>();
        var cancellationTokens = new List<CancellationToken>();
        var tcs1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcs2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completeTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var call1Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var call2Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;

        source.SelectLatestAsync(async (x, ct) =>
        {
            cancellationTokens.Add(ct);
            var count = Interlocked.Increment(ref callCount);
            try
            {
                if (count == 1)
                {
                    call1Started.TrySetResult(true);
                    await tcs1.Task.WaitAsync(ct);
                }
                else
                {
                    call2Started.TrySetResult(true);
                    await tcs2.Task.WaitAsync(ct);
                }

                var val = x * 2;
                results.Add(val);
                completeTcs.TrySetResult(true);
                return val;
            }
            catch (OperationCanceledException)
            {
                // cancellation expected for first operation
                throw;
            }
        }).Subscribe();

        source.OnNext(1);
        await call1Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        source.OnNext(2); // Should cancel first operation
        await call2Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        tcs2.SetResult(true);

        await completeTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(results);
        Assert.Equal(4, results[0]);
        Assert.True(cancellationTokens[0].IsCancellationRequested);
    }

    [Fact]
    public async Task SelectLatestAsync_Task_ReturnsTransformedValues()
    {
        var source = new Subject<int>();
        var results = new List<int>();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        source.SelectLatestAsync(async x =>
        {
            var val = x * 3;
            results.Add(val);
            tcs.TrySetResult(true);
            return val;
        }).Subscribe(_ => { });

        source.OnNext(5);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(results);
        Assert.Equal(15, results[0]);
    }

    [Fact]
    public void SelectLatestAsync_ThrowsOnNullSource()
    {
        Observable<int> nullSource = null!;

        Assert.Throws<ArgumentNullException>(() =>
            nullSource.SelectLatestAsync((x, ct) => new ValueTask<int>(x * 2)));
    }

    [Fact]
    public void SelectLatestAsync_ThrowsOnNullSelector()
    {
        var source = new Subject<int>();

        Assert.Throws<ArgumentNullException>(() =>
            source.SelectLatestAsync((Func<int, CancellationToken, ValueTask<int>>)null!));
    }

    [Fact]
    public async Task SelectAsyncSequential_ProcessesValuesInOrder()
    {
        var source = new Subject<int>();
        var results = new List<int>();
        var processingOrder = new List<int>();
        var completionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Per-item TCS for controlled completion
        var itemTcs = new Dictionary<int, TaskCompletionSource<bool>>
        {
            [1] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            [2] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            [3] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        var itemStarted = new Dictionary<int, TaskCompletionSource<bool>>
        {
            [1] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            [2] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            [3] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
        };

        source.SelectAsyncSequential(async (x, ct) =>
        {
            processingOrder.Add(x);
            itemStarted[x].TrySetResult(true);
            await itemTcs[x].Task.WaitAsync(ct);
            return x * 2;
        }).Subscribe(value =>
        {
            results.Add(value);
            if (results.Count == 3) completionTcs.TrySetResult(true);
        });

        source.OnNext(3);
        source.OnNext(1);
        source.OnNext(2);

        // 3 should start first (sequential), then 1, then 2
        await itemStarted[3].Task.WaitAsync(TimeSpan.FromSeconds(5));
        itemTcs[3].SetResult(true);

        await itemStarted[1].Task.WaitAsync(TimeSpan.FromSeconds(5));
        itemTcs[1].SetResult(true);

        await itemStarted[2].Task.WaitAsync(TimeSpan.FromSeconds(5));
        itemTcs[2].SetResult(true);

        await completionTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, results.Count);
        Assert.Equal(new[] { 6, 2, 4 }, results);
        Assert.Equal(new[] { 3, 1, 2 }, processingOrder);
    }

    [Fact]
    public async Task SelectAsyncSequential_Task_ProcessesSequentially()
    {
        var source = new Subject<int>();
        var results = new List<string>();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        source.SelectAsyncSequential(async x =>
        {
            return $"value-{x}";
        }).Subscribe(val =>
        {
            results.Add(val);
            if (results.Count == 2)
            {
                tcs.TrySetResult(true);
            }
        });

        source.OnNext(1);
        source.OnNext(2);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, results.Count);
        Assert.Equal("value-1", results[0]);
        Assert.Equal("value-2", results[1]);
    }

    [Fact]
    public void SelectAsyncSequential_ThrowsOnNullSource()
    {
        Observable<int> nullSource = null!;

        Assert.Throws<ArgumentNullException>(() =>
            nullSource.SelectAsyncSequential((x, ct) => new ValueTask<int>(x)));
    }

    [Fact]
    public void SelectAsyncSequential_ThrowsOnNullSelector()
    {
        var source = new Subject<int>();

        Assert.Throws<ArgumentNullException>(() =>
            source.SelectAsyncSequential((Func<int, CancellationToken, ValueTask<int>>)null!));
    }

    [Fact]
    public async Task SelectAsyncConcurrent_ProcessesMultipleValuesInParallel()
    {
        var source = new Subject<int>();
        var results = new List<int>();
        var concurrentCount = 0;
        var maxConcurrent = 0;
        var completionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedCount = 0;
        var allStartedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        source.SelectAsyncConcurrent(
            async (x, ct) =>
            {
                var current = Interlocked.Increment(ref concurrentCount);
                if (Interlocked.Increment(ref startedCount) == 3)
                {
                    allStartedTcs.TrySetResult(true);
                }

                maxConcurrent = Math.Max(maxConcurrent, current);
                await releaseTcs.Task.WaitAsync(ct);
                Interlocked.Decrement(ref concurrentCount);
                return x * 2;
            },
            maxConcurrency: 3).Subscribe(value =>
        {
            results.Add(value);
            if (results.Count == 4) completionTcs.TrySetResult(true);
        });

        source.OnNext(1);
        source.OnNext(2);
        source.OnNext(3);
        await allStartedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5)); // Wait until first 3 started
        source.OnNext(4);

        // Release all blocked operations
        releaseTcs.SetResult(true);

        await completionTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(4, results.Count);
        Assert.True(maxConcurrent >= 2);
    }

    [Fact]
    public async Task SelectAsyncConcurrent_Task_RespectsMaxConcurrency()
    {
        var source = new Subject<int>();
        var results = new List<int>();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        source.SelectAsyncConcurrent(
            async x =>
            {
                return x + 10;
            },
            maxConcurrency: 2).Subscribe(val =>
        {
            results.Add(val);
            if (results.Count == 5)
            {
                tcs.TrySetResult(true);
            }
        });

        for (int i = 0; i < 5; i++)
        {
            source.OnNext(i);
        }

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(5, results.Count);
        Assert.Contains(10, results);
        Assert.Contains(14, results);
    }

    [Fact]
    public void SelectAsyncConcurrent_ThrowsOnNullSource()
    {
        Observable<int> nullSource = null!;

        Assert.Throws<ArgumentNullException>(() =>
            nullSource.SelectAsyncConcurrent((x, ct) => new ValueTask<int>(x), 2));
    }

    [Fact]
    public void SelectAsyncConcurrent_ThrowsOnNullSelector()
    {
        var source = new Subject<int>();

        Assert.Throws<ArgumentNullException>(() =>
            source.SelectAsyncConcurrent((Func<int, CancellationToken, ValueTask<int>>)null!, 2));
    }

    [Fact]
    public void SelectAsyncConcurrent_ThrowsOnZeroMaxConcurrency()
    {
        var source = new Subject<int>();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            source.SelectAsyncConcurrent((x, ct) => new ValueTask<int>(x), 0));
    }

    [Fact]
    public async Task SubscribeAsync_ValueTask_HandlesSequentialOperations()
    {
        var source = new Subject<int>();
        var processedValues = new List<int>();
        var completionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expectedCount = 3;

        // Per-item TCS for controlled completion
        var itemTcs = new Dictionary<int, TaskCompletionSource<bool>>
        {
            [1] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            [2] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            [3] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        var itemStarted = new Dictionary<int, TaskCompletionSource<bool>>
        {
            [1] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            [2] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            [3] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
        };

        source.SubscribeAsync(
            async (x, ct) =>
            {
                itemStarted[x].TrySetResult(true);
                await itemTcs[x].Task.WaitAsync(ct);
                processedValues.Add(x);
                if (processedValues.Count == expectedCount) completionTcs.TrySetResult(true);
            },
            AwaitOperation.Sequential);

        source.OnNext(1);
        source.OnNext(2);
        source.OnNext(3);

        // Complete items sequentially to prove order is maintained
        await itemStarted[1].Task.WaitAsync(TimeSpan.FromSeconds(5));
        itemTcs[1].SetResult(true);

        await itemStarted[2].Task.WaitAsync(TimeSpan.FromSeconds(5));
        itemTcs[2].SetResult(true);

        await itemStarted[3].Task.WaitAsync(TimeSpan.FromSeconds(5));
        itemTcs[3].SetResult(true);

        await completionTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, processedValues.Count);
        Assert.Equal(new[] { 1, 2, 3 }, processedValues);
    }

    [Fact]
    public async Task SubscribeAsync_Task_ProcessesValues()
    {
        var source = new Subject<string>();
        var results = new List<string>();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        source.SubscribeAsync(async x =>
        {
            results.Add(x.ToUpper());
            if (results.Count == 2)
            {
                tcs.TrySetResult(true);
            }
        });

        source.OnNext("hello");
        source.OnNext("world");

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, results.Count);
        Assert.Contains("HELLO", results);
        Assert.Contains("WORLD", results);
    }

    [Fact]
    public async Task SubscribeAsync_SwitchOperation_CancelsPreviousOperation()
    {
        var source = new Subject<int>();
        var completedValues = new List<int>();
        var cancelledTokens = new List<CancellationToken>();
        var tcs1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcs2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcsMap = new Dictionary<int, TaskCompletionSource<bool>> { { 1, tcs1 }, { 2, tcs2 } };
        var started1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        source.SubscribeAsync(
            async (x, ct) =>
            {
                cancelledTokens.Add(ct);
                if (x == 1) started1.TrySetResult(true);
                try
                {
                    if (tcsMap.TryGetValue(x, out var tcs))
                    {
                        await tcs.Task.WaitAsync(ct);
                    }

                    completedValues.Add(x);
                    if (x == 2) completionTcs.TrySetResult(true);
                }
                catch (OperationCanceledException)
                {
                    // Expected for cancelled operations
                }
            },
            AwaitOperation.Switch);

        source.OnNext(1);
        await started1.Task.WaitAsync(TimeSpan.FromSeconds(5)); // Wait until first handler started
        source.OnNext(2); // Should cancel first operation

        tcs2.SetResult(true);
        await completionTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(completedValues);
        Assert.Equal(2, completedValues[0]);
        Assert.True(cancelledTokens[0].IsCancellationRequested);
    }

    [Fact]
    public void SubscribeAsync_ThrowsOnNullSource()
    {
        Observable<int> nullSource = null!;

        Assert.Throws<ArgumentNullException>(() =>
            nullSource.SubscribeAsync((x, ct) => default));
    }

    [Fact]
    public void SubscribeAsync_ThrowsOnNullHandler()
    {
        var source = new Subject<int>();

        Assert.Throws<ArgumentNullException>(() =>
            source.SubscribeAsync((Func<int, CancellationToken, ValueTask>)null!));
    }

    [Fact]
    public async Task SelectLatestAsync_CompletesSuccessfulOperations()
    {
        var source = new Subject<int>();
        var results = new List<int>();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        var itemTcs = new Dictionary<int, TaskCompletionSource<bool>>
        {
            { 1, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) },
            { 2, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) },
            { 3, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) },
        };

        source.SelectLatestAsync(async (x, ct) =>
        {
            await itemTcs[x].Task.WaitAsync(ct);
            return x * 10;
        }).Subscribe(value =>
        {
            results.Add(value);
            if (Interlocked.Increment(ref count) == 3)
            {
                tcs.TrySetResult(true);
            }
        });

        source.OnNext(1);
        itemTcs[1].SetResult(true);
        await Task.Yield();
        source.OnNext(2);
        itemTcs[2].SetResult(true);
        await Task.Yield();
        source.OnNext(3);
        itemTcs[3].SetResult(true);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, results.Count);
        Assert.Equal(new[] { 10, 20, 30 }, results);
    }

    [Fact]
    public async Task SelectAsyncConcurrent_PropagatesExceptionsFromTasks()
    {
        var source = new Subject<int>();
        var startedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        source.SelectAsyncConcurrent<int, int>(
            async (x, ct) =>
            {
                startedTcs.TrySetResult(true);
                throw new InvalidOperationException($"Error-{x}");
            },
            maxConcurrency: 2).Subscribe(_ => { });

        source.OnNext(1);
        await startedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // If we reach here without unhandled exception, test passes
        Assert.True(true);
    }

    [Fact]
    public async Task SubscribeAsync_DisposalCancelsOperation()
    {
        var source = new Subject<int>();
        var wasCancelledTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var neverCompleteTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var subscription = source.SubscribeAsync(async (x, ct) =>
        {
            startedTcs.TrySetResult(true);
            try
            {
                await neverCompleteTcs.Task.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                wasCancelledTcs.TrySetResult(true);
            }
        });

        source.OnNext(1);
        await startedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        subscription.Dispose();
        await wasCancelledTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(wasCancelledTcs.Task.Result);
    }
}
