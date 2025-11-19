using Microsoft.Extensions.Time.Testing;
using R3;
using R3.Collections;

namespace R3Ext.Tests;

public class ErrorHandlingTests
{
    [Fact]
    public async Task CatchIgnore_CompletesOnError()
    {
        Subject<int> subject = new();
        LiveList<int> list = subject.CatchIgnore().ToLiveList();
        subject.OnNext(1);
        subject.OnErrorResume(new InvalidOperationException());
        Assert.True(list.IsCompleted);
        Assert.Equal(new[] { 1, }, list.ToArray());
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CatchAndReturn_EmitsFallback()
    {
        Subject<int> subject = new();
        LiveList<int> list = subject.CatchAndReturn(99).ToLiveList();
        subject.OnErrorResume(new Exception());
        Assert.True(list.IsCompleted);
        Assert.Equal(new[] { 99, }, list.ToArray());
        await Task.CompletedTask;
    }

    [Fact]
    public async Task OnErrorRetry_RetriesAndThenCompletes()
    {
        FakeTimeProvider tp = new();
        int tries = 0;
        Observable<int> src = Observable.Defer(() =>
        {
            if (tries++ < 2)
            {
                return Observable.ReturnOnCompleted<int>(Result.Failure(new InvalidOperationException("fail")));
            }

            return Observable.Return(42);
        });

        LiveList<int> list = src.OnErrorRetry(5, TimeSpan.FromSeconds(1), tp).ToLiveList();

        // advance stepwise for retries: first retry at +1s, second at +1s
        tp.Advance(TimeSpan.FromSeconds(1));
        tp.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(new[] { 42, }, list.ToArray());
        Assert.True(list.IsCompleted);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task RetryWithBackoff_StopsAfterMaxRetries()
    {
        FakeTimeProvider tp = new();
        int attempts = 0;
        Observable<int> src = Observable.Defer(() =>
        {
            attempts++;
            return Observable.ReturnOnCompleted<int>(Result.Failure(new Exception("fail")));
        });

        LiveList<int> list = src.RetryWithBackoff(2, TimeSpan.FromSeconds(1), timeProvider: tp).ToLiveList();

        // backoff delays: 1s then 2s for two retries; advance stepwise
        tp.Advance(TimeSpan.FromSeconds(1));
        tp.Advance(TimeSpan.FromSeconds(2));
        Assert.True(list.IsCompleted);
        Assert.Empty(list.ToArray());
        Assert.True(attempts >= 3); // initial + 2 retries
        await Task.CompletedTask;
    }
}
