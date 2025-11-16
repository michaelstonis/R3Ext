using System.Threading.Tasks;
using R3;
using Xunit;

namespace R3Ext.Tests;

public class OnNextHelpersTests
{
    [Fact]
    public async Task ObserverParamsOnNextEmitsAll()
    {
        var obs = Observable.Create<int>(observer =>
        {
            observer.OnNext(1, 2, 3, 4, 5);
            observer.OnCompleted();
            return Disposable.Create(() => { });
        });

        var arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, arr);
    }

    [Fact]
    public async Task ObserverEnumerableOnNextEmitsAll()
    {
        var obs = Observable.Create<int>(observer =>
        {
            observer.OnNext(new[] { 10, 20, 30 });
            observer.OnCompleted();
            return Disposable.Create(() => { });
        });

        var arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { 10, 20, 30 }, arr);
    }
}
