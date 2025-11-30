using R3;
using R3Ext.Activation;

namespace R3Ext.Maui.Internal;

/// <summary>
/// Observer that handles MAUI attachment state changes (Loaded/Unloaded events).
/// Supports late-bound ViewModels by fetching the attacher on each attachment.
/// </summary>
internal sealed class MauiAttachmentObserver(MauiAttachmentState state) : Observer<ActivationState>
{
    protected override void OnNextCore(ActivationState activationState)
    {
        if (activationState == ActivationState.Activated)
        {
            // Get the current attacher (supports late-bound ViewModels)
            ViewModelAttacher? attacher = state.GetAttacher?.Invoke();
            state.VmAttachmentHandle = attacher?.Attach();

            // Execute the block with a new disposable bag
            state.CurrentBag = default;
            state.Block(ref state.CurrentBag);
        }
        else
        {
            // Detach: dispose the bag and view model handle
            state.CurrentBag.Dispose();
            state.CurrentBag = default;

            state.VmAttachmentHandle?.Dispose();
            state.VmAttachmentHandle = null;
        }
    }

    protected override void OnErrorResumeCore(Exception error)
    {
        // Log error but don't propagate - attachment should be resilient
    }

    protected override void OnCompletedCore(Result result)
    {
        // Ensure cleanup on completion
        state.CurrentBag.Dispose();
        state.VmAttachmentHandle?.Dispose();
    }
}
