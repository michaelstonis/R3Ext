namespace R3Ext.Activation;

/// <summary>
/// Represents the activation state of a view or view model.
/// </summary>
public enum ActivationState
{
    /// <summary>
    /// The view/view model has become active and is visible/attached.
    /// </summary>
    Activated,

    /// <summary>
    /// The view/view model has become inactive and is hidden/detached.
    /// </summary>
    Deactivated,
}
