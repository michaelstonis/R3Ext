using Microsoft.Extensions.Time.Testing;
using R3;
using R3.Collections;
using Xunit;

namespace R3Ext.Tests;

public class ErrorHandlingAdvancedTests
{
    [Fact]
    public async Task CatchIgnore_NullSource_ThrowsArgumentNullException()
    {
        Observable<int> nullSource = null!;
        Assert.Throws<ArgumentNullException>(() => nullSource.CatchIgnore());
    }

    [Fact]
    public async Task CatchIgnore_EmitsValuesBeforeError()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.CatchIgnore().Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        subject.OnErrorResume(new InvalidOperationException("Test error"));

        await Task.Delay(10);

        Assert.Equal(new[] { 1, 2, 3 }, results);
    }

    [Fact]
    public async Task CatchIgnore_MultipleErrors_CompletesOnFirstError()
    {
        var subject = new Subject<int>();
        var completionCount = 0;

        subject.CatchIgnore().Subscribe(
            _ => { },
            _ => { },
            _ => completionCount++);

        subject.OnNext(1);
        subject.OnErrorResume(new Exception("First error"));

        await Task.Delay(10);

        Assert.Equal(1, completionCount);
    }

    [Fact]
    public async Task CatchAndReturn_NullSource_ThrowsArgumentNullException()
    {
        Observable<int> nullSource = null!;
        Assert.Throws<ArgumentNullException>(() => nullSource.CatchAndReturn(42));
    }

    [Fact]
    public async Task CatchAndReturn_EmitsValuesBeforeError()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.CatchAndReturn(99).Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnErrorResume(new Exception());

        await Task.Delay(10);

        Assert.Equal(new[] { 1, 2, 99 }, results);
    }

    [Fact]
    public async Task CatchAndReturn_NoError_DoesNotEmitFallback()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.CatchAndReturn(99).Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnCompleted();

        await Task.Delay(10);

        Assert.Equal(new[] { 1, 2 }, results);
    }

    [Fact]
    public async Task OnErrorRetry_NullSource_ThrowsArgumentNullException()
    {
        Observable<int> nullSource = null!;
        Assert.Throws<ArgumentNullException>(() => nullSource.OnErrorRetry());
    }

    [Fact]
    public async Task OnErrorRetry_SucceedsOnFirstTry_NoRetries()
    {
        var attempts = 0;
        var source = Observable.Defer(() =>
        {
            attempts++;
            return Observable.Return(42);
        });

        var results = new List<int>();
        source.OnErrorRetry(3).Subscribe(results.Add);

        await Task.Delay(50);

        Assert.Equal(1, attempts);
        Assert.Single(results);
        Assert.Equal(42, results[0]);
    }

    [Fact]
    public async Task OnErrorRetry_InfiniteRetries_KeepsRetrying()
    {
        var tp = new FakeTimeProvider();
        var attempts = 0;
        var source = Observable.Defer(() =>
        {
            attempts++;
            if (attempts < 10)
            {
                return Observable.ReturnOnCompleted<int>(Result.Failure(new Exception("fail")));
            }

            return Observable.Return(42);
        });

        var list = source.OnErrorRetry(-1, TimeSpan.FromSeconds(1), tp).ToLiveList();

        for (int i = 0; i < 9; i++)
        {
            tp.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.Equal(new[] { 42 }, list.ToArray());
        Assert.Equal(10, attempts);
    }

    [Fact]
    public async Task OnErrorRetry_WithDelay_WaitsBeforeRetrying()
    {
        var tp = new FakeTimeProvider();
        var attempts = 0;
        var timestamps = new List<DateTimeOffset>();

        var source = Observable.Defer(() =>
        {
            timestamps.Add(tp.GetUtcNow());
            attempts++;
            if (attempts < 3)
            {
                return Observable.ReturnOnCompleted<int>(Result.Failure(new Exception("fail")));
            }

            return Observable.Return(42);
        });

        var list = source.OnErrorRetry(5, TimeSpan.FromSeconds(2), tp).ToLiveList();

        tp.Advance(TimeSpan.FromSeconds(2));
        tp.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(3, attempts);
        Assert.True((timestamps[1] - timestamps[0]).TotalSeconds >= 2);
        Assert.True((timestamps[2] - timestamps[1]).TotalSeconds >= 2);
    }

    [Fact]
    public async Task OnErrorRetry_ExceedsMaxRetries_CompletesWithFailure()
    {
        var tp = new FakeTimeProvider();
        var attempts = 0;
        var source = Observable.Defer(() =>
        {
            attempts++;
            return Observable.ReturnOnCompleted<int>(Result.Failure(new Exception("fail")));
        });

        var list = source.OnErrorRetry(2, TimeSpan.FromSeconds(1), tp).ToLiveList();

        tp.Advance(TimeSpan.FromSeconds(1));
        tp.Advance(TimeSpan.FromSeconds(1));

        await Task.Delay(10);

        Assert.True(list.IsCompleted);
        Assert.Empty(list.ToArray());
        Assert.Equal(3, attempts); // Initial + 2 retries
    }

    [Fact]
    public async Task OnErrorRetry_DisposedDuringRetry_StopsRetrying()
    {
        var tp = new FakeTimeProvider();
        var attempts = 0;
        var source = Observable.Defer(() =>
        {
            attempts++;
            return Observable.ReturnOnCompleted<int>(Result.Failure(new Exception("fail")));
        });

        var subscription = source.OnErrorRetry(10, TimeSpan.FromSeconds(1), tp).Subscribe(_ => { });

        tp.Advance(TimeSpan.FromSeconds(1));
        var attemptsBeforeDispose = attempts;

        subscription.Dispose();
        tp.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(attemptsBeforeDispose, attempts); // No more retries after disposal
    }

    [Fact]
    public async Task RetryWithBackoff_NullSource_ThrowsArgumentNullException()
    {
        Observable<int> nullSource = null!;
        Assert.Throws<ArgumentNullException>(() => nullSource.RetryWithBackoff(3, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task RetryWithBackoff_NegativeMaxRetries_ThrowsArgumentOutOfRangeException()
    {
        var source = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            source.RetryWithBackoff(-1, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task RetryWithBackoff_NegativeInitialDelay_ThrowsArgumentOutOfRangeException()
    {
        var source = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            source.RetryWithBackoff(3, TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task RetryWithBackoff_ZeroOrNegativeFactor_ThrowsArgumentOutOfRangeException()
    {
        var source = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            source.RetryWithBackoff(3, TimeSpan.FromSeconds(1), factor: 0));
    }

    [Fact]
    public async Task RetryWithBackoff_SucceedsOnFirstTry_NoRetries()
    {
        var attempts = 0;
        var source = Observable.Defer(() =>
        {
            attempts++;
            return Observable.Return(42);
        });

        var results = new List<int>();
        source.RetryWithBackoff(3, TimeSpan.FromSeconds(1)).Subscribe(results.Add);

        await Task.Delay(50);

        Assert.Equal(1, attempts);
        Assert.Single(results);
        Assert.Equal(42, results[0]);
    }

    [Fact]
    public async Task RetryWithBackoff_ExponentialDelays()
    {
        var tp = new FakeTimeProvider();
        var attempts = 0;
        var timestamps = new List<DateTimeOffset>();

        var source = Observable.Defer(() =>
        {
            timestamps.Add(tp.GetUtcNow());
            attempts++;
            if (attempts < 4)
            {
                return Observable.ReturnOnCompleted<int>(Result.Failure(new Exception("fail")));
            }

            return Observable.Return(42);
        });

        var list = source.RetryWithBackoff(5, TimeSpan.FromSeconds(1), factor: 2.0, timeProvider: tp).ToLiveList();

        // Delays: 1s, 2s, 4s for retries 1, 2, 3
        tp.Advance(TimeSpan.FromSeconds(1));
        tp.Advance(TimeSpan.FromSeconds(2));
        tp.Advance(TimeSpan.FromSeconds(4));

        Assert.Equal(4, attempts);
        Assert.Equal(new[] { 42 }, list.ToArray());
    }

    [Fact]
    public async Task RetryWithBackoff_MaxDelayLimit()
    {
        var tp = new FakeTimeProvider();
        var attempts = 0;

        var source = Observable.Defer(() =>
        {
            attempts++;
            if (attempts < 4)
            {
                return Observable.ReturnOnCompleted<int>(Result.Failure(new Exception("fail")));
            }

            return Observable.Return(42);
        });

        var list = source.RetryWithBackoff(
            5,
            TimeSpan.FromSeconds(1),
            factor: 10.0,
            maxDelay: TimeSpan.FromSeconds(3),
            timeProvider: tp).ToLiveList();

        // With factor 10, delays would be: 1s, 10s, 100s
        // But maxDelay caps them to: 1s, 3s, 3s
        tp.Advance(TimeSpan.FromSeconds(1));
        tp.Advance(TimeSpan.FromSeconds(3));
        tp.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(4, attempts);
        Assert.Equal(new[] { 42 }, list.ToArray());
    }

    [Fact]
    public async Task RetryWithBackoff_OnErrorCallback_InvokedForEachError()
    {
        var tp = new FakeTimeProvider();
        var attempts = 0;
        var errors = new List<Exception>();

        var source = Observable.Defer(() =>
        {
            attempts++;
            if (attempts < 3)
            {
                return Observable.ReturnOnCompleted<int>(Result.Failure(new InvalidOperationException($"Attempt {attempts}")));
            }

            return Observable.Return(42);
        });

        var list = source.RetryWithBackoff(
            5,
            TimeSpan.FromSeconds(1),
            timeProvider: tp,
            onError: errors.Add).ToLiveList();

        tp.Advance(TimeSpan.FromSeconds(1));
        tp.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(2, errors.Count);
        Assert.All(errors, e => Assert.IsType<InvalidOperationException>(e));
        Assert.Contains("Attempt 1", errors[0].Message);
        Assert.Contains("Attempt 2", errors[1].Message);
    }

    [Fact]
    public async Task RetryWithBackoff_ExceedsMaxRetries_CompletesWithFailure()
    {
        var tp = new FakeTimeProvider();
        var attempts = 0;
        var source = Observable.Defer(() =>
        {
            attempts++;
            return Observable.ReturnOnCompleted<int>(Result.Failure(new Exception("fail")));
        });

        var list = source.RetryWithBackoff(2, TimeSpan.FromSeconds(1), timeProvider: tp).ToLiveList();

        tp.Advance(TimeSpan.FromSeconds(1));
        tp.Advance(TimeSpan.FromSeconds(2));

        await Task.Delay(10);

        Assert.True(list.IsCompleted);
        Assert.Empty(list.ToArray());
        Assert.Equal(3, attempts); // Initial + 2 retries
    }

    [Fact]
    public async Task RetryWithBackoff_DisposedDuringRetry_StopsRetrying()
    {
        var tp = new FakeTimeProvider();
        var attempts = 0;
        var source = Observable.Defer(() =>
        {
            attempts++;
            return Observable.ReturnOnCompleted<int>(Result.Failure(new Exception("fail")));
        });

        var subscription = source.RetryWithBackoff(10, TimeSpan.FromSeconds(1), timeProvider: tp).Subscribe(_ => { });

        tp.Advance(TimeSpan.FromSeconds(1));
        var attemptsBeforeDispose = attempts;

        subscription.Dispose();
        tp.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(attemptsBeforeDispose, attempts); // No more retries after disposal
    }

    [Fact]
    public async Task RetryWithBackoff_CustomFactor_AffectsDelays()
    {
        var tp = new FakeTimeProvider();
        var attempts = 0;

        var source = Observable.Defer(() =>
        {
            attempts++;
            if (attempts < 4)
            {
                return Observable.ReturnOnCompleted<int>(Result.Failure(new Exception("fail")));
            }

            return Observable.Return(42);
        });

        var list = source.RetryWithBackoff(5, TimeSpan.FromSeconds(2), factor: 3.0, timeProvider: tp).ToLiveList();

        // Delays with factor 3.0: 2s, 6s, 18s
        tp.Advance(TimeSpan.FromSeconds(2));
        tp.Advance(TimeSpan.FromSeconds(6));
        tp.Advance(TimeSpan.FromSeconds(18));

        Assert.Equal(4, attempts);
        Assert.Equal(new[] { 42 }, list.ToArray());
    }

    [Fact]
    public async Task OnErrorRetry_ZeroRetries_NoRetries()
    {
        var tp = new FakeTimeProvider();
        var attempts = 0;
        var source = Observable.Defer(() =>
        {
            attempts++;
            return Observable.ReturnOnCompleted<int>(Result.Failure(new Exception("fail")));
        });

        var list = source.OnErrorRetry(0, TimeSpan.FromSeconds(1), tp).ToLiveList();

        await Task.Delay(10);

        Assert.Equal(1, attempts); // Only initial attempt
        Assert.Empty(list.ToArray());
    }

    [Fact]
    public async Task RetryWithBackoff_ZeroRetries_NoRetries()
    {
        var tp = new FakeTimeProvider();
        var attempts = 0;
        var source = Observable.Defer(() =>
        {
            attempts++;
            return Observable.ReturnOnCompleted<int>(Result.Failure(new Exception("fail")));
        });

        var list = source.RetryWithBackoff(0, TimeSpan.FromSeconds(1), timeProvider: tp).ToLiveList();

        await Task.Delay(10);

        Assert.Equal(1, attempts); // Only initial attempt
        Assert.Empty(list.ToArray());
    }
}
