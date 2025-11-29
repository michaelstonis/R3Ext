using Microsoft.Maui.Controls;
using R3;
using R3Ext.Activation;

namespace R3Ext.Maui.Internal;

/// <summary>
/// Observable that emits activation states based on View IsVisible property changes.
/// </summary>
internal sealed class ViewVisibilityActivationObservable(View view) : Observable<ActivationState>
{
    protected override IDisposable SubscribeCore(Observer<ActivationState> observer)
    {
        return new ViewVisibilitySubscription(view, observer);
    }
}
