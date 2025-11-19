using CommunityToolkit.Mvvm.ComponentModel;
using R3;

namespace R3Ext.SampleApp;

public partial class ControlsPage : ContentPage
{
    public sealed class ControlsViewModel : ObservableObject
    {
        private double _sliderValue = 25;
        private double _stepperValue = 5;
        private bool _toggle;
        private DateTime _date = DateTime.Today;
        private TimeSpan _time = DateTime.Now.TimeOfDay;

        public double SliderValue
        {
            get => _sliderValue;
            set => this.SetProperty(ref _sliderValue, value);
        }

        public double StepperValue
        {
            get => _stepperValue;
            set => this.SetProperty(ref _stepperValue, value);
        }

        public bool Toggle
        {
            get => _toggle;
            set => this.SetProperty(ref _toggle, value);
        }

        public DateTime Date
        {
            get => _date;
            set => this.SetProperty(ref _date, value);
        }

        public TimeSpan Time
        {
            get => _time;
            set => this.SetProperty(ref _time, value);
        }
    }

    private readonly ControlsViewModel _vm = new();
    private DisposableBag _bindings;

    public ControlsPage()
    {
        this.InitializeComponent();
        this.SetupBindings();
    }

    private void SetupBindings()
    {
        // Two-way control bindings
        _vm.BindTwoWay(DemoSlider, v => v.SliderValue, c => c.Value).AddTo(ref _bindings);
        _vm.BindTwoWay(DemoStepper, v => v.StepperValue, c => c.Value).AddTo(ref _bindings);
        _vm.BindTwoWay(DemoSwitch, v => v.Toggle, c => c.IsToggled).AddTo(ref _bindings);
        _vm.BindTwoWay(DemoDate, v => v.Date, c => c.Date).AddTo(ref _bindings);
        _vm.BindTwoWay(DemoTime, v => v.Time, c => c.Time).AddTo(ref _bindings);

        // Display values
        _vm.BindOneWay(SliderValueLabel, v => v.SliderValue, l => l.Text, d => $"Slider: {d:F1}").AddTo(ref _bindings);
        _vm.BindOneWay(StepperValueLabel, v => v.StepperValue, l => l.Text, d => $"Stepper: {d:F0}").AddTo(ref _bindings);
        _vm.BindOneWay(SwitchStateLabel, v => v.Toggle, l => l.Text, b => b ? "Toggle: On" : "Toggle: Off").AddTo(ref _bindings);
        _vm.BindOneWay(DateLabel, v => v.Date, l => l.Text, d => $"Date: {d:d}").AddTo(ref _bindings);
        _vm.BindOneWay(TimeLabel, v => v.Time, l => l.Text, t => $"Time: {new DateTime(t.Ticks):T}").AddTo(ref _bindings);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _bindings.Dispose();
    }
}
