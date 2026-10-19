using R3;
using R3.Collections;

namespace R3Ext.Tests;

public class TransformationExtensionsTests
{
    // ── MapTo ────────────────────────────────────────────────────────────────

    [Fact]
    public void MapTo_ReplacesAllValues()
    {
        Subject<int> subject = new();
        LiveList<string> result = subject.MapTo("x").ToLiveList();

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        Assert.Equal(new[] { "x", "x", "x" }, result.ToArray());
    }

    [Fact]
    public void MapTo_CompletionPropagates()
    {
        Subject<int> subject = new();
        LiveList<string> result = subject.MapTo("x").ToLiveList();

        subject.OnNext(1);
        subject.OnCompleted();

        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void MapTo_ThrowsOnNullSource()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.MapTo("x"));
    }

    [Fact]
    public void MapTo_WorksWithNullConstant()
    {
        Subject<int> subject = new();
        LiveList<string?> result = subject.MapTo<int, string?>(null).ToLiveList();

        subject.OnNext(1);

        Assert.Single(result);
        Assert.Null(result[0]);
    }

    // ── CompactMap (reference type) ──────────────────────────────────────────

    [Fact]
    public void CompactMap_ReferenceType_FiltersNulls()
    {
        Subject<int> subject = new();
        LiveList<string> result = subject.CompactMap(x => x % 2 == 0 ? x.ToString() : null).ToLiveList();

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        subject.OnNext(4);

        Assert.Equal(new[] { "2", "4" }, result.ToArray());
    }

    [Fact]
    public void CompactMap_ReferenceType_CompletionPropagates()
    {
        Subject<int> subject = new();
        LiveList<string> result = subject.CompactMap(x => x.ToString()).ToLiveList();

        subject.OnCompleted();

        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void CompactMap_ReferenceType_ThrowsOnNullSource()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.CompactMap<int, string>(x => x.ToString()));
    }

    [Fact]
    public void CompactMap_ReferenceType_ThrowsOnNullSelector()
    {
        Subject<int> subject = new();
        Assert.Throws<ArgumentNullException>(() => subject.CompactMap<int, string>(null!));
    }

    // ── CompactMap (value type) ──────────────────────────────────────────────

    [Fact]
    public void CompactMap_ValueType_FiltersNulls()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.CompactMap(x => x % 2 == 0 ? (int?)x : null).ToLiveList();

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        subject.OnNext(4);

        Assert.Equal(new[] { 2, 4 }, result.ToArray());
    }

    [Fact]
    public void CompactMap_ValueType_CompletionPropagates()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.CompactMap(x => (int?)x).ToLiveList();

        subject.OnCompleted();

        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void CompactMap_ValueType_ThrowsOnNullSource()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.CompactMap<int, int>(x => x));
    }

    [Fact]
    public void CompactMap_ValueType_AllNullsProducesEmpty()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.CompactMap(_ => (int?)null).ToLiveList();

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnCompleted();

        Assert.Empty(result);
        Assert.True(result.IsCompleted);
    }

    // ── WithIndex ────────────────────────────────────────────────────────────

    [Fact]
    public void WithIndex_EmitsValueAndZeroBasedIndex()
    {
        Subject<string> subject = new();
        LiveList<(string Value, int Index)> result = subject.WithIndex().ToLiveList();

        subject.OnNext("a");
        subject.OnNext("b");
        subject.OnNext("c");

        Assert.Equal(new[] { ("a", 0), ("b", 1), ("c", 2) }, result.ToArray());
    }

    [Fact]
    public void WithIndex_CompletionPropagates()
    {
        Subject<int> subject = new();
        LiveList<(int Value, int Index)> result = subject.WithIndex().ToLiveList();

        subject.OnCompleted();

        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void WithIndex_ThrowsOnNullSource()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.WithIndex());
    }

    [Fact]
    public void WithIndex_IndexIncrementsMonotonically()
    {
        Subject<int> subject = new();
        LiveList<(int Value, int Index)> result = subject.WithIndex().ToLiveList();

        for (int i = 0; i < 5; i++)
        {
            subject.OnNext(i * 10);
        }

        int[] indices = result.Select(t => t.Index).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, indices);
    }

    // ── RunningFold ──────────────────────────────────────────────────────────

    [Fact]
    public void RunningFold_AccumulatesValues()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.RunningFold(0, (acc, x) => acc + x).ToLiveList();

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        Assert.Equal(new[] { 1, 3, 6 }, result.ToArray());
    }

    [Fact]
    public void RunningFold_UsesSeedAsInitialAccumulator()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.RunningFold(10, (acc, x) => acc + x).ToLiveList();

        subject.OnNext(1);
        subject.OnNext(2);

        Assert.Equal(new[] { 11, 13 }, result.ToArray());
    }

    [Fact]
    public void RunningFold_CompletionPropagates()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.RunningFold(0, (acc, x) => acc + x).ToLiveList();

        subject.OnCompleted();

        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void RunningFold_ThrowsOnNullSource()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.RunningFold(0, (acc, x) => acc + x));
    }

    [Fact]
    public void RunningFold_ThrowsOnNullAccumulator()
    {
        Subject<int> subject = new();
        Assert.Throws<ArgumentNullException>(() => subject.RunningFold<int, int>(0, null!));
    }

    // ── RunningReduce ────────────────────────────────────────────────────────

    [Fact]
    public void RunningReduce_AccumulatesWithoutSeed()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.RunningReduce((acc, x) => acc + x).ToLiveList();

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        Assert.Equal(new[] { 1, 3, 6 }, result.ToArray());
    }

    [Fact]
    public void RunningReduce_CompletionPropagates()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.RunningReduce((acc, x) => acc + x).ToLiveList();

        subject.OnCompleted();

        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void RunningReduce_ThrowsOnNullSource()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.RunningReduce((acc, x) => acc + x));
    }

    [Fact]
    public void RunningReduce_ThrowsOnNullAccumulator()
    {
        Subject<int> subject = new();
        Assert.Throws<ArgumentNullException>(() => subject.RunningReduce(null!));
    }

    [Fact]
    public void RunningReduce_FirstValueIsEmittedUnchanged()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.RunningReduce((acc, x) => acc * x).ToLiveList();

        subject.OnNext(5);

        Assert.Equal(new[] { 5 }, result.ToArray());
    }
}
