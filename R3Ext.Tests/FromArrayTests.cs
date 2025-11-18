using System.Threading.Tasks;
using R3;
using Xunit;

namespace R3Ext.Tests;

public class FromArrayTests
{
    [Fact]
    public async Task EmitsItemsAndCompletes()
    {
        var obs = CreationExtensions.FromArray(1, 2, 3, 4);
        var arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { 1, 2, 3, 4 }, arr);
    }

    [Fact]
    public async Task FromArray_Empty_CompletesImmediately()
    {
        var obs = CreationExtensions.FromArray<int>();
        var arr = await obs.ToArrayAsync();
        Assert.Empty(arr);
    }
}
