using System;
using System.Threading.Tasks;
using R3;
using Xunit;

namespace R3Ext.Tests;

public class UsingTests
{
    private sealed class TestResource : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public async Task DisposesResourceOnCompletion()
    {
        var res = new TestResource();
        var obs = ReactivePortedExtensions.Using(() => res, r => Observable.Return(42));
        var arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { 42 }, arr);
        Assert.True(res.Disposed);
    }

    [Fact]
    public void Using_NullFactories_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => ReactivePortedExtensions.Using<TestResource, int>(null!, _ => Observable.Return(1)));
        Assert.Throws<ArgumentNullException>(() => ReactivePortedExtensions.Using<TestResource, int>(() => new TestResource(), null!));
    }
}
