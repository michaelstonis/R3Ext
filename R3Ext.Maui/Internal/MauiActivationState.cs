using R3;
using R3Ext.Activation;

namespace R3Ext.Maui.Internal;

/// <summary>
/// State object for MAUI activation, avoiding closure allocations.
/// Supports late-bound ViewModels by using a delegate to fetch the activator.
/// </summary>
internal sealed class MauiActivationState
{
    public MauiActivationState(ActivationBlock block, Func<ViewModelActivator?>? getActivator)
    {
        Block = block;
        GetActivator = getActivator;
    }

    public ActivationBlock Block { get; }

    /// <summary>
    /// Gets the delegate to fetch the current ViewModelActivator.
    /// Called on each activation to support late-bound ViewModels.
    /// </summary>
    public Func<ViewModelActivator?>? GetActivator { get; }

    public DisposableBag CurrentBag;

    public IDisposable? VmActivationHandle;
}
