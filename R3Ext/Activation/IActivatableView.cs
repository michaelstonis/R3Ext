namespace R3Ext.Activation;

/// <summary>
/// Represents a view that can be activated and deactivated.
/// Implement this interface on view types (pages, components, controls) to enable activation lifecycle.
/// </summary>
/// <remarks>
/// Platform-specific packages (R3Ext.Maui, R3Ext.Blazor, etc.) provide source generators
/// that automatically implement the <see cref="IActivatable.Activation"/> property based on
/// native platform lifecycle events.
/// </remarks>
public interface IActivatableView : IActivatable
{
}
