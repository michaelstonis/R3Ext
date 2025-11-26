using R3Ext.SampleApp.ViewModels;

namespace R3Ext.SampleApp;

/// <summary>
/// Demonstrates WhenObserved operator examples.
/// </summary>
public partial class WhenObservedPage : ContentPage
{
    private WhenObservedViewModel ViewModel => (WhenObservedViewModel)BindingContext;

    public WhenObservedPage()
    {
        InitializeComponent();
    }

    private void OnTriggerDocumentSave(object sender, EventArgs e)
    {
        ViewModel.TriggerDocumentSave();
    }

    private void OnTriggerDocumentEdit(object sender, EventArgs e)
    {
        ViewModel.TriggerDocumentEdit();
    }

    private void OnSwitchToNewDocument(object sender, EventArgs e)
    {
        ViewModel.SwitchToNewDocument();
    }

    private void OnEmitStreamValue(object sender, EventArgs e)
    {
        ViewModel.EmitStreamValue();
    }

    private void OnSwitchToNewStream(object sender, EventArgs e)
    {
        ViewModel.SwitchToNewStream();
    }

    private void OnSwitchNestedDocument(object sender, EventArgs e)
    {
        ViewModel.SwitchNestedDocument();
    }

    private void OnClearLog(object sender, EventArgs e)
    {
        ViewModel.ClearLog();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
