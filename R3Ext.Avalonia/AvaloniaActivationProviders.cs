// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;
using R3;
using R3Ext.Activation;

namespace R3Ext.Avalonia;

/// <summary>
/// Avalonia-specific activation providers.
/// </summary>
internal static class AvaloniaActivationProviders
{
    /// <summary>
    /// Activation provider that handles Avalonia Visual types.
    /// </summary>
    public static Observable<ActivationState>? GetActivation(object view)
    {
        return view switch
        {
            Window window => window.GetActivation(),
            Visual visual => visual.GetActivation(),
            _ => null,
        };
    }
}
