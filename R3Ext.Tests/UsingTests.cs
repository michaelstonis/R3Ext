using R3;

namespace R3Ext.Tests;

public class UsingTests
{
    private sealed class TestResource : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    [Fact]
    public async Task DisposesResourceOnCompletion()
    {
        TestResource res = new();
        Observable<int> obs = CreationExtensions.Using(() => res, r => Observable.Return(42));
        int[] arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { 42, }, arr);
        Assert.True(res.Disposed);
    }

    [Fact]
    public void Using_NullFactories_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => CreationExtensions.Using<TestResource, int>(null!, _ => Observable.Return(1)));
        Assert.Throws<ArgumentNullException>(() => CreationExtensions.Using<TestResource, int>(() => new TestResource(), null!));
    }
}
