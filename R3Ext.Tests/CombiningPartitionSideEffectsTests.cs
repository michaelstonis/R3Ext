using R3;

namespace R3Ext.Tests;

public class CombiningPartitionSideEffectsTests
{
    [Fact]
    public async Task CombineLatestValuesAreAllTrue_Works()
    {
        Observable<bool> a = Observable.Return(true);
        Observable<bool> b = Observable.Return(true);
        Observable<bool> c = Observable.Return(false);
        Observable<bool> allTrue = new[] { a, b, }.CombineLatestValuesAreAllTrue();
        Observable<bool> allFalse = new[] { c, }.CombineLatestValuesAreAllFalse();
        Assert.True(await allTrue.FirstAsync());
        Assert.True(await allFalse.FirstAsync());
    }

    [Fact]
    public async Task Partition_SplitsByPredicate()
    {
        Observable<int> src = CreationExtensions.FromArray(1, 2, 3, 4, 5);
        (Observable<int> even, Observable<int> odd) = src.Partition(x => x % 2 == 0);
        int[] evenArr = await even.ToArrayAsync();
        int[] oddArr = await odd.ToArrayAsync();
        Assert.Equal(new[] { 2, 4, }, evenArr);
        Assert.Equal(new[] { 1, 3, 5, }, oddArr);
    }

    [Fact]
    public async Task DoOnSubscribe_And_DoOnDispose_Invoke()
    {
        bool subscribed = false;
        bool disposed = false;
        Observable<int> obs = CreationExtensions.FromArray(1, 2, 3)
            .DoOnSubscribe(() => subscribed = true)
            .DoOnDispose(() => disposed = true);
        int[] arr = await obs.ToArrayAsync();
        Assert.True(subscribed);
        Assert.True(disposed);
        Assert.Equal(new[] { 1, 2, 3, }, arr);
    }

    [Fact]
    public async Task WaitUntil_TakesFirstMatching()
    {
        int[] arr = await CreationExtensions.FromArray(1, 2, 3, 4)
            .WaitUntil(x => x > 2)
            .ToArrayAsync();
        Assert.Equal(new[] { 3, }, arr);
    }

    [Fact]
    public async Task TakeUntil_Predicate_Inclusive()
    {
        int[] arr = await CreationExtensions.FromArray(1, 2, 3, 4)
            .TakeUntil(x => x >= 3)
            .ToArrayAsync();
        Assert.Equal(new[] { 1, 2, 3, }, arr);
    }
}
