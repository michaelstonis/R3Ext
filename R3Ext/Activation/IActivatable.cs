using R3;

namespace R3Ext.Activation;

/// <summary>
/// Base interface for objects that support activation lifecycle.
/// Implement this interface to participate in activation/deactivation notifications.
/// </summary>
public interface IActivatable
{
    /// <summary>
    /// Gets an observable stream of activation state changes.
    /// Emits <see cref="ActivationState.Activated"/> when the object becomes active
    /// and <see cref="ActivationState.Deactivated"/> when it becomes inactive.
    /// </summary>
    Observable<ActivationState> Activation { get; }
}
