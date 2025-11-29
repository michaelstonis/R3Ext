using R3;

namespace R3Ext.Activation;

/// <summary>
/// Delegate for activation blocks that receive a disposable bag by reference.
/// </summary>
/// <param name="disposables">The disposable bag to add subscriptions to.</param>
public delegate void ActivationBlock(ref DisposableBag disposables);

/// <summary>
/// Extension methods for activatable views.
/// </summary>
public static class ActivatableViewExtensions
{
    /// <summary>
    /// Executes a block when the view is activated (visibility-based).
    /// The block receives a <see cref="DisposableBag"/> that is automatically disposed
    /// when the view is deactivated.
    /// </summary>
    /// <param name="view">The activatable view.</param>
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
    /// This method uses the <see cref="ActivationTrigger.Visibility"/> trigger,
    /// which maps to Appearing/Disappearing on MAUI Pages, IsVisible changes on Views, etc.
    /// </para>
    /// <para>
    /// The block can be called multiple times as the view is activated and deactivated.
    /// Each time the view is deactivated, all disposables added to the bag are disposed.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// this.WhenActivated((ref DisposableBag disposables) =>
    /// {
    ///     viewModel.Items
    ///         .Subscribe(items => UpdateList(items))
    ///         .AddTo(ref disposables);
    /// });
    /// </code>
    /// </example>
    public static IDisposable WhenActivated(
        this IActivatableView view,
        ActivationBlock block)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(block);

        var state = new ViewActivationState(block, null);
        var subscription = view.Activation.Subscribe(new ViewActivationObserver(state));

        // When disposed, clean up both the subscription and any current activation state
        return Disposable.Combine(
            subscription,
            Disposable.Create(state, static s =>
            {
                s.CurrentBag.Dispose();
                s.VmActivationHandle?.Dispose();
            }));
    }

    /// <summary>
    /// Executes a block when the view is activated, with automatic view model activation.
    /// </summary>
    /// <typeparam name="TViewModel">The view model type.</typeparam>
    /// <param name="view">The view implementing IViewFor.</param>
    /// <param name="block">The block to execute on activation.</param>
    /// <returns>An <see cref="IDisposable"/> that stops monitoring when disposed.</returns>
    public static IDisposable WhenActivated<TViewModel>(
        this IViewFor<TViewModel> view,
        ActivationBlock block)
        where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(block);

        var vmActivator = GetViewModelActivator(view);
        var state = new ViewActivationState(block, vmActivator);
        var subscription = view.Activation.Subscribe(new ViewActivationObserver(state));

        return Disposable.Combine(
            subscription,
            Disposable.Create(state, static s =>
            {
                s.CurrentBag.Dispose();
                s.VmActivationHandle?.Dispose();
            }));
    }

    /// <summary>
    /// Executes a block when the view is attached to the UI hierarchy.
    /// The block receives a <see cref="DisposableBag"/> that is automatically disposed
    /// when the view is detached.
    /// </summary>
    /// <param name="view">The activatable view.</param>
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
    /// This method uses the <see cref="ActivationTrigger.Attached"/> trigger,
    /// which maps to Loaded/Unloaded on MAUI, AttachedToVisualTree on Avalonia, etc.
    /// </para>
    /// <para>
    /// Use this method when you need to respond to the view being physically added
    /// to or removed from the visual tree, rather than visibility changes.
    /// </para>
    /// <para>
    /// Platform packages can provide an AttachedActivation observable on views.
    /// If not available, this falls back to the standard Activation observable.
    /// </para>
    /// </remarks>
    public static IDisposable WhenAttached(
        this IActivatableView view,
        ActivationBlock block)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(block);

        // Platform packages can provide a separate Attached observable
        // For now, use the standard activation - platforms will override
        var activation = (view as IAttachableView)?.AttachedActivation ?? view.Activation;
        var state = new ViewActivationState(block, null);
        var subscription = activation.Subscribe(new ViewActivationObserver(state));

        return Disposable.Combine(
            subscription,
            Disposable.Create(state, static s =>
            {
                s.CurrentBag.Dispose();
                s.VmActivationHandle?.Dispose();
            }));
    }

    private static ViewModelActivator? GetViewModelActivator<TViewModel>(IViewFor<TViewModel> view)
        where TViewModel : class
    {
        // No reflection - direct type checking
        if (!view.AutoActivateViewModel)
        {
            return null;
        }

        return (view.ViewModel as IActivatableViewModel)?.Activator;
    }

    /// <summary>
    /// State object for activation, avoiding closure allocations.
    /// </summary>
    private sealed class ViewActivationState
    {
        public ViewActivationState(ActivationBlock block, ViewModelActivator? vmActivator)
        {
            Block = block;
            ViewModelActivator = vmActivator;
        }

        public ActivationBlock Block { get; }

        public ViewModelActivator? ViewModelActivator { get; }

        public DisposableBag CurrentBag;

        public IDisposable? VmActivationHandle;
    }

    /// <summary>
    /// Observer that handles activation state changes.
    /// Using a class observer avoids lambda closure allocations on the hot path.
    /// </summary>
    private sealed class ViewActivationObserver(ViewActivationState state) : Observer<ActivationState>
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
}

/// <summary>
/// Optional interface for views that support attached/detached lifecycle separately from visibility.
/// Platform packages implement this to provide the Attached trigger.
/// </summary>
public interface IAttachableView : IActivatableView
{
    /// <summary>
    /// Gets an observable that emits when the view is attached to or detached from the UI hierarchy.
    /// </summary>
    Observable<ActivationState> AttachedActivation { get; }
}
