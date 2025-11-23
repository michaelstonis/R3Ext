// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace R3.DynamicData.List;

/// <summary>
/// Sort optimization options.
/// </summary>
[Flags]
public enum SortOptions
{
    /// <summary>
    /// No sort optimizations.
    /// </summary>
    None = 0,

    /// <summary>
    /// Use binary search for inserting items into sorted position.
    /// This can only be used when the values which are sorted on are immutable.
    /// </summary>
    UseBinarySearch = 1,
}
