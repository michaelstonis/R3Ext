using R3;

namespace R3Ext.Tests;

public class AsSignalAndBoolTests
{
    [Fact]
    public async Task AsSignal_EmitsUnitForEachValue()
    {
        Observable<Unit> obs = CreationExtensions.FromArray(10, 20, 30).AsSignal();
        Unit[] arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { Unit.Default, Unit.Default, Unit.Default, }, arr);
    }

    [Fact]
    public void Not_NullSource_Throws()
    {
        Observable<bool>? source = null;
        Assert.Throws<ArgumentNullException>(() => FilteringExtensions.Not(source!));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Not_Inverts(bool input, bool expected)
    {
        Observable<bool> obs = Observable.Return(input).Not();
        bool val = await obs.FirstAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(expected, val);
    }

    [Fact]
    public async Task WhereTrue_Filters()
    {
        Observable<bool> obs = CreationExtensions.FromArray(true, false, true).WhereTrue();
        bool[] arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { true, true, }, arr);
    }

    [Fact]
    public async Task WhereFalse_Filters()
    {
        Observable<bool> obs = CreationExtensions.FromArray(true, false, false).WhereFalse();
        bool[] arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { false, false, }, arr);
    }
}
