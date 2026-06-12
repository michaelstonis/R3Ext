using R3;
using R3.Collections;

#pragma warning disable SA1503, SA1513, SA1515, SA1107, SA1502, SA1508, SA1516

namespace R3Ext.Tests;

public class ErrorHandlingExtraTests
{
    [Fact]
    public void ReplaceError_EmitsFallbackOnErrorResume()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.ReplaceError(99).ToLiveList();
        subject.OnNext(1);
        subject.OnErrorResume(new Exception("boom"));
        Assert.Equal(new[] { 1, 99 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void ReplaceError_NullSource_ThrowsArgumentNullException()
    {
        Observable<int> nullSource = null!;
        Assert.Throws<ArgumentNullException>(() => nullSource.ReplaceError(0));
    }

    [Fact]
    public void ReplaceError_ForwardsSuccessCompletion()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.ReplaceError(99).ToLiveList();
        subject.OnNext(1);
        subject.OnCompleted();
        Assert.Equal(new[] { 1 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void ReplaceEmpty_EmitsFallbackWhenNoValues()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.ReplaceEmpty(42).ToLiveList();
        subject.OnCompleted();
        Assert.Equal(new[] { 42 }, result.ToArray());
    }

    [Fact]
    public void ReplaceEmpty_DoesNotEmitFallbackWhenHasValues()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.ReplaceEmpty(42).ToLiveList();
        subject.OnNext(1);
        subject.OnCompleted();
        Assert.Equal(new[] { 1 }, result.ToArray());
    }

    [Fact]
    public void ReplaceEmpty_NullSource_ThrowsArgumentNullException()
    {
        Observable<int> nullSource = null!;
        Assert.Throws<ArgumentNullException>(() => nullSource.ReplaceEmpty(0));
    }

    [Fact]
    public void SelectSafe_RoutesExceptionToOnErrorResume()
    {
        Subject<int> subject = new();
        List<Exception> errors = new();
        LiveList<string> result = subject.SelectSafe(x =>
        {
            if (x == 0) throw new DivideByZeroException();
            return x.ToString();
        }).DoOnError(ex => errors.Add(ex)).ToLiveList();

        subject.OnNext(1);
        subject.OnNext(0);
        subject.OnNext(2);
        subject.OnCompleted();
        Assert.Equal(new[] { "1", "2" }, result.ToArray());
        Assert.Single(errors);
        Assert.IsType<DivideByZeroException>(errors[0]);
    }

    [Fact]
    public void SelectSafe_NullSource_ThrowsArgumentNullException()
    {
        Observable<int> nullSource = null!;
        Assert.Throws<ArgumentNullException>(() => nullSource.SelectSafe(x => x.ToString()));
    }

    [Fact]
    public void WhereSafe_RoutesExceptionToOnErrorResume()
    {
        Subject<int> subject = new();
        List<Exception> errors = new();
        LiveList<int> result = subject.WhereSafe(x =>
        {
            if (x < 0) throw new ArgumentOutOfRangeException();
            return x % 2 == 0;
        }).DoOnError(ex => errors.Add(ex)).ToLiveList();

        subject.OnNext(2);
        subject.OnNext(-1);
        subject.OnNext(4);
        subject.OnCompleted();
        Assert.Equal(new[] { 2, 4 }, result.ToArray());
        Assert.Single(errors);
        Assert.IsType<ArgumentOutOfRangeException>(errors[0]);
    }

    [Fact]
    public void RetryWhen_RetriesOnHandlerSignal()
    {
        int attempts = 0;
        // Use Observable.Create so events fire after subscription (not before like with pre-signaled Subject)
        Observable<int> src = Observable.Create<int>(observer =>
        {
            attempts++;
            if (attempts < 3)
            {
                observer.OnErrorResume(new Exception("fail"));
            }
            else
            {
                observer.OnNext(42);
                observer.OnCompleted();
            }
            return Disposable.Empty;
        });

        LiveList<int> result = src.RetryWhen(errors => errors.Select(_ => Unit.Default)).ToLiveList();
        Assert.Equal(new[] { 42 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void RetryWhen_CompletesWhenHandlerCompletes()
    {
        Subject<int> subject = new();
        Subject<Unit> trigger = new();
        LiveList<int> result = subject.RetryWhen(_ => trigger).ToLiveList();
        subject.OnNext(1);
        trigger.OnCompleted();
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void RetryWhen_NullSource_ThrowsArgumentNullException()
    {
        Observable<int> nullSource = null!;
        Assert.Throws<ArgumentNullException>(() => nullSource.RetryWhen(_ => Observable.Empty<Unit>()));
    }

    [Fact]
    public void RetryWhen_NullHandler_ThrowsArgumentNullException()
    {
        Subject<int> subject = new();
        Assert.Throws<ArgumentNullException>(() => subject.RetryWhen(null!));
    }
}
