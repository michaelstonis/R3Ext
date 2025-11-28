using R3;

#pragma warning disable SA1503, SA1513, SA1515, SA1107, SA1502, SA1508, SA1516

namespace R3Ext.Tests;

public class SignalExtensionsAdvancedTests
{
    [Fact]
    public void AsSignal_NullSource_ThrowsArgumentNullException()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.AsSignal());
    }

    [Fact]
    public void AsSignal_EmitsUnitForEachSourceValue()
    {
        var source = new Subject<int>();
        var values = new List<Unit>();
        source.AsSignal().Subscribe(values.Add);

        source.OnNext(1);
        source.OnNext(2);
        source.OnNext(3);

        Assert.Equal(3, values.Count);
        Assert.All(values, v => Assert.Equal(Unit.Default, v));
    }

    [Fact]
    public void AsSignal_EmitsImmediatelyForEachSource()
    {
        // This test verifies AsSignal emits synchronously as source emits
        // (no timing delays introduced by the operator)
        var source = new Subject<string>();
        var emitOrder = new List<string>();

        source.AsSignal().Subscribe(_ => emitOrder.Add("signal"));

        emitOrder.Add("before-a");
        source.OnNext("a");
        emitOrder.Add("after-a");

        emitOrder.Add("before-b");
        source.OnNext("b");
        emitOrder.Add("after-b");

        // Verify signals are emitted synchronously in order
        Assert.Equal(new[] { "before-a", "signal", "after-a", "before-b", "signal", "after-b" }, emitOrder);
    }

    [Fact]
    public void AsSignal_WorksWithReferenceTypes()
    {
        var source = new Subject<string>();
        var values = new List<Unit>();
        source.AsSignal().Subscribe(values.Add);

        source.OnNext("hello");
        source.OnNext("world");

        Assert.Equal(2, values.Count);
        Assert.All(values, v => Assert.Equal(Unit.Default, v));
    }

    [Fact]
    public void AsSignal_WorksWithValueTypes()
    {
        var source = new Subject<int>();
        var values = new List<Unit>();
        source.AsSignal().Subscribe(values.Add);

        source.OnNext(42);
        source.OnNext(100);

        Assert.Equal(2, values.Count);
        Assert.All(values, v => Assert.Equal(Unit.Default, v));
    }

    [Fact]
    public void AsSignal_WorksWithStructTypes()
    {
        var source = new Subject<(int X, int Y)>();
        var values = new List<Unit>();
        source.AsSignal().Subscribe(values.Add);

        source.OnNext((1, 2));
        source.OnNext((3, 4));

        Assert.Equal(2, values.Count);
        Assert.All(values, v => Assert.Equal(Unit.Default, v));
    }

    [Fact]
    public void AsSignal_LargeNumberOfEmissions()
    {
        var source = new Subject<int>();
        var count = 0;
        source.AsSignal().Subscribe(_ => count++);

        for (int i = 0; i < 1000; i++)
        {
            source.OnNext(i);
        }

        Assert.Equal(1000, count);
    }

    [Fact]
    public void AsSignal_CanBeChained()
    {
        var source = new Subject<int>();
        var values = new List<Unit>();

        source
            .Where(x => x > 5)
            .AsSignal()
            .Subscribe(values.Add);

        source.OnNext(1);
        source.OnNext(10);
        source.OnNext(3);
        source.OnNext(20);

        Assert.Equal(2, values.Count);
    }
}
