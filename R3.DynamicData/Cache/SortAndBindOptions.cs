// Combined Sort + Bind operator for cache side (inspired by DynamicData SortAndBind).

using R3.DynamicData.Binding;

namespace R3.DynamicData.Cache;

/// <summary>
/// Options for SortAndBind optimization.
/// </summary>
public sealed class SortAndBindOptions
{
    /// <summary>Gets or sets optional initial capacity hint for backing collection (best effort).</summary>
    public int? InitialCapacity { get; set; }

    /// <summary>Gets or sets a value indicating whether binary search is used for inserts/removes (requires immutable sort keys).</summary>
    public bool UseBinarySearch { get; set; }

    /// <summary>Gets or sets a value indicating whether replace semantics are used for updates instead of remove + add.</summary>
    public bool UseReplaceForUpdates { get; set; }

    /// <summary>Gets or sets threshold of accumulated changes before performing a reset-style rebuild.</summary>
    public int ResetThreshold { get; set; } = BindingOptions.DefaultResetThreshold;
}
