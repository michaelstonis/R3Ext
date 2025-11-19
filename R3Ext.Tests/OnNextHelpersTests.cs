using R3;

namespace R3Ext.Tests;

public class OnNextHelpersTests
{
    [Fact]
    public async Task ObserverParamsOnNextEmitsAll()
    {
        Observable<int> obs = Observable.Create<int>(observer =>
        {
            observer.OnNext(1, 2, 3, 4, 5);
            observer.OnCompleted();
            return Disposable.Create(() => { });
        });

        int[] arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { 1, 2, 3, 4, 5, }, arr);
    }

    [Fact]
    public async Task ObserverEnumerableOnNextEmitsAll()
    {
        Observable<int> obs = Observable.Create<int>(observer =>
        {
            observer.OnNext(new[] { 10, 20, 30, });
            observer.OnCompleted();
            return Disposable.Create(() => { });
        });

        int[] arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { 10, 20, 30, }, arr);
    }
}
