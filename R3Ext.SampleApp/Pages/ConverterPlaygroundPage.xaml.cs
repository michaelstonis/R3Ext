using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using R3;
using R3Ext;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.SampleApp;

public partial class ConverterPlaygroundPage : ContentPage
{
    private readonly ConverterPlaygroundViewModel _vm = new();
    private DisposableBag _bindings;

    public ConverterPlaygroundPage()
    {
        InitializeComponent();
        SetupBindings();
        UpdateComfortSummary();
    }

    private void SetupBindings()
    {
        _vm.BindTwoWay(TemperatureSlider, v => v.TemperatureCelsius, c => c.Value)
            .AddTo(ref _bindings);

        _vm.BindOneWay(CelsiusLabel, v => v.TemperatureCelsius, l => l.Text, c => $"{c:F1} °C")
            .AddTo(ref _bindings);

        _vm.BindOneWay(FahrenheitLabel, v => v.TemperatureCelsius, l => l.Text, c => $"{c * 9.0 / 5.0 + 32:F1} °F")
            .AddTo(ref _bindings);

        _vm.BindOneWay(TemperatureSwatch, v => v.TemperatureCelsius, b => b.Color, ToTemperatureColor)
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.TemperatureCelsius)
            .Subscribe(_ => UpdateComfortSummary())
            .AddTo(ref _bindings);

        _vm.BindTwoWay(RainSwitch, v => v.IsRaining, c => c.IsToggled)
            .AddTo(ref _bindings);

        _vm.BindOneWay(ConditionLabel, v => v.IsRaining, l => l.Text, raining => raining ? "Raining" : "Dry Skies")
            .AddTo(ref _bindings);

        _vm.BindOneWay(ConditionAdviceLabel, v => v.IsRaining, l => l.Text, raining => raining ? "Grab a jacket and umbrella." : "Great weather for a walk.")
            .AddTo(ref _bindings);

        _vm.BindTwoWay(HumiditySlider, v => v.Humidity, c => c.Value)
            .AddTo(ref _bindings);

        _vm.BindOneWay(HumidityLabel, v => v.Humidity, l => l.Text, h => $"Humidity: {h:F0}%")
            .AddTo(ref _bindings);

        _vm.BindOneWay(HumidityProgress, v => v.Humidity, p => p.Progress, h => Math.Clamp(h / 100.0, 0, 1))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Humidity)
            .Subscribe(_ => UpdateComfortSummary())
            .AddTo(ref _bindings);
    }

    private static Color ToTemperatureColor(double temperatureCelsius)
    {
        var normalized = Math.Clamp((temperatureCelsius + 20.0) / 70.0, 0, 1);
        var cold = Color.FromRgb(0.0, 0.45, 0.9);
        var warm = Color.FromRgb(0.95, 0.4, 0.1);
        return LerpColor(cold, warm, normalized);
    }

    private static Color LerpColor(Color start, Color end, double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        double Lerp(double a, double b) => a + (b - a) * progress;
        return Color.FromRgb(Lerp(start.Red, end.Red), Lerp(start.Green, end.Green), Lerp(start.Blue, end.Blue));
    }

    private void UpdateComfortSummary()
    {
        ComfortLabel.Text = DescribeComfort(_vm.TemperatureCelsius, _vm.Humidity);
    }

    private static string DescribeComfort(double temperature, double humidity)
    {
        if (temperature < 5)
        {
            return humidity > 70 ? "Cold and damp — bundle up!" : "Chilly but dry — grab a coat.";
        }

        if (temperature < 20)
        {
            return humidity < 50 ? "Cool and comfortable." : "Cool with a hint of humidity.";
        }

        if (temperature < 28)
        {
            if (humidity < 40) return "Warm and crisp — ideal weather.";
            if (humidity < 70) return "Pleasantly warm with mild humidity.";
            return "Warm yet sticky — take a break in the shade.";
        }

        return humidity > 60 ? "Hot and muggy — hydrate often." : "Summer heat — stay cool!";
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _bindings.Dispose();
    }
}
