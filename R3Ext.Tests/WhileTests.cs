using R3;

namespace R3Ext.Tests;

public class WhileTests
{
    [Fact]
    public async Task RepeatsWhileConditionTrue()
    {
        int count = 0;

        bool Condition()
        {
            return count++ < 3;
        }

        Observable<int> source = Observable.Return(7);
        Observable<int> obs = source.While(Condition);
        int[] arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { 7, 7, 7, }, arr);
    }

    [Fact]
    public void While_NullArgs_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => FilteringExtensions.While<int>(null!, () => true));
        Observable<int> src = Observable.Return(1);
        Assert.Throws<ArgumentNullException>(() => src.While(null!));
    }
}
