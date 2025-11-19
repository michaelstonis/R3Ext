using R3;

namespace R3Ext.Tests;

public class StartHelpersTests
{
    [Fact]
    public async Task Start_Action_EmitsUnit()
    {
        bool called = false;
        Unit[] arr = await CreationExtensions.Start(() => { called = true; }).ToArrayAsync();
        Assert.True(called);
        Assert.Equal(new[] { Unit.Default, }, arr);
    }

    [Fact]
    public async Task Start_Func_EmitsResult()
    {
        int[] arr = await CreationExtensions.Start(() => 123).ToArrayAsync();
        Assert.Equal(new[] { 123, }, arr);
    }
}
