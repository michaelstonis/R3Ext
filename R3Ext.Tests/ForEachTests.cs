using R3;

namespace R3Ext.Tests;

public class ForEachTests
{
    [Fact]
    public async Task ForEach_ExpandsArray()
    {
        Observable<int[]> src = Observable.Return(new[] { 1, 2, 3, });
        int[] arr = await src.ForEach().ToArrayAsync();
        Assert.Equal(new[] { 1, 2, 3, }, arr);
    }

    [Fact]
    public async Task ForEach_ExpandsList()
    {
        Observable<List<int>> src = Observable.Return(new List<int> { 4, 5, });
        int[] arr = await src.ForEach().ToArrayAsync();
        Assert.Equal(new[] { 4, 5, }, arr);
    }
}
