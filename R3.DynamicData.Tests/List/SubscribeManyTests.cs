// Port of DynamicData to R3.

using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;
#pragma warning disable SA1516
#pragma warning disable SA1503

public class SubscribeManyTests
{
    private sealed class Trackable : IDisposable
    {
        public int Id { get; }
        public bool Disposed { get; private set; }

        public Trackable(int id)
        {
            Id = id;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    [Fact]
    public void SubscribeMany_SubscribesAndDisposesPerItem()
    {
        var list = new SourceList<Trackable>();
        var subscriptions = new List<IDisposable>();

        var sub = list.Connect()
            .SubscribeMany(t =>
            {
                var d = Disposable.Create(() => t.Dispose());
                subscriptions.Add(d);
                return d;
            })
            .Subscribe(_ => { });

        var a = new Trackable(1);
        var b = new Trackable(2);
        list.AddRange(new[] { a, b });

        // Remove 'a' -> its subscription disposed
        list.RemoveAt(0);
        Assert.True(a.Disposed);
        Assert.False(b.Disposed);

        // Clear -> remaining disposed
        list.Clear();
        Assert.True(b.Disposed);

        sub.Dispose();
        foreach (var d in subscriptions)
        {
            d.Dispose();
        }
    }
}
