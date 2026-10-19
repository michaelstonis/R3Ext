#pragma warning disable SA1107, SA1124, SA1501, SA1503, SA1515, SA1025, SA1520, SA1513, SA1508, SA1516
using R3;
using R3.Collections;

namespace R3Ext.Tests;

public class SubjectTypesTests
{
    [Fact]
    public void AsyncSubject_EmitsLastValueOnCompletion()
    {
        AsyncSubject<int> subject = new();
        LiveList<int> result = subject.ToLiveList();
        subject.OnNext(1); subject.OnNext(2); subject.OnNext(3);
        subject.OnCompleted();
        Assert.Equal(new[] { 3 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void AsyncSubject_LateSubscriberGetsLastValue()
    {
        AsyncSubject<int> subject = new();
        subject.OnNext(42);
        subject.OnCompleted();
        // subscribe AFTER completion
        LiveList<int> result = subject.ToLiveList();
        Assert.Equal(new[] { 42 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void AsyncSubject_NoValueOnFailure()
    {
        AsyncSubject<int> subject = new();
        subject.OnNext(1);
        subject.OnCompleted(new Exception("fail"));
        LiveList<int> result = subject.ToLiveList();
        Assert.Empty(result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void AsyncSubject_EmptyOnCompletionWithNoValues()
    {
        AsyncSubject<int> subject = new();
        LiveList<int> result = subject.ToLiveList();
        subject.OnCompleted();
        Assert.Empty(result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void AsyncSubject_OnNextAfterCompletion_IsIgnored()
    {
        AsyncSubject<int> subject = new();
        LiveList<int> result = subject.ToLiveList();
        subject.OnNext(1);
        subject.OnCompleted();
        subject.OnNext(99); // should be ignored
        Assert.Equal(new[] { 1 }, result.ToArray());
    }

    [Fact]
    public void AsyncSubject_DisposedSubject_LateSubscriberGetsError()
    {
        AsyncSubject<int> subject = new();
        subject.OnNext(5);
        subject.Dispose();
        LiveList<int> result = subject.ToLiveList();
        Assert.Empty(result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void ReadOnlySubject_HidesSubjectMethods()
    {
        Subject<int> inner = new();
        ReadOnlySubject<int> ro = inner.AsReadOnly();
        LiveList<int> result = ro.ToLiveList();
        inner.OnNext(1); inner.OnNext(2);
        inner.OnCompleted();
        Assert.Equal(new[] { 1, 2 }, result.ToArray());
        Assert.IsNotType<Subject<int>>(ro);
    }

    [Fact]
    public void ReadOnlySubject_BehaviorSubject_EmitsCurrentAndFutureValues()
    {
        BehaviorSubject<int> inner = new(10);
        ReadOnlySubject<int> ro = inner.AsReadOnly();
        LiveList<int> result = ro.ToLiveList();
        inner.OnNext(20);
        inner.OnCompleted();
        Assert.Equal(new[] { 10, 20 }, result.ToArray());
    }

    [Fact]
    public void ReadOnlySubject_Observable_WrapsCorrectly()
    {
        Subject<string> inner = new();
        ReadOnlySubject<string> ro = ((Observable<string>)inner).AsReadOnly();
        LiveList<string> result = ro.ToLiveList();
        inner.OnNext("hello");
        inner.OnCompleted();
        Assert.Equal(new[] { "hello" }, result.ToArray());
    }
}
