using System;
using System.Threading.Tasks;
using R3;
using R3Ext;
using Xunit;

namespace R3Ext.Tests;

public class NullAndFilterTests
{
    [Fact]
    public async Task WhereIsNotNull_Class_FiltersNulls()
    {
        string?[] data = { "a", null, "b" };
        var obs = Observable.ToObservable(data).WhereIsNotNull();
        var arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { "a", "b" }, arr);
    }

    [Fact]
    public async Task WhereIsNotNull_Struct_FiltersNullables()
    {
        int?[] data = { 1, null, 3 };
        var obs = Observable.ToObservable(data).WhereIsNotNull();
        var arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { 1, 3 }, arr);
    }

    [Fact]
    public void Filter_NullPattern_Throws()
    {
        var src = ReactivePortedExtensions.FromArray("a", "bb");
        Assert.Throws<ArgumentNullException>(() => src.Filter(null!));
    }

    [Fact]
    public async Task Filter_Regex_Matches()
    {
        var src = ReactivePortedExtensions.FromArray("a", "bb", "ccc");
        var arr = await src.Filter("^b+").ToArrayAsync();
        Assert.Equal(new[] { "bb" }, arr);
    }
}
