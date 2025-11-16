using System.Threading.Tasks;
using R3;
using R3Ext;
using Xunit;

namespace R3Ext.Tests;

public class ForEachTests
{
    [Fact]
    public async Task ForEach_ExpandsArray()
    {
        var src = Observable.Return(new[] { 1, 2, 3 });
        var arr = await src.ForEach().ToArrayAsync();
        Assert.Equal(new[] { 1, 2, 3 }, arr);
    }

    [Fact]
    public async Task ForEach_ExpandsList()
    {
        var src = Observable.Return(new System.Collections.Generic.List<int> { 4, 5 });
        var arr = await src.ForEach().ToArrayAsync();
        Assert.Equal(new[] { 4, 5 }, arr);
    }
}
