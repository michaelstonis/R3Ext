using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Controls;
using R3;
using R3Ext;

namespace R3Ext.SampleApp;

public partial class PerformancePage : ContentPage
{
    public sealed class PerfViewModel : ObservableObject
    {
        private string _input = string.Empty;
        private int _clicks;

        public string Input { get => _input; set => SetProperty(ref _input, value); }
        public int Clicks { get => _clicks; set => SetProperty(ref _clicks, value); }
        public void Increment(int by = 1) => Clicks += by;
    }

    private readonly PerfViewModel _vm = new();
    private DisposableBag _bindings;

    public PerformancePage()
    {
        InitializeComponent();
        SetupBindings();
    }

    private void SetupBindings()
    {
        // Two-way bind Entry.Text <-> vm.Input
        _vm.BindTwoWay(InputEntry, v => v.Input, e => e.Text).AddTo(ref _bindings);

        // Immediate vs DebounceImmediate for input
        _vm.WhenChanged(v => v.Input)
            .Subscribe(text => ImmediateLabel.Text = $"Immediate: {text}")
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Input)
            .DebounceImmediate(TimeSpan.FromMilliseconds(400))
            .Subscribe(text => DebouncedLabel.Text = $"Debounced: {text}")
            .AddTo(ref _bindings);

        // Direct vs Conflated click count
        _vm.WhenChanged(v => v.Clicks)
            .Subscribe(c => DirectClicksLabel.Text = $"Direct: {c}")
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Clicks)
            .Conflate(TimeSpan.FromMilliseconds(500))
            .Subscribe(c => ConflatedClicksLabel.Text = $"Conflated: {c}")
            .AddTo(ref _bindings);
    }

    void OnSpam(object? sender, EventArgs e) => _vm.Increment();
    void OnSpam10(object? sender, EventArgs e) => _vm.Increment(10);

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _bindings.Dispose();
    }
}
