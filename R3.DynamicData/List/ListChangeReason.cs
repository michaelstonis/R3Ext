// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

namespace R3.DynamicData.List;

/// <summary>
/// The reason for a change in a list.
/// </summary>
public enum ListChangeReason
{
    /// <summary>
    /// An item was added.
    /// </summary>
    Add,

    /// <summary>
    /// An item was added at a specific index.
    /// </summary>
    AddRange,

    /// <summary>
    /// An item was replaced.
    /// </summary>
    Replace,

    /// <summary>
    /// An item was removed.
    /// </summary>
    Remove,

    /// <summary>
    /// A range of items was removed.
    /// </summary>
    RemoveRange,

    /// <summary>
    /// An item was moved.
    /// </summary>
    Moved,

    /// <summary>
    /// An item was refreshed.
    /// </summary>
    Refresh,

    /// <summary>
    /// The list was cleared.
    /// </summary>
    Clear,
}
