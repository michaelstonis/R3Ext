using R3;
using R3Ext.Activation;

namespace R3Ext.Maui.Internal;

/// <summary>
/// State object for MAUI activation, avoiding closure allocations.
/// </summary>
internal sealed class MauiActivationState
{
    public MauiActivationState(ActivationBlock block, ViewModelActivator? vmActivator)
    {
        Block = block;
        ViewModelActivator = vmActivator;
    }

    public ActivationBlock Block { get; }

    public ViewModelActivator? ViewModelActivator { get; }

    public DisposableBag CurrentBag;

    public IDisposable? VmActivationHandle;
}
