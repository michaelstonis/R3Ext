using R3;

namespace R3Ext.Activation;

/// <summary>
/// Represents a view model that can be activated and deactivated.
/// Implement this interface on view model types to enable activation lifecycle.
/// </summary>
/// <remarks>
/// <para>
/// When a view implementing <see cref="IViewFor{TViewModel}"/> is activated,
/// it will automatically activate the associated view model (unless
/// <see cref="IViewFor{TViewModel}.AutoActivateViewModel"/> is set to false).
/// </para>
/// <para>
/// Use the <see cref="ActivatableViewModelExtensions.WhenActivated"/> extension method
/// to register blocks of code that run during activation and are disposed on deactivation.
/// </para>
/// </remarks>
public interface IActivatableViewModel : IActivatable
{
    /// <summary>
    /// Gets the activator that manages this view model's activation lifecycle.
    /// </summary>
    ViewModelActivator Activator { get; }

    /// <summary>
    /// Gets the activation observable from the activator.
    /// </summary>
    Observable<ActivationState> IActivatable.Activation => Activator.Activation;
}
