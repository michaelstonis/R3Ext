using System;
using System.Linq;
using System.Threading.Tasks;
using R3;
using R3Ext;
using Xunit;

namespace R3Ext.Tests;

public class CombiningPartitionSideEffectsTests
{
    [Fact]
    public async Task CombineLatestValuesAreAllTrue_Works()
    {
        var a = Observable.Return(true);
        var b = Observable.Return(true);
        var c = Observable.Return(false);
        var allTrue = new[] { a, b }.CombineLatestValuesAreAllTrue();
        var allFalse = new[] { c }.CombineLatestValuesAreAllFalse();
        Assert.True(await allTrue.FirstAsync());
        Assert.True(await allFalse.FirstAsync());
    }

    [Fact]
    public async Task Partition_SplitsByPredicate()
    {
        var src = ReactivePortedExtensions.FromArray(1, 2, 3, 4, 5);
        var (even, odd) = src.Partition(x => x % 2 == 0);
        var evenArr = await even.ToArrayAsync();
        var oddArr = await odd.ToArrayAsync();
        Assert.Equal(new[] { 2, 4 }, evenArr);
        Assert.Equal(new[] { 1, 3, 5 }, oddArr);
    }

    [Fact]
    public async Task DoOnSubscribe_And_DoOnDispose_Invoke()
    {
        bool subscribed = false;
        bool disposed = false;
        var obs = ReactivePortedExtensions.FromArray(1, 2, 3)
            .DoOnSubscribe(() => subscribed = true)
            .DoOnDispose(() => disposed = true);
        var arr = await obs.ToArrayAsync();
        Assert.True(subscribed);
        Assert.True(disposed);
        Assert.Equal(new[] { 1, 2, 3 }, arr);
    }

    [Fact]
    public async Task WaitUntil_TakesFirstMatching()
    {
        var arr = await ReactivePortedExtensions.FromArray(1, 2, 3, 4)
            .WaitUntil(x => x > 2)
            .ToArrayAsync();
        Assert.Equal(new[] { 3 }, arr);
    }

    [Fact]
    public async Task TakeUntil_Predicate_Inclusive()
    {
        var arr = await ReactivePortedExtensions.FromArray(1, 2, 3, 4)
            .TakeUntil(x => x >= 3)
            .ToArrayAsync();
        Assert.Equal(new[] { 1, 2, 3 }, arr);
    }
}
