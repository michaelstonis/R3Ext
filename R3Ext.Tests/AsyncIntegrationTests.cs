using R3;
using R3.Collections;

#pragma warning disable SA1503, SA1513, SA1515, SA1107, SA1502, SA1508, SA1516

namespace R3Ext.Tests;

public class AsyncIntegrationTests
{
    [Fact]
    public async Task SelectLatestAsync_CancelsPrevious()
    {
        Subject<int> subject = new();
        int completed = 0;
        var blockTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Observable<int> obs = subject.SelectLatestAsync(async (x, ct) =>
        {
            try
            {
                await blockTcs.Task.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // Expected for cancelled items
            }

            Interlocked.Increment(ref completed);
            return x;
        });

        LiveList<int> list = obs.ToLiveList();
        subject.OnNext(1);
        subject.OnNext(2); // cancel 1
        subject.OnNext(3); // cancel 2

        // Release the blocking TCS
        blockTcs.SetResult(true);

        subject.OnCompleted();
        Assert.True(list.IsCompleted);
    }

    [Fact]
    public async Task SelectAsyncSequential_Orders()
    {
        Subject<int> subject = new();
        Observable<int> obs = subject.SelectAsyncSequential(
            async (x, ct) =>
            {
                await Task.Yield();
                return x * 2;
            }, cancelOnCompleted: false);
        Task<int[]> arrTask = obs.ToArrayAsync();
        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        subject.OnCompleted();
        int[] arr = await arrTask;
        Assert.Equal(new[] { 2, 4, 6, }, arr);
    }

    [Fact]
    public void SelectAsyncConcurrent_LimitsZero_Throws()
    {
        Observable<int> src = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => src.SelectAsyncConcurrent(async (x, ct) => x, 0));
    }

    [Fact]
    public async Task SubscribeAsync_TaskOverload_Works()
    {
        Observable<int> src = CreationExtensions.FromArray(1, 2, 3);
        int sum = 0;
        var completedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable d = src.SubscribeAsync(
            x =>
            {
                sum += x;
                if (sum == 6) completedTcs.TrySetResult(true);
                return Task.CompletedTask;
            });
        await completedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(6, sum);
    }
}
