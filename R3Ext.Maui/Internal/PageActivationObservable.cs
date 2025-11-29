using Microsoft.Maui.Controls;
using R3;
using R3Ext.Activation;

namespace R3Ext.Maui.Internal;

/// <summary>
/// Observable that emits activation states based on Page Appearing/Disappearing events.
/// </summary>
internal sealed class PageActivationObservable(Page page) : Observable<ActivationState>
{
    protected override IDisposable SubscribeCore(Observer<ActivationState> observer)
    {
        return new PageActivationSubscription(page, observer);
    }
}
