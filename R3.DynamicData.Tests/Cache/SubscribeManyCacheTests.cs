// Port of DynamicData to R3.

using R3.DynamicData.Cache;

namespace R3.DynamicData.Tests.Cache;

public class SubscribeManyCacheTests
{
    private sealed class Trackable : IDisposable
    {
        public int Id { get; }
        public bool Disposed { get; private set; }
        public Trackable(int id) => Id = id;
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void SubscribeMany_DisposesOnRemoveAndClear()
    {
        var cache = new SourceCache<Trackable, int>(t => t.Id);
        var subscriptions = new List<IDisposable>();

        using var sub = cache.Connect()
            .SubscribeMany(t =>
            {
                var d = Disposable.Create(() => t.Dispose());
                subscriptions.Add(d);
                return d;
            })
            .Subscribe(_ => { });

        var a = new Trackable(1);
        var b = new Trackable(2);
        cache.AddOrUpdate(a);
        cache.AddOrUpdate(b);

        // Remove a
        cache.Remove(1);
        Assert.True(a.Disposed);
        Assert.False(b.Disposed);

        // Clear
        cache.Clear();
        Assert.True(b.Disposed);

        sub.Dispose();
        foreach (var d in subscriptions) d.Dispose();
    }
}
