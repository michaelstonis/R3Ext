using R3;
using R3Ext; // binding extensions
using R3Ext.SampleApp.ViewModels;
// Removed System.Reactive alias; use R3 DisposableBag for subscription management.

namespace R3Ext.SampleApp;

public partial class MainPage : ContentPage
{
	int count = 0;
	IDisposable? _ticker;
	readonly SampleViewModel _vm = new();
	DisposableBag _bindings; // struct, no initialization needed

	public MainPage()
	{
		InitializeComponent();
		SetupBindings();
	}

	void SetupBindings()
	{
		// Single reactive subscription using WhenChanged for Counter to update status label.
		_vm.WhenChanged(x => x.Counter)
			.Subscribe(c =>
			{
				StatusLabel.Text = $"Status: {_vm.Status}";
				StatusLabel.TextColor = c switch { < 5 => Colors.Green, < 10 => Colors.Orange, _ => Colors.Red };
			})
			.AddTo(ref _bindings);

		// Generated two-way binding between EditableName and Entry.Text.
		_vm.BindTwoWay(NameEntry, x => x.EditableName, e => e.Text)
			.AddTo(ref _bindings);

		_vm.BindOneWay(NameEntry, p => p.Person.Name, e => e.Text)
			.AddTo(ref _bindings);

		// Reactive update of labels on EditableName changes.
		_vm.WhenChanged(x => x.EditableName)
			.Subscribe(n =>
			{
				NameLabel.Text = $"Name: {n ?? "(null)"}";
				UpperNameLabel.Text = $"Upper: {n?.ToUpperInvariant() ?? "(null)"}";
			})
			.AddTo(ref _bindings);
	}

	private void OnCounterClicked(object? sender, EventArgs e)
	{
		count++;
		_vm.Increment();
		CounterBtn.Text = count == 1 ? $"Clicked {count} time" : $"Clicked {count} times";
		SemanticScreenReader.Announce(CounterBtn.Text);
	}

	private void OnStartTicker(object? sender, EventArgs e)
	{
		if (_ticker != null) return;

		_ticker = Observable.Interval(TimeSpan.FromSeconds(1))
			.Select((_, i) => i)
			.Subscribe(i => TickerLabel.Text = $"Ticker: {i}");
	}

	private void OnStopTicker(object? sender, EventArgs e)
	{
		_ticker?.Dispose();
		_ticker = null;
		TickerLabel.Text = "Ticker: -";
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_bindings.Dispose();
	}
}
