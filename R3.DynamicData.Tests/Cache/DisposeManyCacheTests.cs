// Port of DynamicData to R3.
#pragma warning disable SA1516, SA1515, SA1503, SA1502, SA1107

using R3.DynamicData.Cache;

namespace R3.DynamicData.Tests.Cache;

public class DisposeManyCacheTests
{
    private sealed class Trackable : IDisposable
    {
        public int Id { get; }
        public bool Disposed { get; private set; }
        public Trackable(int id) => Id = id;
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void DisposeMany_DisposesOnRemoveUpdateAndClear()
    {
        var cache = new SourceCache<Trackable, int>(t => t.Id);
        var disposed = new List<int>();

        using var sub = cache.Connect()
            .DisposeMany<Trackable, int>(t =>
            {
                t.Dispose();
                disposed.Add(t.Id);
            })
            .Subscribe(_ => { });

        var a = new Trackable(1);
        var b = new Trackable(2);
        cache.AddOrUpdate(a);
        cache.AddOrUpdate(b);

        // Update a
        var a2 = new Trackable(1);
        cache.AddOrUpdate(a2); // should dispose a
        Assert.Contains(1, disposed);
        Assert.False(b.Disposed);

        // Remove b
        cache.Remove(2);
        Assert.True(b.Disposed);

        // Clear remaining (a2)
        cache.Clear();
        Assert.True(a2.Disposed);
    }
}
