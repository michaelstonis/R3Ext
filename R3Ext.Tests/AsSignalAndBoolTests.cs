using System;
using System.Threading.Tasks;
using R3;
using Xunit;

namespace R3Ext.Tests;

public class AsSignalAndBoolTests
{
    [Fact]
    public async Task AsSignal_EmitsUnitForEachValue()
    {
        var obs = CreationExtensions.FromArray(10, 20, 30).AsSignal();
        var arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { Unit.Default, Unit.Default, Unit.Default }, arr);
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
        var obs = Observable.Return(input).Not();
        var val = await obs.FirstAsync();
        Assert.Equal(expected, val);
    }

    [Fact]
    public async Task WhereTrue_Filters()
    {
        var obs = CreationExtensions.FromArray(true, false, true).WhereTrue();
        var arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { true, true }, arr);
    }

    [Fact]
    public async Task WhereFalse_Filters()
    {
        var obs = CreationExtensions.FromArray(true, false, false).WhereFalse();
        var arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { false, false }, arr);
    }
}
