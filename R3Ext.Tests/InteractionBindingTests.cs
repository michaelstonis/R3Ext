using R3;

namespace R3Ext.Tests;

public class InteractionBindingTests
{
    public sealed class Vm : RxObject
    {
        private Interaction<string, int>? _askLength;

        public Interaction<string, int>? AskLength
        {
            get => _askLength;
            set => this.RaiseAndSetIfChanged(ref _askLength, value);
        }

        // Trigger generator to create WhenChanged for AskLength
        internal void RegisterBinding()
        {
            this.WhenChanged(v => v.AskLength).Subscribe(_ => { });
        }
    }

    [Fact]
    public async Task Registers_And_Handles_Initial_Instance()
    {
        Vm vm = new() { AskLength = new Interaction<string, int>(), };
        using IDisposable binding = vm.BindInteraction(v => v.AskLength, async ctx =>
        {
            await Task.Yield();
            ctx.SetOutput(ctx.Input.Length);
        });

        int result = await vm.AskLength!.Handle("foo").FirstAsync();
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task Swaps_Handler_On_New_Instance()
    {
        Vm vm = new() { AskLength = new Interaction<string, int>(), };
        int handled = 0;
        using IDisposable binding = vm.BindInteraction(v => v.AskLength, ctx =>
        {
            handled++;
            ctx.SetOutput(ctx.Input.Length);
        });

        int r1 = await vm.AskLength!.Handle("abcd").FirstAsync();
        Assert.Equal(4, r1);
        Assert.Equal(1, handled);

        vm.AskLength = new Interaction<string, int>();
        await Task.Yield(); // Let the property change notification propagate through the observable pipeline
        int r2 = await vm.AskLength!.Handle("xyz").FirstAsync();
        Assert.Equal(3, r2);
        Assert.Equal(2, handled);
    }

    [Fact]
    public async Task Null_Instance_Unregisters_Handler()
    {
        Vm vm = new() { AskLength = new Interaction<string, int>(), };
        using IDisposable binding = vm.BindInteraction(v => v.AskLength, ctx => ctx.SetOutput(ctx.Input.Length));
        Interaction<string, int>? old = vm.AskLength!;
        vm.AskLength = null;
        binding.Dispose();
        await Assert.ThrowsAsync<UnhandledInteractionException<string, int>>(async () => { _ = await old.Handle("fail").FirstAsync(); });
    }

    [Fact]
    public async Task Observable_Handler_Variant_Works()
    {
        Vm vm = new() { AskLength = new Interaction<string, int>(), };
        using IDisposable binding = vm.BindInteraction(v => v.AskLength, ctx =>
        {
            // Simulate async before setting output
            return Observable.Timer(TimeSpan.FromMilliseconds(1)).Do(onCompleted: _ => ctx.SetOutput(ctx.Input.Length));
        });

        int result = await vm.AskLength!.Handle("length").FirstAsync();
        Assert.Equal(6, result);
    }

    [Fact]
    public async Task Disposing_Binding_Unregisters_Handler()
    {
        Vm vm = new() { AskLength = new Interaction<string, int>(), };
        IDisposable binding = vm.BindInteraction(v => v.AskLength, ctx => ctx.SetOutput(ctx.Input.Length));
        binding.Dispose();

        await Assert.ThrowsAsync<UnhandledInteractionException<string, int>>(async () => { _ = await vm.AskLength!.Handle("no").FirstAsync(); });
    }
}
