// Port of DynamicData to R3.
#pragma warning disable SA1516, SA1515, SA1503, SA1502, SA1107

using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

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
        var source = new SourceList<AsyncTracker>();
        var t1 = new AsyncTracker();
        var t2 = new AsyncTracker();
        using var sub = source.Connect().AsyncDisposeMany().Subscribe(_ => { });
        source.AddRange(new[] { t1, t2 });
        source.Remove(t1);
        await Task.Delay(100);
        Assert.True(t1.IsDisposed);
        Assert.False(t2.IsDisposed);
    }

    [Fact]
    public async Task AsyncDisposeMany_DisposesOnClear()
    {
        var source = new SourceList<AsyncTracker>();
        var t1 = new AsyncTracker();
        var t2 = new AsyncTracker();
        using var sub = source.Connect().AsyncDisposeMany().Subscribe(_ => { });
        source.AddRange(new[] { t1, t2 });
        source.Clear();
        await Task.Delay(100);
        Assert.True(t1.IsDisposed);
        Assert.True(t2.IsDisposed);
    }

    [Fact]
    public async Task AsyncDisposeMany_DisposesRemainingOnUnsubscribe()
    {
        var source = new SourceList<AsyncTracker>();
        var t1 = new AsyncTracker();
        var sub = source.Connect().AsyncDisposeMany().Subscribe(_ => { });
        source.Add(t1);
        sub.Dispose();
        await Task.Delay(100);
        Assert.True(t1.IsDisposed);
    }

    [Fact]
    public void AsyncDisposeMany_ThrowsOnNullSource()
    {
        Observable<IChangeSet<AsyncTracker>>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.AsyncDisposeMany());
    }
}
