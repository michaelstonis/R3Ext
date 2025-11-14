using R3;
using R3Ext;

namespace R3Ext.SampleApp;

public partial class MainPage : ContentPage
{
	int count = 0;
	IDisposable? _ticker;

	public MainPage()
	{
		InitializeComponent();
	}

	private void OnCounterClicked(object? sender, EventArgs e)
	{
		count++;

		if (count == 1)
			CounterBtn.Text = $"Clicked {count} time";
		else
			CounterBtn.Text = $"Clicked {count} times";

		SemanticScreenReader.Announce(CounterBtn.Text);
	}

	private void OnStartTicker(object? sender, EventArgs e)
	{
		if (_ticker != null) return;

		_ticker = Observable.Interval(TimeSpan.FromSeconds(1))
			.Select((_, i) => i)
			.Log("Ticker")
			.Subscribe(i => TickerLabel.Text = $"Ticker: {i}");
	}

	private void OnStopTicker(object? sender, EventArgs e)
	{
		_ticker?.Dispose();
		_ticker = null;
		TickerLabel.Text = "Ticker: -";
	}
}
