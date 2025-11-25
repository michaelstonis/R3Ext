namespace R3Ext.SampleApp;

/// <summary>
/// Main page providing card-based navigation to all sample features.
/// </summary>
public partial class MainPage : ContentPage
{
    public MainPage()
    {
        this.InitializeComponent();
    }

    private async void OnNavigate(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string route)
        {
            await Shell.Current.GoToAsync(route);
        }
    }
}
