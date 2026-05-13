// Port of DynamicData to R3.
#pragma warning disable SA1516, SA1515, SA1503, SA1502, SA1107

using System.Runtime.CompilerServices;
using R3.DynamicData.Cache;

namespace R3.DynamicData.Tests.Cache;

public class AsyncDisposeManyTests
{
    private sealed class AsyncTracker : IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task AsyncDisposeMany_DisposesOnRemove()
    {
        var source = new SourceCache<AsyncTracker, int>(x => RuntimeHelpers.GetHashCode(x));
        var t1 = new AsyncTracker();
        var t2 = new AsyncTracker();
        using var sub = source.Connect().AsyncDisposeMany().Subscribe(_ => { });
        source.AddOrUpdate(new[] { t1, t2 });
        source.Remove(RuntimeHelpers.GetHashCode(t1));
        await Task.Delay(100);
        Assert.True(t1.IsDisposed);
        Assert.False(t2.IsDisposed);
    }

    [Fact]
    public async Task AsyncDisposeMany_DisposesOnClear()
    {
        var source = new SourceCache<AsyncTracker, int>(x => RuntimeHelpers.GetHashCode(x));
        var t1 = new AsyncTracker();
        var t2 = new AsyncTracker();
        using var sub = source.Connect().AsyncDisposeMany().Subscribe(_ => { });
        source.AddOrUpdate(new[] { t1, t2 });
        source.Clear();
        await Task.Delay(100);
        Assert.True(t1.IsDisposed);
        Assert.True(t2.IsDisposed);
    }

    [Fact]
    public async Task AsyncDisposeMany_DisposesOldValueOnUpdate()
    {
        var source = new SourceCache<AsyncTracker, int>(x => RuntimeHelpers.GetHashCode(x));
        var t1 = new AsyncTracker();
        var t2 = new AsyncTracker();
        using var sub = source.Connect().AsyncDisposeMany().Subscribe(_ => { });
        source.AddOrUpdate(t1);
        source.AddOrUpdate(t2);
        source.Remove(RuntimeHelpers.GetHashCode(t1));
        await Task.Delay(100);
        Assert.True(t1.IsDisposed);
        Assert.False(t2.IsDisposed);
    }

    [Fact]
    public async Task AsyncDisposeMany_DisposesRemainingOnUnsubscribe()
    {
        var source = new SourceCache<AsyncTracker, int>(x => RuntimeHelpers.GetHashCode(x));
        var t1 = new AsyncTracker();
        var sub = source.Connect().AsyncDisposeMany().Subscribe(_ => { });
        source.AddOrUpdate(t1);
        sub.Dispose();
        await Task.Delay(100);
        Assert.True(t1.IsDisposed);
    }

    [Fact]
    public void AsyncDisposeMany_ThrowsOnNullSource()
    {
        Observable<IChangeSet<AsyncTracker, int>>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.AsyncDisposeMany());
    }
}
