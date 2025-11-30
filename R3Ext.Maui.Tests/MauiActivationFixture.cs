using Microsoft.Maui.Controls;
using R3;
using R3Ext.Activation;

// Resolve ambiguity with Microsoft.Maui.ActivationState
using ActivationState = R3Ext.Activation.ActivationState;

namespace R3Ext.Maui.Tests;

/// <summary>
/// Fixture that registers the MAUI activation provider for tests.
/// </summary>
public sealed class MauiActivationFixture : IDisposable
{
    private static int _registrationCount;

    public MauiActivationFixture()
    {
        // Register the MAUI activation provider (idempotent)
        if (Interlocked.Increment(ref _registrationCount) == 1)
        {
            ActivationProviderRegistry.Register(MauiActivationProvider);
        }
    }

    private static Observable<ActivationState>? MauiActivationProvider(object view)
    {
        return view switch
        {
            Page page => page.GetActivation(),
            View v => v.GetActivation(),
            _ => null,
        };
    }

    public void Dispose()
    {
        // Don't clear the provider - other tests may still need it
    }
}
