using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using R3;
using Xunit;

namespace R3Ext.Tests;

public class ErrorHandlingTests
{
    [Fact]
    public async Task CatchIgnore_CompletesOnError()
    {
        var subject = new Subject<int>();
        var list = subject.CatchIgnore().ToLiveList();
        subject.OnNext(1);
        subject.OnErrorResume(new InvalidOperationException());
        Assert.True(list.IsCompleted);
        Assert.Equal(new[] { 1 }, list.ToArray());
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CatchAndReturn_EmitsFallback()
    {
        var subject = new Subject<int>();
        var list = subject.CatchAndReturn(99).ToLiveList();
        subject.OnErrorResume(new Exception());
        Assert.True(list.IsCompleted);
        Assert.Equal(new[] { 99 }, list.ToArray());
        await Task.CompletedTask;
    }

    [Fact]
    public async Task OnErrorRetry_RetriesAndThenCompletes()
    {
        var tp = new FakeTimeProvider();
        int tries = 0;
        var src = Observable.Defer(() =>
        {
            if (tries++ < 2)
            {
                return Observable.ReturnOnCompleted<int>(Result.Failure(new InvalidOperationException("fail")));
            }
            return Observable.Return(42);
        });

        var list = src.OnErrorRetry(retryCount: 5, delay: TimeSpan.FromSeconds(1), timeProvider: tp).ToLiveList();
        // advance stepwise for retries: first retry at +1s, second at +1s
        tp.Advance(TimeSpan.FromSeconds(1));
        tp.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(new[] { 42 }, list.ToArray());
        Assert.True(list.IsCompleted);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task RetryWithBackoff_StopsAfterMaxRetries()
    {
        var tp = new FakeTimeProvider();
        int attempts = 0;
        var src = Observable.Defer(() =>
        {
            attempts++;
            return Observable.ReturnOnCompleted<int>(Result.Failure(new Exception("fail")));
        });

        var list = src.RetryWithBackoff(maxRetries: 2, initialDelay: TimeSpan.FromSeconds(1), timeProvider: tp).ToLiveList();
        // backoff delays: 1s then 2s for two retries; advance stepwise
        tp.Advance(TimeSpan.FromSeconds(1));
        tp.Advance(TimeSpan.FromSeconds(2));
        Assert.True(list.IsCompleted);
        Assert.Empty(list.ToArray());
        Assert.True(attempts >= 3); // initial + 2 retries
        await Task.CompletedTask;
    }
}
