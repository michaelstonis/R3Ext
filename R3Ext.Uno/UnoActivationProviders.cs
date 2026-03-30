// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using R3;
using R3Ext.Activation;

namespace R3Ext.Uno;

/// <summary>
/// Uno Platform-specific activation providers.
/// </summary>
internal static class UnoActivationProviders
{
    /// <summary>
    /// Activation provider that handles Uno Platform FrameworkElement types.
    /// </summary>
    public static Observable<ActivationState>? GetActivation(object view)
    {
        return view switch
        {
            Window window => window.GetActivation(),
            FrameworkElement element => element.GetActivation(),
            _ => null,
        };
    }
}
