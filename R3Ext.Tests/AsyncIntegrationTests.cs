using R3;
using R3.Collections;

namespace R3Ext.Tests;

public class AsyncIntegrationTests
{
    [Fact]
    public async Task SelectLatestAsync_CancelsPrevious()
    {
        Subject<int> subject = new();
        int completed = 0;
        Observable<int> obs = subject.SelectLatestAsync(async (x, ct) =>
        {
            try
            {
                await Task.Delay(1000, ct);
            }
            catch (OperationCanceledException)
            {
            }

            Interlocked.Increment(ref completed);
            return x;
        });

        LiveList<int> list = obs.ToLiveList();
        subject.OnNext(1);
        subject.OnNext(2); // cancel 1
        subject.OnNext(3); // cancel 2
        await Task.Delay(10);
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
                await Task.Delay(1, ct);
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
    public async Task SelectAsyncConcurrent_LimitsZero_Throws()
    {
        Observable<int> src = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => src.SelectAsyncConcurrent(async (x, ct) => x, 0));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task SubscribeAsync_TaskOverload_Works()
    {
        Observable<int> src = CreationExtensions.FromArray(1, 2, 3);
        int sum = 0;
        using IDisposable d = src.SubscribeAsync(x =>
        {
            sum += x;
            return Task.CompletedTask;
        });
        await Task.Delay(1);
        Assert.Equal(6, sum);
    }
}
