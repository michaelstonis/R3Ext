using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using R3;
using R3Ext;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.SampleApp;

public partial class ConversionFormPage : ContentPage
{
    private readonly ConversionFormViewModel _vm = new();
    private DisposableBag _bindings;

    public ConversionFormPage()
    {
        InitializeComponent();
        SetupBindings();
        UpdateTotals();
    }

    private void SetupBindings()
    {
        _vm.BindTwoWay(UnitPriceEntry, v => v.UnitPrice, e => e.Text,
                price => price.ToString("F2", CultureInfo.CurrentCulture),
                text => ParseDecimal(text, _vm.UnitPrice))
            .AddTo(ref _bindings);

        _vm.BindTwoWay(QuantityEntry, v => v.Quantity, e => e.Text,
                quantity => quantity.ToString(CultureInfo.CurrentCulture),
                text => ParseInt(text, _vm.Quantity))
            .AddTo(ref _bindings);

        _vm.BindTwoWay(TaxRateEntry, v => v.TaxRate, e => e.Text,
                rate => (rate * 100).ToString("F2", CultureInfo.CurrentCulture),
                text => ParsePercent(text, _vm.TaxRate))
            .AddTo(ref _bindings);

        _vm.BindTwoWay(DiscountSlider, v => v.Discount, s => s.Value,
                discount => discount * 100,
                value => Math.Clamp(value / 100.0, 0, 0.5))
            .AddTo(ref _bindings);

        _vm.BindOneWay(DiscountLabel, v => v.Discount, l => l.Text, discount => $"Discount: {discount * 100:F1}%")
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.UnitPrice).Subscribe(_ => UpdateTotals()).AddTo(ref _bindings);
        _vm.WhenChanged(v => v.Quantity).Subscribe(_ => UpdateTotals()).AddTo(ref _bindings);
        _vm.WhenChanged(v => v.TaxRate).Subscribe(_ => UpdateTotals()).AddTo(ref _bindings);
        _vm.WhenChanged(v => v.Discount).Subscribe(_ => UpdateTotals()).AddTo(ref _bindings);
    }

    private static decimal ParseDecimal(string? text, decimal fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value))
        {
            return value < 0 ? 0 : value;
        }

        return fallback;
    }

    private static int ParseInt(string? text, int fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value))
        {
            return Math.Max(0, value);
        }

        return fallback;
    }

    private static double ParsePercent(string? text, double fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;

        var normalized = text.Replace("%", string.Empty, StringComparison.Ordinal).Trim();
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.CurrentCulture, out var value))
        {
            return Math.Clamp(value / 100.0, 0, 1);
        }

        return fallback;
    }

    private void UpdateTotals()
    {
        var subtotal = _vm.UnitPrice * _vm.Quantity;
        var discountAmount = subtotal * (decimal)_vm.Discount;
        var discounted = subtotal - discountAmount;
        var taxAmount = discounted * (decimal)_vm.TaxRate;
        var total = discounted + taxAmount;

        SubtotalLabel.Text = $"Subtotal: {subtotal.ToString("C", CultureInfo.CurrentCulture)}";
        DiscountAmountLabel.Text = $"Discount: -{discountAmount.ToString("C", CultureInfo.CurrentCulture)}";
        TaxAmountLabel.Text = $"Tax: {taxAmount.ToString("C", CultureInfo.CurrentCulture)}";
        TotalLabel.Text = $"Total: {total.ToString("C", CultureInfo.CurrentCulture)}";

        var perUnit = _vm.Quantity > 0 ? total / _vm.Quantity : total;
        EffectiveUnitPriceLabel.Text = $"Effective per unit: {perUnit.ToString("C", CultureInfo.CurrentCulture)}";
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _bindings.Dispose();
    }
}
