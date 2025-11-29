using R3;

namespace R3Ext.Activation;

/// <summary>
/// Extension methods for activatable view models.
/// </summary>
public static class ActivatableViewModelExtensions
{
    /// <summary>
    /// Executes a block when the view model is activated.
    /// The block receives a <see cref="DisposableBag"/> that is automatically disposed
    /// when the view model is deactivated.
    /// </summary>
    /// <param name="viewModel">The activatable view model.</param>
    /// <param name="block">
    /// The block to execute on activation. Add disposables to the bag
    /// to have them automatically disposed on deactivation.
    /// </param>
    /// <returns>
    /// An <see cref="IDisposable"/> that, when disposed, stops monitoring activation
    /// and disposes any active subscriptions.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The block can be called multiple times as the view model is activated and deactivated.
    /// Each time the view model is deactivated, all disposables added to the bag are disposed.
    /// </para>
    /// <para>
    /// View models are typically activated automatically when their associated view
    /// (implementing <see cref="IViewFor{TViewModel}"/>) is activated.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class MyViewModel : IActivatableViewModel
    /// {
    ///     public ViewModelActivator Activator { get; } = new();
    ///
    ///     public MyViewModel()
    ///     {
    ///         this.WhenActivated((ref DisposableBag disposables) =>
    ///         {
    ///             _dataService.GetItems()
    ///                 .Subscribe(items => Items = items)
    ///                 .AddTo(ref disposables);
    ///         });
    ///     }
    /// }
    /// </code>
    /// </example>
    public static IDisposable WhenActivated(
        this IActivatableViewModel viewModel,
        ActivationBlock block)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(block);

        var state = new ViewModelActivationState(block);
        var subscription = viewModel.Activator.Activation.Subscribe(new ViewModelActivationObserver(state));

        // When disposed, clean up both the subscription and any current activation state
        return Disposable.Combine(
            subscription,
            Disposable.Create(state, static s => s.CurrentBag.Dispose()));
    }

    /// <summary>
    /// State object for view model activation, avoiding closure allocations.
    /// </summary>
    private sealed class ViewModelActivationState
    {
        public ViewModelActivationState(ActivationBlock block)
        {
            Block = block;
        }

        public ActivationBlock Block { get; }

        public DisposableBag CurrentBag;
    }

    /// <summary>
    /// Observer that handles view model activation state changes.
    /// </summary>
    private sealed class ViewModelActivationObserver(ViewModelActivationState state) : Observer<ActivationState>
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
            // Log error but don't propagate - activation should be resilient
        }

        protected override void OnCompletedCore(Result result)
        {
            state.CurrentBag.Dispose();
        }
    }
}
