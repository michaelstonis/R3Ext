namespace R3Ext.Activation;

/// <summary>
/// Platform-agnostic activation triggers that map to platform-specific events.
/// Each platform maps these concepts to appropriate native lifecycle events.
/// </summary>
public enum ActivationTrigger
{
    /// <summary>
    /// Triggered when the view becomes visible or hidden.
    /// <para>
    /// Platform mappings:
    /// <list type="bullet">
    ///   <item>MAUI Page: Appearing/Disappearing events</item>
    ///   <item>MAUI View: IsVisible property changes</item>
    ///   <item>Avalonia: IsVisible property changes</item>
    ///   <item>Blazor: Component render state</item>
    /// </list>
    /// </para>
    /// </summary>
    Visibility,

    /// <summary>
    /// Triggered when the view is attached to or detached from the UI hierarchy.
    /// <para>
    /// Platform mappings:
    /// <list type="bullet">
    ///   <item>MAUI: Loaded/Unloaded events</item>
    ///   <item>Avalonia: AttachedToVisualTree/DetachedFromVisualTree</item>
    ///   <item>Blazor: OnAfterRender/Dispose</item>
    ///   <item>WinUI/Uno: Loading/Loaded/Unloaded events</item>
    /// </list>
    /// </para>
    /// </summary>
    Attached,

    /// <summary>
    /// Triggered based on focus or window activation state.
    /// <para>
    /// Platform mappings:
    /// <list type="bullet">
    ///   <item>MAUI: Window.Activated/Deactivated</item>
    ///   <item>Avalonia: Window.Activated/Deactivated</item>
    ///   <item>Blazor: Focus/blur events</item>
    /// </list>
    /// </para>
    /// </summary>
    Focus,
}
