namespace R3Ext.SampleApp;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        this.InitializeComponent();
    }

    private async void OnGoBasics(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//BasicsPage");
    }

    private async void OnGoDynamicDataBasics(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//DynamicDataBasicsPage");
    }

    private async void OnGoDynamicDataFilterSort(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//DynamicDataFilterSortPage");
    }

    private async void OnGoDeep(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//DeepBindingPage");
    }

    private async void OnGoConverters(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//ConverterPlaygroundPage");
    }

    private async void OnGoConversionForm(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//ConversionFormPage");
    }

    private async void OnGoControls(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//ControlsPage");
    }

    private async void OnGoPerformance(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//PerformancePage");
    }

    private async void OnGoForm(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//FormPage");
    }
}
