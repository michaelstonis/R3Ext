using R3;
using System;
using System.Threading.Tasks;
using Xunit;

namespace R3Ext.Tests;

public class InteractionTests
{
    [Fact]
    public async Task Sync_Handler_Produces_Output()
    {
        var interaction = new Interaction<string, int>();

        using var _ = interaction.RegisterHandler(ctx =>
        {
            ctx.SetOutput(ctx.Input.Length);
        });

        var result = await interaction.Handle("hello").FirstAsync();
        Assert.Equal(5, result);
    }

    [Fact]
    public async Task Task_Handler_Produces_Output()
    {
        var interaction = new Interaction<int, int>();

        using var _ = interaction.RegisterHandler(async ctx =>
        {
            await Task.Delay(1);
            ctx.SetOutput(ctx.Input * 2);
        });

        var result = await interaction.Handle(21).FirstAsync();
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Observable_Handler_Produces_Output()
    {
        var interaction = new Interaction<Unit, string>();

        using var _ = interaction.RegisterHandler(_ =>
        {
            return Observable.Timer(TimeSpan.FromMilliseconds(1))
                .Select(_ => Unit.Default)
                .Do(onCompleted: _ => { });
        });

        // Register a real handler second to ensure reverse order takes it first
        using var __ = interaction.RegisterHandler(ctx =>
        {
            ctx.SetOutput("ok");
        });

        var result = await interaction.Handle(Unit.Default).FirstAsync();
        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Reverse_Order_Invokes_Latest_First()
    {
        var calls = new System.Collections.Generic.List<string>();
        var interaction = new Interaction<int, int>();

        using var a = interaction.RegisterHandler(ctx =>
        {
            calls.Add("A");
            // do not set output, not handled
        });

        using var b = interaction.RegisterHandler(ctx =>
        {
            calls.Add("B");
            ctx.SetOutput(ctx.Input + 1);
        });

        var result = await interaction.Handle(1).FirstAsync();
        Assert.Equal(2, result);
        Assert.Equal(new[] { "B", "A" }, calls);
    }

    [Fact]
    public async Task Unhandled_Throws_Exception()
    {
        var interaction = new Interaction<string, int>();
        await Assert.ThrowsAsync<UnhandledInteractionException<string, int>>(async () =>
        {
            _ = await interaction.Handle("x").FirstAsync();
        });
    }

    [Fact]
    public async Task Disposing_Registration_Unregisters_Handler()
    {
        var interaction = new Interaction<Unit, bool>();

        var registration = interaction.RegisterHandler(ctx =>
        {
            ctx.SetOutput(true);
        });

        registration.Dispose();

        await Assert.ThrowsAsync<UnhandledInteractionException<Unit, bool>>(async () =>
        {
            _ = await interaction.Handle(Unit.Default).FirstAsync();
        });
    }
}
