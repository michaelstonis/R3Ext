using R3;
using R3.Collections;

#pragma warning disable SA1503, SA1513, SA1515, SA1107, SA1502, SA1508, SA1516

namespace R3Ext.Tests;

public class SideEffectExtensionsTests
{
    [Fact]
    public void DoOnError_InvokesActionOnErrorResume()
    {
        Subject<int> subject = new();
        List<Exception> captured = new();
        LiveList<int> result = subject.DoOnError(ex => captured.Add(ex)).ToLiveList();
        subject.OnNext(1);
        subject.OnErrorResume(new InvalidOperationException("test"));
        subject.OnNext(2);
        subject.OnCompleted();
        Assert.Single(captured);
        Assert.Equal(new[] { 1, 2 }, result.ToArray());
    }

    [Fact]
    public void DoOnError_DoesNotFireOnSuccessCompletion()
    {
        Subject<int> subject = new();
        int callCount = 0;
        LiveList<int> result = subject.DoOnError(_ => callCount++).ToLiveList();
        subject.OnNext(1);
        subject.OnCompleted();
        Assert.Equal(0, callCount);
        Assert.Equal(new[] { 1 }, result.ToArray());
    }

    [Fact]
    public void DoOnError_NullSource_ThrowsArgumentNullException()
    {
        Observable<int> nullSource = null!;
        Assert.Throws<ArgumentNullException>(() => nullSource.DoOnError(_ => { }));
    }

    [Fact]
    public void DoOnComplete_ResultOverload_InvokesOnTermination()
    {
        Subject<int> subject = new();
        bool called = false;
        subject.DoOnComplete(_ => called = true).ToLiveList();
        subject.OnCompleted();
        Assert.True(called);
    }

    [Fact]
    public void DoOnComplete_ActionOverload_InvokesOnlyOnSuccess()
    {
        Subject<int> s1 = new();
        Subject<int> s2 = new();
        int successCount = 0;
        s1.DoOnComplete(() => successCount++).ToLiveList();
        s2.DoOnComplete(() => successCount++).ToLiveList();
        s1.OnCompleted();
        s2.OnCompleted(Result.Failure(new Exception("fail")));
        Assert.Equal(1, successCount);
    }

    [Fact]
    public void DoOnComplete_NullSource_ThrowsArgumentNullException()
    {
        Observable<int> nullSource = null!;
        Assert.Throws<ArgumentNullException>(() => nullSource.DoOnComplete(_ => { }));
        Assert.Throws<ArgumentNullException>(() => nullSource.DoOnComplete(() => { }));
    }

    [Fact]
    public void DoOnTerminate_InvokesOnSuccessAndFailure()
    {
        int count = 0;
        Subject<int> s1 = new();
        Subject<int> s2 = new();
        s1.DoOnTerminate(() => count++).ToLiveList();
        s2.DoOnTerminate(() => count++).ToLiveList();
        s1.OnCompleted();
        s2.OnCompleted(Result.Failure(new Exception("fail")));
        Assert.Equal(2, count);
    }

    [Fact]
    public void DoOnTerminate_DoesNotFireOnOnErrorResume()
    {
        Subject<int> subject = new();
        int count = 0;
        subject.DoOnTerminate(() => count++).ToLiveList();
        subject.OnErrorResume(new Exception("non-terminal"));
        Assert.Equal(0, count);
    }

    [Fact]
    public void DoOnTerminate_NullSource_ThrowsArgumentNullException()
    {
        Observable<int> nullSource = null!;
        Assert.Throws<ArgumentNullException>(() => nullSource.DoOnTerminate(() => { }));
    }

    [Fact]
    public void DoAfterTerminate_FiresAfterDownstream()
    {
        Subject<int> subject = new();
        List<string> order = new();
        LiveList<int> result = subject
            .Do(onCompleted: _ => order.Add("downstream"))
            .DoAfterTerminate(() => order.Add("after"))
            .ToLiveList();
        subject.OnCompleted();
        Assert.Equal(new[] { "downstream", "after" }, order.ToArray());
    }

    [Fact]
    public void DoAfterTerminate_NullSource_ThrowsArgumentNullException()
    {
        Observable<int> nullSource = null!;
        Assert.Throws<ArgumentNullException>(() => nullSource.DoAfterTerminate(() => { }));
    }

    [Fact]
    public void WithIndex_PairsValueWithIndex()
    {
        Subject<string> subject = new();
        LiveList<(string Value, int Index)> result = subject.WithIndex().ToLiveList();
        subject.OnNext("a");
        subject.OnNext("b");
        subject.OnNext("c");
        subject.OnCompleted();
        Assert.Equal(("a", 0), result[0]);
        Assert.Equal(("b", 1), result[1]);
        Assert.Equal(("c", 2), result[2]);
    }

    [Fact]
    public void WithIndex_StartsAtZero()
    {
        Subject<int> subject = new();
        LiveList<(int Value, int Index)> result = subject.WithIndex().ToLiveList();
        subject.OnNext(100);
        subject.OnCompleted();
        Assert.Equal((100, 0), result[0]);
    }

    [Fact]
    public void WithIndex_NullSource_ThrowsArgumentNullException()
    {
        Observable<int> nullSource = null!;
        Assert.Throws<ArgumentNullException>(() => nullSource.WithIndex());
    }
}
