using System.Threading.Tasks;
using R3;
using Xunit;

namespace R3Ext.Tests;

public class StartHelpersTests
{
    [Fact]
    public async Task Start_Action_EmitsUnit()
    {
        bool called = false;
        var arr = await ReactivePortedExtensions.Start(() => { called = true; }).ToArrayAsync();
        Assert.True(called);
        Assert.Equal(new[] { Unit.Default }, arr);
    }

    [Fact]
    public async Task Start_Func_EmitsResult()
    {
        var arr = await ReactivePortedExtensions.Start(() => 123).ToArrayAsync();
        Assert.Equal(new[] { 123 }, arr);
    }
}
