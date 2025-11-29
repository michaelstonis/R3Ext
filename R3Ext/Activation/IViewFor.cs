namespace R3Ext.Activation;

/// <summary>
/// Associates a view with its view model, enabling automatic view model activation.
/// Implement this interface on view types (pages, components, controls) to establish
/// the view-view model relationship.
/// </summary>
/// <typeparam name="TViewModel">The type of the view model.</typeparam>
/// <remarks>
/// <para>
/// This interface is the primary opt-in mechanism for activation support.
/// Platform-specific source generators detect implementations of this interface
/// and generate the required <see cref="IActivatable.Activation"/> property.
/// </para>
/// <para>
/// When the view activates, it automatically activates the <see cref="ViewModel"/>
/// (if it implements <see cref="IActivatableViewModel"/>) unless
/// <see cref="AutoActivateViewModel"/> is set to false.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // MAUI example - source generator provides Activation implementation
/// public partial class MyPage : ContentPage, IViewFor&lt;MyViewModel&gt;
/// {
///     public MyPage(MyViewModel viewModel)
///     {
///         InitializeComponent();
///         ViewModel = viewModel;
///     }
///
///     public MyViewModel? ViewModel { get; set; }
/// }
/// </code>
/// </example>
public interface IViewFor<TViewModel> : IActivatableView
    where TViewModel : class
{
    /// <summary>
    /// Gets or sets the view model for this view.
    /// </summary>
    TViewModel? ViewModel { get; set; }

    /// <summary>
    /// Gets a value indicating whether the view model should be automatically
    /// activated/deactivated when this view activates/deactivates.
    /// </summary>
    /// <remarks>
    /// Default is true. Override this property and return false if you need
    /// to manage view model activation manually.
    /// </remarks>
    bool AutoActivateViewModel => true;
}
