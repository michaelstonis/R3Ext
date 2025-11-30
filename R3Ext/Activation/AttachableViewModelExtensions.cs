using R3;

namespace R3Ext.Activation;

/// <summary>
/// Extension methods for attachable view models.
/// </summary>
public static class AttachableViewModelExtensions
{
    /// <summary>
    /// Executes a block when the view model is attached (view loaded).
    /// The block receives a <see cref="DisposableBag"/> that is automatically disposed
    /// when the view model is detached (view unloaded).
    /// </summary>
    /// <param name="viewModel">The attachable view model.</param>
    /// <param name="block">
    /// The block to execute on attachment. Add disposables to the bag
    /// to have them automatically disposed on detachment.
    /// </param>
    /// <returns>
    /// An <see cref="IDisposable"/> that, when disposed, stops monitoring attachment
    /// and disposes any active subscriptions.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The block can be called multiple times as the view model is attached and detached.
    /// Each time the view model is detached, all disposables added to the bag are disposed.
    /// </para>
    /// <para>
    /// View models are typically attached automatically when their associated view
    /// (implementing <see cref="IViewFor{TViewModel}"/>) is loaded.
    /// </para>
    /// <para>
    /// This differs from <see cref="ActivatableViewModelExtensions.WhenActivated"/> in that
    /// it responds to Loaded/Unloaded events rather than Appearing/Disappearing events.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class MyViewModel : IAttachableViewModel
    /// {
    ///     public ViewModelAttacher Attacher { get; } = new();
    ///
    ///     public MyViewModel()
    ///     {
    ///         this.WhenAttached((ref DisposableBag disposables) =>
    ///         {
    ///             _dataService.GetItems()
    ///                 .Subscribe(items => Items = items)
    ///                 .AddTo(ref disposables);
    ///         });
    ///     }
    /// }
    /// </code>
    /// </example>
    public static IDisposable WhenAttached(
        this IAttachableViewModel viewModel,
        ActivationBlock block)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(block);

        var state = new ViewModelAttachmentState(block);
        var subscription = viewModel.Attacher.Activation.Subscribe(new ViewModelAttachmentObserver(state));

        // When disposed, clean up both the subscription and any current attachment state
        return Disposable.Combine(
            subscription,
            Disposable.Create(state, static s => s.CurrentBag.Dispose()));
    }

    /// <summary>
    /// State object for view model attachment, avoiding closure allocations.
    /// </summary>
    private sealed class ViewModelAttachmentState
    {
        public ViewModelAttachmentState(ActivationBlock block)
        {
            Block = block;
        }

        public ActivationBlock Block { get; }

        public DisposableBag CurrentBag;
    }

    /// <summary>
    /// Observer that handles view model attachment state changes.
    /// </summary>
    private sealed class ViewModelAttachmentObserver(ViewModelAttachmentState state) : Observer<ActivationState>
    {
        protected override void OnNextCore(ActivationState activationState)
        {
            if (activationState == ActivationState.Activated)
            {
                state.CurrentBag = default;
                state.Block(ref state.CurrentBag);
            }
            else
            {
                state.CurrentBag.Dispose();
                state.CurrentBag = default;
            }
        }

        protected override void OnErrorResumeCore(Exception error)
        {
            // Log or handle error as appropriate
        }

        protected override void OnCompletedCore(Result result)
        {
            // Clean up on completion
            state.CurrentBag.Dispose();
            state.CurrentBag = default;
        }
    }
}
