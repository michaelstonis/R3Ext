using R3;

namespace R3Ext.Activation;

/// <summary>
/// Represents a view model that can be attached and detached based on the view's
/// loaded/unloaded lifecycle (as opposed to visibility-based activation).
/// Implement this interface on view model types to enable attachment lifecycle.
/// </summary>
/// <remarks>
/// <para>
/// When a view implementing <see cref="IViewFor{TViewModel}"/> is loaded into the visual tree,
/// it will automatically attach the associated view model (if it implements this interface
/// and <see cref="IViewFor{TViewModel}.AutoActivateViewModel"/> is true).
/// </para>
/// <para>
/// Use the <see cref="AttachableViewModelExtensions.WhenAttached"/> extension method
/// to register blocks of code that run during attachment and are disposed on detachment.
/// </para>
/// <para>
/// This differs from <see cref="IActivatableViewModel"/> in that it uses Loaded/Unloaded
/// events rather than Appearing/Disappearing (visibility) events.
/// </para>
/// <para>
/// A ViewModel can implement both <see cref="IActivatableViewModel"/> and <see cref="IAttachableViewModel"/>
/// to respond to both visibility and load-based lifecycle events.
/// </para>
/// </remarks>
public interface IAttachableViewModel
{
    /// <summary>
    /// Gets the attacher that manages this view model's attachment lifecycle.
    /// </summary>
    ViewModelAttacher Attacher { get; }
}
