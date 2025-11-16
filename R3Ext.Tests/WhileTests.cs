using System.Threading.Tasks;
using R3;
using Xunit;

namespace R3Ext.Tests;

public class WhileTests
{
    [Fact]
    public async Task RepeatsWhileConditionTrue()
    {
        int count = 0;
        bool Condition() => count++ < 3;

        var source = Observable.Return(7);
        var obs = source.While(Condition);
        var arr = await obs.ToArrayAsync();
        Assert.Equal(new[] { 7, 7, 7 }, arr);
    }

    [Fact]
    public void While_NullArgs_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => ReactivePortedExtensions.While<int>(null!, () => true));
        var src = Observable.Return(1);
        Assert.Throws<ArgumentNullException>(() => src.While(null!));
    }
}
