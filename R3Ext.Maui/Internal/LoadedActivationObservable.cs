using Microsoft.Maui.Controls;
using R3;
using R3Ext.Activation;

namespace R3Ext.Maui.Internal;

/// <summary>
/// Observable that emits activation states based on Loaded/Unloaded events.
/// </summary>
internal sealed class LoadedActivationObservable(VisualElement element) : Observable<ActivationState>
{
    protected override IDisposable SubscribeCore(Observer<ActivationState> observer)
    {
        return new LoadedActivationSubscription(element, observer);
    }
}
