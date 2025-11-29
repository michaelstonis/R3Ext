using Microsoft.Extensions.Logging;
using R3;
using Xunit;

namespace R3Ext.Tests;

[Collection("FrameProvider")]
public class ObserverExtensionsCompleteTests(FrameProviderFixture fp)
{
    [Fact]
    public void OnNext_ParamsArray_ThrowsOnNullObserver()
    {
        Observer<int>? observer = null;
        Assert.Throws<ArgumentNullException>(() => observer!.OnNext(1, 2, 3));
    }

    [Fact]
    public void OnNext_ParamsArray_HandlesNullValues()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.Subscribe(results.Add);

        subject.AsObserver().OnNext(null);

        Assert.Empty(results);
    }

    [Fact]
    public void OnNext_ParamsArray_PushesMultipleValues()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.Subscribe(results.Add);

        subject.AsObserver().OnNext(1, 2, 3, 4, 5);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, results);
    }

    [Fact]
    public void OnNext_ParamsArray_HandlesEmptyArray()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.Subscribe(results.Add);

        subject.AsObserver().OnNext();

        Assert.Empty(results);
    }

    [Fact]
    public void OnNext_ParamsArray_HandlesSingleValue()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.Subscribe(results.Add);

        subject.AsObserver().OnNext(42);

        Assert.Single(results);
        Assert.Equal(42, results[0]);
    }

    [Fact]
    public void OnNext_IEnumerable_ThrowsOnNullObserver()
    {
        Observer<int>? observer = null;
        Assert.Throws<ArgumentNullException>(() => observer!.OnNext(new List<int> { 1, 2, 3 }));
    }

    [Fact]
    public void OnNext_IEnumerable_HandlesNullValues()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.Subscribe(results.Add);

        subject.AsObserver().OnNext((IEnumerable<int>)null!);

        Assert.Empty(results);
    }

    [Fact]
    public void OnNext_IEnumerable_PushesSequenceValues()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.Subscribe(results.Add);

        subject.AsObserver().OnNext(new List<int> { 10, 20, 30 });

        Assert.Equal(new[] { 10, 20, 30 }, results);
    }

    [Fact]
    public void OnNext_IEnumerable_HandlesEmptySequence()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.Subscribe(results.Add);

        subject.AsObserver().OnNext(Array.Empty<int>());

        Assert.Empty(results);
    }

    [Fact]
    public void OnNext_IEnumerable_WorksWithLinqQuery()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.Subscribe(results.Add);

        var query = Enumerable.Range(1, 5).Where(x => x % 2 == 0);
        subject.AsObserver().OnNext(query);

        Assert.Equal(new[] { 2, 4 }, results);
    }

    [Fact]
    public void Partition_ThrowsOnNullSource()
    {
        Observable<int> source = null!;
        Assert.Throws<ArgumentNullException>(() => source.Partition(x => x > 5));
    }

    [Fact]
    public void Partition_ThrowsOnNullPredicate()
    {
        var source = Observable.Range(1, 10);
        Assert.Throws<ArgumentNullException>(() => source.Partition(null!));
    }

    [Fact]
    public void Partition_SplitsStreamCorrectly()
    {
        var source = new Subject<int>();
        var trueResults = new List<int>();
        var falseResults = new List<int>();

        var (trueStream, falseStream) = source.Partition(x => x % 2 == 0);

        trueStream.Subscribe(trueResults.Add);
        falseStream.Subscribe(falseResults.Add);

        source.OnNext(1);
        source.OnNext(2);
        source.OnNext(3);
        source.OnNext(4);
        source.OnNext(5);

        Assert.Equal(new[] { 2, 4 }, trueResults);
        Assert.Equal(new[] { 1, 3, 5 }, falseResults);
    }

    [Fact]
    public void Partition_HandlesAllTrueCase()
    {
        var source = new Subject<int>();
        var trueResults = new List<int>();
        var falseResults = new List<int>();

        var (trueStream, falseStream) = source.Partition(x => true);

        trueStream.Subscribe(trueResults.Add);
        falseStream.Subscribe(falseResults.Add);

        source.OnNext(1);
        source.OnNext(2);
        source.OnNext(3);

        Assert.Equal(new[] { 1, 2, 3 }, trueResults);
        Assert.Empty(falseResults);
    }

    [Fact]
    public void Partition_HandlesAllFalseCase()
    {
        var source = new Subject<int>();
        var trueResults = new List<int>();
        var falseResults = new List<int>();

        var (trueStream, falseStream) = source.Partition(x => false);

        trueStream.Subscribe(trueResults.Add);
        falseStream.Subscribe(falseResults.Add);

        source.OnNext(1);
        source.OnNext(2);
        source.OnNext(3);

        Assert.Empty(trueResults);
        Assert.Equal(new[] { 1, 2, 3 }, falseResults);
    }

    [Fact]
    public void DoOnSubscribe_ThrowsOnNullSource()
    {
        Observable<int> source = null!;
        Assert.Throws<ArgumentNullException>(() => source.DoOnSubscribe(() => { }));
    }

    [Fact]
    public void DoOnSubscribe_ThrowsOnNullAction()
    {
        var source = Observable.Range(1, 5);
        Assert.Throws<ArgumentNullException>(() => source.DoOnSubscribe(null!));
    }

    [Fact]
    public void DoOnSubscribe_InvokesActionOnSubscribe()
    {
        var invoked = false;
        var source = Observable.Range(1, 5);

        var withAction = source.DoOnSubscribe(() => invoked = true);

        Assert.False(invoked);

        withAction.Subscribe(_ => { });

        Assert.True(invoked);
    }

    [Fact]
    public void DoOnSubscribe_InvokesActionForMultipleSubscribers()
    {
        var invokeCount = 0;
        var source = new Subject<int>();

        var withAction = source.DoOnSubscribe(() => invokeCount++);

        withAction.Subscribe(_ => { });
        withAction.Subscribe(_ => { });
        withAction.Subscribe(_ => { });

        Assert.Equal(3, invokeCount);
    }

    [Fact]
    public void DoOnDispose_ThrowsOnNullSource()
    {
        Observable<int> source = null!;
        Assert.Throws<ArgumentNullException>(() => source.DoOnDispose(() => { }));
    }

    [Fact]
    public void DoOnDispose_ThrowsOnNullAction()
    {
        var source = Observable.Range(1, 5);
        Assert.Throws<ArgumentNullException>(() => source.DoOnDispose(null!));
    }

    [Fact]
    public void DoOnDispose_InvokesActionOnDispose()
    {
        var invoked = false;
        var source = new Subject<int>();

        var withAction = source.DoOnDispose(() => invoked = true);

        Assert.False(invoked);

        var subscription = withAction.Subscribe(_ => { });
        Assert.False(invoked);

        subscription.Dispose();
        Assert.True(invoked);
    }

    [Fact]
    public void DoOnDispose_InvokesActionForEachDisposal()
    {
        var invokeCount = 0;
        var source = new Subject<int>();

        var withAction = source.DoOnDispose(() => invokeCount++);

        var sub1 = withAction.Subscribe(_ => { });
        var sub2 = withAction.Subscribe(_ => { });
        var sub3 = withAction.Subscribe(_ => { });

        sub1.Dispose();
        Assert.Equal(1, invokeCount);

        sub2.Dispose();
        Assert.Equal(2, invokeCount);

        sub3.Dispose();
        Assert.Equal(3, invokeCount);
    }

    [Fact]
    public void Log_WithoutLogger_DoesNotThrow()
    {
        var source = new Subject<int>();
        var logged = source.Log();

        var results = new List<int>();
        logged.Subscribe(results.Add);

        source.OnNext(1);
        source.OnNext(2);
        source.OnCompleted();

        Assert.Equal(new[] { 1, 2 }, results);
    }

    [Fact]
    public void Log_WithoutLogger_UsesDefaultTag()
    {
        var source = new Subject<int>();
        var logged = source.Log();

        var results = new List<int>();
        logged.Subscribe(results.Add);

        source.OnNext(42);

        Assert.Single(results);
        Assert.Equal(42, results[0]);
    }

    [Fact]
    public void Log_WithoutLogger_UsesCustomTag()
    {
        var source = new Subject<int>();
        var logged = source.Log(tag: "CustomTag");

        var results = new List<int>();
        logged.Subscribe(results.Add);

        source.OnNext(100);

        Assert.Single(results);
        Assert.Equal(100, results[0]);
    }
}
