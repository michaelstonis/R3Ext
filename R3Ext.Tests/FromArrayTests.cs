using R3;

namespace R3Ext.Tests;

public class FromArrayTests
{
    [Fact]
    public async Task EmitsItemsAndCompletes()
    {
        Observable<int> obs = CreationExtensions.FromArray(1, 2, 3, 4);
        int[] arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { 1, 2, 3, 4, }, arr);
    }

    [Fact]
    public async Task FromArray_Empty_CompletesImmediately()
    {
        Observable<int> obs = CreationExtensions.FromArray<int>();
        int[] arr = await obs.ToArrayAsync();
        Assert.Empty(arr);
    }
}
