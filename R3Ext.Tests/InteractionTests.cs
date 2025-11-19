using R3;

namespace R3Ext.Tests;

public class InteractionTests
{
    [Fact]
    public async Task Sync_Handler_Produces_Output()
    {
        Interaction<string, int> interaction = new();

        using IDisposable _ = interaction.RegisterHandler(ctx => { ctx.SetOutput(ctx.Input.Length); });

        int result = await interaction.Handle("hello").FirstAsync();
        Assert.Equal(5, result);
    }

    [Fact]
    public async Task Task_Handler_Produces_Output()
    {
        Interaction<int, int> interaction = new();

        using IDisposable _ = interaction.RegisterHandler(async ctx =>
        {
            await Task.Delay(1);
            ctx.SetOutput(ctx.Input * 2);
        });

        int result = await interaction.Handle(21).FirstAsync();
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Observable_Handler_Produces_Output()
    {
        Interaction<Unit, string> interaction = new();

        using IDisposable _ = interaction.RegisterHandler(_ =>
        {
            return Observable.Timer(TimeSpan.FromMilliseconds(1))
                .Select(_ => Unit.Default)
                .Do(onCompleted: _ => { });
        });

        // Register a real handler second to ensure reverse order takes it first
        using IDisposable __ = interaction.RegisterHandler(ctx => { ctx.SetOutput("ok"); });

        string result = await interaction.Handle(Unit.Default).FirstAsync();
        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Reverse_Order_Invokes_Latest_First()
    {
        List<string> calls = new();
        Interaction<int, int> interaction = new();

        using IDisposable a = interaction.RegisterHandler(ctx =>
        {
            calls.Add("A");

            // do not set output, not handled
        });

        using IDisposable b = interaction.RegisterHandler(ctx =>
        {
            calls.Add("B");
            ctx.SetOutput(ctx.Input + 1);
        });

        int result = await interaction.Handle(1).FirstAsync();
        Assert.Equal(2, result);
        Assert.Equal(new[] { "B", "A", }, calls);
    }

    [Fact]
    public async Task Unhandled_Throws_Exception()
    {
        Interaction<string, int> interaction = new();
        await Assert.ThrowsAsync<UnhandledInteractionException<string, int>>(async () => { _ = await interaction.Handle("x").FirstAsync(); });
    }

    [Fact]
    public async Task Disposing_Registration_Unregisters_Handler()
    {
        Interaction<Unit, bool> interaction = new();

        IDisposable registration = interaction.RegisterHandler(ctx => { ctx.SetOutput(true); });

        registration.Dispose();

        await Assert.ThrowsAsync<UnhandledInteractionException<Unit, bool>>(async () => { _ = await interaction.Handle(Unit.Default).FirstAsync(); });
    }
}
