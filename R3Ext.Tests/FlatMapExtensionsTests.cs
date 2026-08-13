using R3;
using R3.Collections;

namespace R3Ext.Tests;

public class FlatMapExtensionsTests
{
    // ── ConcatMap ────────────────────────────────────────────────────────────

    [Fact]
    public void ConcatMap_ProcessesSequentially()
    {
        Subject<int> subject = new();
        Subject<string> inner1 = new();
        Subject<string> inner2 = new();

        LiveList<string> result = subject.ConcatMap(x => x == 1 ? inner1 : inner2).ToLiveList();

        subject.OnNext(1);
        subject.OnNext(2); // queued; inner1 not yet done

        inner1.OnNext("a");
        Assert.Equal(new[] { "a" }, result.ToArray()); // inner2 not started yet

        inner1.OnCompleted();
        inner2.OnNext("b");
        Assert.Equal(new[] { "a", "b" }, result.ToArray());
    }

    [Fact]
    public void ConcatMap_EmitsInOrder()
    {
        Subject<int> subject = new();
        Subject<int> inner1 = new();
        Subject<int> inner2 = new();

        LiveList<int> result = subject.ConcatMap(x => x == 1 ? inner1 : inner2).ToLiveList();

        subject.OnNext(1);
        subject.OnNext(2);
        inner1.OnNext(10);
        inner1.OnNext(11);
        inner1.OnCompleted();
        inner2.OnNext(20);
        inner2.OnCompleted();
        subject.OnCompleted();

        Assert.Equal(new[] { 10, 11, 20 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void ConcatMap_CompletionPropagatesAfterAllInners()
    {
        Subject<int> subject = new();
        Subject<string> inner = new();

        LiveList<string> result = subject.ConcatMap(_ => inner).ToLiveList();

        subject.OnNext(1);
        subject.OnCompleted();
        Assert.False(result.IsCompleted); // inner still active

        inner.OnNext("a");
        inner.OnCompleted();
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void ConcatMap_CompletesImmediatelyWhenNoInners()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.ConcatMap(x => Observable.Return(x)).ToLiveList();

        subject.OnCompleted();

        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void ConcatMap_ThrowsOnNullSource()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.ConcatMap(x => Observable.Return(x)));
    }

    [Fact]
    public void ConcatMap_ThrowsOnNullSelector()
    {
        Subject<int> subject = new();
        Assert.Throws<ArgumentNullException>(() => subject.ConcatMap<int, int>(null!));
    }

    // ── SwitchMap ────────────────────────────────────────────────────────────

    [Fact]
    public void SwitchMap_CancelsOnNewValue()
    {
        Subject<int> subject = new();
        Subject<string> inner1 = new();
        Subject<string> inner2 = new();

        LiveList<string> result = subject.SwitchMap(x => x == 1 ? inner1 : inner2).ToLiveList();

        subject.OnNext(1);
        inner1.OnNext("a");
        subject.OnNext(2); // cancels inner1
        inner1.OnNext("dropped");
        inner2.OnNext("b");

        Assert.Equal(new[] { "a", "b" }, result.ToArray());
    }

    [Fact]
    public void SwitchMap_CompletesWhenSourceAndCurrentInnerDone()
    {
        Subject<int> subject = new();
        Subject<string> inner = new();

        LiveList<string> result = subject.SwitchMap(_ => inner).ToLiveList();

        subject.OnNext(1);
        subject.OnCompleted();
        Assert.False(result.IsCompleted); // inner still active

        inner.OnNext("a");
        inner.OnCompleted();
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void SwitchMap_CompletesImmediatelyWhenSourceCompletesWithNoActiveInner()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.SwitchMap(x => Observable.Return(x)).ToLiveList();

        subject.OnNext(1);
        subject.OnCompleted();

        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void SwitchMap_ThrowsOnNullSource()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.SwitchMap(x => Observable.Return(x)));
    }

    [Fact]
    public void SwitchMap_ThrowsOnNullSelector()
    {
        Subject<int> subject = new();
        Assert.Throws<ArgumentNullException>(() => subject.SwitchMap<int, int>(null!));
    }

    // ── FlatMapLatest ────────────────────────────────────────────────────────

    [Fact]
    public void FlatMapLatest_IsAliasForSwitchMap()
    {
        Subject<int> subject = new();
        Subject<string> inner1 = new();
        Subject<string> inner2 = new();

        LiveList<string> result = subject.FlatMapLatest(x => x == 1 ? inner1 : inner2).ToLiveList();

        subject.OnNext(1);
        inner1.OnNext("a");
        subject.OnNext(2);
        inner1.OnNext("dropped");
        inner2.OnNext("b");

        Assert.Equal(new[] { "a", "b" }, result.ToArray());
    }

    // ── ExhaustMap ───────────────────────────────────────────────────────────

    [Fact]
    public void ExhaustMap_IgnoresValuesWhileInnerActive()
    {
        Subject<int> subject = new();
        Subject<string> inner1 = new();
        Subject<string> inner2 = new();

        LiveList<string> result = subject.ExhaustMap(x => x == 1 ? inner1 : inner2).ToLiveList();

        subject.OnNext(1);
        subject.OnNext(2); // ignored; inner1 still active
        inner1.OnNext("a");
        inner1.OnCompleted();

        subject.OnNext(2); // now accepted
        inner2.OnNext("b");

        Assert.Equal(new[] { "a", "b" }, result.ToArray());
    }

    [Fact]
    public void ExhaustMap_CompletesAfterSourceAndInner()
    {
        Subject<int> subject = new();
        Subject<string> inner = new();

        LiveList<string> result = subject.ExhaustMap(_ => inner).ToLiveList();

        subject.OnNext(1);
        subject.OnCompleted();
        Assert.False(result.IsCompleted);

        inner.OnCompleted();
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void ExhaustMap_AcceptsNextValueAfterInnerCompletes()
    {
        Subject<int> subject = new();
        Subject<int> inner1 = new();
        Subject<int> inner2 = new();

        LiveList<int> result = subject.ExhaustMap(x => x == 1 ? inner1 : inner2).ToLiveList();

        subject.OnNext(1);
        inner1.OnNext(10);
        inner1.OnCompleted();
        subject.OnNext(2);
        inner2.OnNext(20);
        inner2.OnCompleted();
        subject.OnCompleted();

        Assert.Equal(new[] { 10, 20 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void ExhaustMap_ThrowsOnNullSource()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.ExhaustMap(x => Observable.Return(x)));
    }

    [Fact]
    public void ExhaustMap_ThrowsOnNullSelector()
    {
        Subject<int> subject = new();
        Assert.Throws<ArgumentNullException>(() => subject.ExhaustMap<int, int>(null!));
    }

    // ── Expand ───────────────────────────────────────────────────────────────

    [Fact]
    public void Expand_RecursivelyAppliesSelector()
    {
        Subject<int> subject = new();

        // Expand values < 3 by adding 1; stop at 3
        LiveList<int> result = subject
            .Expand(x => x < 3 ? Observable.Return(x + 1) : Observable.Empty<int>())
            .ToLiveList();

        subject.OnNext(0);
        subject.OnCompleted();

        Assert.Equal(new[] { 0, 1, 2, 3 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void Expand_CompletesWhenNoMoreExpansions()
    {
        Subject<int> subject = new();

        LiveList<int> result = subject.Expand(_ => Observable.Empty<int>()).ToLiveList();

        subject.OnNext(1);
        subject.OnCompleted();

        Assert.Equal(new[] { 1 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void Expand_ThrowsOnNullSource()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.Expand(x => Observable.Return(x)));
    }

    [Fact]
    public void Expand_ThrowsOnNullSelector()
    {
        Subject<int> subject = new();
        Assert.Throws<ArgumentNullException>(() => subject.Expand(null!));
    }

    // ── MergeScan ────────────────────────────────────────────────────────────

    [Fact]
    public void MergeScan_UpdatesAccumulatorState()
    {
        Subject<int> subject = new();

        LiveList<int> result = subject
            .MergeScan(0, (acc, x) => Observable.Return(acc + x))
            .ToLiveList();

        subject.OnNext(1); // acc=0 → Return(1) → current=1, emit 1
        subject.OnNext(2); // acc=1 → Return(3) → current=3, emit 3
        subject.OnNext(3); // acc=3 → Return(6) → current=6, emit 6

        Assert.Equal(new[] { 1, 3, 6 }, result.ToArray());
    }

    [Fact]
    public void MergeScan_CompletesWhenSourceAndAllInnersDone()
    {
        Subject<int> subject = new();
        Subject<int> inner = new();

        LiveList<int> result = subject.MergeScan(0, (_, _) => inner).ToLiveList();

        subject.OnNext(1);
        subject.OnCompleted();
        Assert.False(result.IsCompleted);

        inner.OnNext(10);
        inner.OnCompleted();
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void MergeScan_EmitsFromActiveInner()
    {
        Subject<int> subject = new();
        Subject<int> inner = new();

        LiveList<int> result = subject.MergeScan(0, (acc, x) => inner).ToLiveList();

        subject.OnNext(1);
        inner.OnNext(42);

        Assert.Equal(new[] { 42 }, result.ToArray());
    }

    [Fact]
    public void MergeScan_ThrowsOnNullSource()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.MergeScan(0, (acc, x) => Observable.Return(acc + x)));
    }

    [Fact]
    public void MergeScan_ThrowsOnNullAccumulator()
    {
        Subject<int> subject = new();
        Assert.Throws<ArgumentNullException>(() => subject.MergeScan<int, int>(0, null!));
    }

    // ── SwitchScan ───────────────────────────────────────────────────────────

    [Fact]
    public void SwitchScan_SwitchesToLatestInner()
    {
        Subject<int> subject = new();
        Subject<int> inner1 = new();
        Subject<int> inner2 = new();
        int callCount = 0;

        LiveList<int> result = subject.SwitchScan(0, (acc, x) =>
        {
            callCount++;
            return callCount == 1 ? inner1 : inner2;
        }).ToLiveList();

        subject.OnNext(1);
        inner1.OnNext(10);
        subject.OnNext(2);   // switches away from inner1
        inner1.OnNext(99);   // dropped
        inner2.OnNext(20);

        Assert.Equal(new[] { 10, 20 }, result.ToArray());
    }

    [Fact]
    public void SwitchScan_UpdatesAccumulatorOnEachSwitch()
    {
        Subject<int> subject = new();

        LiveList<int> result = subject
            .SwitchScan(0, (acc, x) => Observable.Return(acc + x))
            .ToLiveList();

        subject.OnNext(1); // acc=0 → Return(1) → current=1, emit 1
        subject.OnNext(2); // acc=1 → Return(3) → current=3, emit 3
        subject.OnNext(4); // acc=3 → Return(7) → current=7, emit 7

        Assert.Equal(new[] { 1, 3, 7 }, result.ToArray());
    }

    [Fact]
    public void SwitchScan_CompletesWhenSourceAndCurrentInnerDone()
    {
        Subject<int> subject = new();
        Subject<int> inner = new();

        LiveList<int> result = subject.SwitchScan(0, (_, _) => inner).ToLiveList();

        subject.OnNext(1);
        subject.OnCompleted();
        Assert.False(result.IsCompleted);

        inner.OnCompleted();
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void SwitchScan_ThrowsOnNullSource()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.SwitchScan(0, (acc, x) => Observable.Return(acc + x)));
    }

    [Fact]
    public void SwitchScan_ThrowsOnNullAccumulator()
    {
        Subject<int> subject = new();
        Assert.Throws<ArgumentNullException>(() => subject.SwitchScan<int, int>(0, null!));
    }
}
