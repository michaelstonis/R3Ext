using R3;

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
    public async Task AsSignal_EmitsUnitForEachSourceValue()
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
    public async Task AsSignal_PreservesEmissionTiming()
    {
        var source = new Subject<string>();
        var timestamps = new List<DateTime>();
        source.AsSignal().Subscribe(_ => timestamps.Add(DateTime.UtcNow));

        var start = DateTime.UtcNow;
        source.OnNext("a");
        await Task.Delay(50);
        source.OnNext("b");
        await Task.Delay(50);
        source.OnNext("c");

        Assert.Equal(3, timestamps.Count);
        Assert.True(timestamps[1] - timestamps[0] >= TimeSpan.FromMilliseconds(40));
        Assert.True(timestamps[2] - timestamps[1] >= TimeSpan.FromMilliseconds(40));
    }

    [Fact]
    public async Task AsSignal_WorksWithReferenceTypes()
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
    public async Task AsSignal_WorksWithValueTypes()
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
    public async Task AsSignal_WorksWithStructTypes()
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
    public async Task AsSignal_LargeNumberOfEmissions()
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
    public async Task AsSignal_CanBeChained()
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
