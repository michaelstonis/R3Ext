using R3;
using R3Ext.Activation;

namespace R3Ext.Maui.Internal;

/// <summary>
/// State object for MAUI attachment, avoiding closure allocations.
/// Supports late-bound ViewModels by using a delegate to fetch the attacher.
/// </summary>
internal sealed class MauiAttachmentState
{
    public MauiAttachmentState(ActivationBlock block, Func<ViewModelAttacher?>? getAttacher)
    {
        Block = block;
        GetAttacher = getAttacher;
    }

    public ActivationBlock Block { get; }

    /// <summary>
    /// Gets the delegate to fetch the current ViewModelAttacher.
    /// Called on each attachment to support late-bound ViewModels.
    /// </summary>
    public Func<ViewModelAttacher?>? GetAttacher { get; }

    public DisposableBag CurrentBag;

    public IDisposable? VmAttachmentHandle;
}
