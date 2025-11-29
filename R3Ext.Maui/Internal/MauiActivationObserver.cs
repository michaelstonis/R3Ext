using R3;
using R3Ext.Activation;

namespace R3Ext.Maui.Internal;

/// <summary>
/// Observer that handles MAUI activation state changes.
/// </summary>
internal sealed class MauiActivationObserver(MauiActivationState state) : Observer<ActivationState>
{
    protected override void OnNextCore(ActivationState activationState)
    {
        if (activationState == ActivationState.Activated)
        {
            // Activate view model if applicable
            state.VmActivationHandle = state.ViewModelActivator?.Activate();

            // Execute the block with a new disposable bag
            state.CurrentBag = default;
            state.Block(ref state.CurrentBag);
        }
        else
        {
            // Deactivate: dispose the bag and view model handle
            state.CurrentBag.Dispose();
            state.CurrentBag = default;

            state.VmActivationHandle?.Dispose();
            state.VmActivationHandle = null;
        }
    }

    protected override void OnErrorResumeCore(Exception error)
    {
        // Log error but don't propagate - activation should be resilient
    }

    protected override void OnCompletedCore(Result result)
    {
        // Ensure cleanup on completion
        state.CurrentBag.Dispose();
        state.VmActivationHandle?.Dispose();
    }
}
