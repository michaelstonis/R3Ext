using Microsoft.Maui.Controls;

namespace R3Ext.SampleApp;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	async void OnGoBasics(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//BasicsPage");
	async void OnGoDeep(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//DeepBindingPage");
	async void OnGoControls(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//ControlsPage");
	async void OnGoPerformance(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//PerformancePage");
	async void OnGoForm(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//FormPage");
}
