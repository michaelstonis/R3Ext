using System;
using System.Threading;
using System.Threading.Tasks;
using R3;
using R3Ext;
using Xunit;

namespace R3Ext.Tests;

public class AsyncIntegrationTests
{
    [Fact]
    public async Task SelectLatestAsync_CancelsPrevious()
    {
        var subject = new Subject<int>();
        int completed = 0;
        var obs = subject.SelectLatestAsync(async (x, ct) =>
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

        var list = obs.ToLiveList();
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
        var subject = new Subject<int>();
        var obs = subject.SelectAsyncSequential(async (x, ct) => { await Task.Delay(1, ct); return x * 2; }, cancelOnCompleted: false);
        var arrTask = obs.ToArrayAsync();
        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        subject.OnCompleted();
        var arr = await arrTask;
        Assert.Equal(new[] { 2, 4, 6 }, arr);
    }

    [Fact]
    public async Task SelectAsyncConcurrent_LimitsZero_Throws()
    {
        var src = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => src.SelectAsyncConcurrent(async (x, ct) => x, 0));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task SubscribeAsync_TaskOverload_Works()
    {
        var src = ReactivePortedExtensions.FromArray(1, 2, 3);
        int sum = 0;
        using var d = src.SubscribeAsync(x => { sum += x; return Task.CompletedTask; });
        await Task.Delay(1);
        Assert.Equal(6, sum);
    }
}
