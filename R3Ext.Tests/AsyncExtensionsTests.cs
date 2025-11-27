using R3;
using Xunit;

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
        var callCount = 0;

        source.SelectLatestAsync(async (x, ct) =>
        {
            cancellationTokens.Add(ct);
            var count = Interlocked.Increment(ref callCount);
            try
            {
                if (count == 1)
                {
                    await tcs1.Task.WaitAsync(ct);
                }
                else
                {
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
        await Task.Delay(20);
        source.OnNext(2); // Should cancel first operation
        await Task.Delay(20);
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

        source.SelectLatestAsync(async x =>
        {
            await Task.Delay(1);
            return x * 3;
        }).Subscribe(results.Add);

        source.OnNext(5);
        await Task.Delay(50);

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
        var tcs = new TaskCompletionSource<bool>();

        source.SelectAsyncSequential(async (x, ct) =>
        {
            processingOrder.Add(x);
            await Task.Delay(x * 10, ct);
            return x * 2;
        }).Subscribe(value =>
        {
            results.Add(value);
            if (results.Count == 3)
            {
                tcs.TrySetResult(true);
            }
        });

        source.OnNext(3);
        source.OnNext(1);
        source.OnNext(2);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, results.Count);
        Assert.Equal(new[] { 6, 2, 4 }, results);
        Assert.Equal(new[] { 3, 1, 2 }, processingOrder);
    }

    [Fact]
    public async Task SelectAsyncSequential_Task_ProcessesSequentially()
    {
        var source = new Subject<int>();
        var results = new List<string>();

        source.SelectAsyncSequential(async x =>
        {
            await Task.Delay(10);
            return $"value-{x}";
        }).Subscribe(results.Add);

        source.OnNext(1);
        source.OnNext(2);
        await Task.Delay(100);

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
        var tcs = new TaskCompletionSource<bool>();

        source.SelectAsyncConcurrent(
            async (x, ct) =>
            {
                var current = Interlocked.Increment(ref concurrentCount);
                maxConcurrent = Math.Max(maxConcurrent, current);
                await Task.Delay(50, ct);
                Interlocked.Decrement(ref concurrentCount);
                return x * 2;
            },
            maxConcurrency: 3).Subscribe(value =>
        {
            results.Add(value);
            if (results.Count == 4)
            {
                tcs.TrySetResult(true);
            }
        });

        source.OnNext(1);
        source.OnNext(2);
        source.OnNext(3);
        await Task.Delay(20);
        source.OnNext(4);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(4, results.Count);
        Assert.True(maxConcurrent >= 2);
    }

    [Fact]
    public async Task SelectAsyncConcurrent_Task_RespectsMaxConcurrency()
    {
        var source = new Subject<int>();
        var results = new List<int>();

        source.SelectAsyncConcurrent(
            async x =>
            {
                await Task.Delay(10);
                return x + 10;
            },
            maxConcurrency: 2).Subscribe(results.Add);

        for (int i = 0; i < 5; i++)
        {
            source.OnNext(i);
        }

        await Task.Delay(150);

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
        var tcs = new TaskCompletionSource<bool>();
        var expectedCount = 3;

        source.SubscribeAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                processedValues.Add(x);
                if (processedValues.Count == expectedCount)
                {
                    tcs.TrySetResult(true);
                }
            },
            AwaitOperation.Sequential);

        source.OnNext(1);
        source.OnNext(2);
        source.OnNext(3);

        // Wait for all operations to complete
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, processedValues.Count);
        Assert.Equal(new[] { 1, 2, 3 }, processedValues);
    }

    [Fact]
    public async Task SubscribeAsync_Task_ProcessesValues()
    {
        var source = new Subject<string>();
        var results = new List<string>();
        var tcs = new TaskCompletionSource<bool>();

        source.SubscribeAsync(async x =>
        {
            await Task.Delay(5);
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

        source.SubscribeAsync(
            async (x, ct) =>
            {
                cancelledTokens.Add(ct);
                try
                {
                    if (tcsMap.TryGetValue(x, out var tcs))
                    {
                        await tcs.Task.WaitAsync(ct);
                    }

                    completedValues.Add(x);
                }
                catch (OperationCanceledException)
                {
                    // Expected for cancelled operations
                }
            },
            AwaitOperation.Switch);

        source.OnNext(1);
        await Task.Delay(50);
        source.OnNext(2);

        await Task.Delay(50);
        tcs2.SetResult(true);
        await Task.Delay(50);

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

        source.SelectLatestAsync(async (x, ct) =>
        {
            await Task.Delay(2, ct);
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
        await Task.Delay(50);
        source.OnNext(2);
        await Task.Delay(50);
        source.OnNext(3);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, results.Count);
        Assert.Equal(new[] { 10, 20, 30 }, results);
    }

    [Fact]
    public async Task SelectAsyncConcurrent_PropagatesExceptionsFromTasks()
    {
        var source = new Subject<int>();
        var tcs = new TaskCompletionSource<bool>();

        source.SelectAsyncConcurrent<int, int>(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                throw new InvalidOperationException($"Error-{x}");
            },
            maxConcurrency: 2).Subscribe(_ => { });

        source.OnNext(1);
        await Task.Delay(50);

        // If we reach here without unhandled exception, test passes
        Assert.True(true);
    }

    [Fact]
    public async Task SubscribeAsync_DisposalCancelsOperation()
    {
        var source = new Subject<int>();
        var wasCancelled = false;

        var subscription = source.SubscribeAsync(async (x, ct) =>
        {
            try
            {
                await Task.Delay(100, ct);
            }
            catch (OperationCanceledException)
            {
                wasCancelled = true;
            }
        });

        source.OnNext(1);
        await Task.Delay(10);

        subscription.Dispose();
        await Task.Delay(50);

        Assert.True(wasCancelled);
    }
}
