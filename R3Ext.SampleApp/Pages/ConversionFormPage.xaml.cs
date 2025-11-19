using System.Globalization;
using R3;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.SampleApp;

public partial class ConversionFormPage : ContentPage
{
    private readonly ConversionFormViewModel _vm = new();
    private DisposableBag _bindings;

    public ConversionFormPage()
    {
        this.InitializeComponent();
        this.SetupBindings();
        this.UpdateTotals();
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

        _vm.WhenChanged(v => v.UnitPrice).Subscribe(_ => this.UpdateTotals()).AddTo(ref _bindings);
        _vm.WhenChanged(v => v.Quantity).Subscribe(_ => this.UpdateTotals()).AddTo(ref _bindings);
        _vm.WhenChanged(v => v.TaxRate).Subscribe(_ => this.UpdateTotals()).AddTo(ref _bindings);
        _vm.WhenChanged(v => v.Discount).Subscribe(_ => this.UpdateTotals()).AddTo(ref _bindings);
    }

    private static decimal ParseDecimal(string? text, decimal fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal value))
        {
            return value < 0 ? 0 : value;
        }

        return fallback;
    }

    private static int ParseInt(string? text, int fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int value))
        {
            return Math.Max(0, value);
        }

        return fallback;
    }

    private static double ParsePercent(string? text, double fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        string normalized = text.Replace("%", string.Empty, StringComparison.Ordinal).Trim();
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.CurrentCulture, out double value))
        {
            return Math.Clamp(value / 100.0, 0, 1);
        }

        return fallback;
    }

    private void UpdateTotals()
    {
        decimal subtotal = _vm.UnitPrice * _vm.Quantity;
        decimal discountAmount = subtotal * (decimal)_vm.Discount;
        decimal discounted = subtotal - discountAmount;
        decimal taxAmount = discounted * (decimal)_vm.TaxRate;
        decimal total = discounted + taxAmount;

        SubtotalLabel.Text = $"Subtotal: {subtotal.ToString("C", CultureInfo.CurrentCulture)}";
        DiscountAmountLabel.Text = $"Discount: -{discountAmount.ToString("C", CultureInfo.CurrentCulture)}";
        TaxAmountLabel.Text = $"Tax: {taxAmount.ToString("C", CultureInfo.CurrentCulture)}";
        TotalLabel.Text = $"Total: {total.ToString("C", CultureInfo.CurrentCulture)}";

        decimal perUnit = _vm.Quantity > 0 ? total / _vm.Quantity : total;
        EffectiveUnitPriceLabel.Text = $"Effective per unit: {perUnit.ToString("C", CultureInfo.CurrentCulture)}";
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _bindings.Dispose();
    }
}
