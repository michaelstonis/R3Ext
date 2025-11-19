using R3;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.SampleApp;

public partial class BasicsPage : ContentPage
{
    private readonly SampleViewModel _vm = new();
    private DisposableBag _bindings;

    public BasicsPage()
    {
        this.InitializeComponent();
        this.SetupBindings();
    }

    private void SetupBindings()
    {
        // Two-way binding EditableName <-> Entry.Text
        _vm.BindTwoWay(NameEntry, v => v.EditableName, e => e.Text)
            .AddTo(ref _bindings);

        // One-way binding Person.Name -> NameLabel.Text
        _vm.BindOneWay(NameLabel, v => v.Person.Name, l => l.Text)
            .AddTo(ref _bindings);

        // Reactive transformation of name to upper
        _vm.WhenChanged(v => v.EditableName)
            .Subscribe(n => UpperNameLabel.Text = $"Upper: {n?.ToUpperInvariant() ?? "(null)"}")
            .AddTo(ref _bindings);

        // Status label updates when Counter changes
        _vm.WhenChanged(v => v.Counter)
            .Subscribe(_ => StatusLabel.Text = $"Status: {_vm.Status}")
            .AddTo(ref _bindings);

        // EvenSwitch reflects whether Counter is even
        _vm.BindOneWay(EvenSwitch, v => v.Counter, s => s.IsToggled, c => c % 2 == 0)
            .AddTo(ref _bindings);

        // CounterLabel reflects current Counter
        _vm.BindOneWay(CounterLabel, v => v.Counter, l => l.Text, c => $"Counter: {c}")
            .AddTo(ref _bindings);
    }

    private void OnIncrement(object? sender, EventArgs e)
    {
        _vm.Increment();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _bindings.Dispose();
    }
}
