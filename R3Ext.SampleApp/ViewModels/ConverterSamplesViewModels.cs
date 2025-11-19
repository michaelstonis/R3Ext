using CommunityToolkit.Mvvm.ComponentModel;

namespace R3Ext.SampleApp.ViewModels;

public sealed class ConverterPlaygroundViewModel : ObservableObject
{
    private double _temperatureCelsius = 22;
    private double _humidity = 55;
    private bool _isRaining;

    public double TemperatureCelsius
    {
        get => _temperatureCelsius;
        set => this.SetProperty(ref _temperatureCelsius, value);
    }

    public double Humidity
    {
        get => _humidity;
        set => this.SetProperty(ref _humidity, value);
    }

    public bool IsRaining
    {
        get => _isRaining;
        set => this.SetProperty(ref _isRaining, value);
    }
}

public sealed class ConversionFormViewModel : ObservableObject
{
    private decimal _unitPrice = 29.99m;
    private int _quantity = 2;
    private double _discount = 0.15;
    private double _taxRate = 0.0825;

    public decimal UnitPrice
    {
        get => _unitPrice;
        set => this.SetProperty(ref _unitPrice, value);
    }

    public int Quantity
    {
        get => _quantity;
        set => this.SetProperty(ref _quantity, value);
    }

    public double Discount
    {
        get => _discount;
        set => this.SetProperty(ref _discount, value);
    }

    public double TaxRate
    {
        get => _taxRate;
        set => this.SetProperty(ref _taxRate, value);
    }
}
