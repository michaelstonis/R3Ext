using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using R3;
using R3.DynamicData.List;

namespace R3Ext.SampleApp;

#pragma warning disable CA1001
public partial class DDAggregationPage : ContentPage
#pragma warning restore CA1001
{
    private readonly SourceList<int> _source = new();
    private readonly Random _random = new();
    private readonly ReadOnlyObservableCollection<int> _items = null!;
    private readonly IDisposable _bindSubscription;
    private readonly IDisposable _countSubscription;
    private readonly IDisposable _sumSubscription;
    private readonly IDisposable _minSubscription;
    private readonly IDisposable _maxSubscription;
    private readonly IDisposable _avgSubscription;
    private readonly IDisposable _stdDevSubscription;

    public DDAggregationPage()
    {
        InitializeComponent();

        // Bind to UI
        _bindSubscription = _source.Connect().Bind(out _items);
        ValuesView.ItemsSource = _items;

        // Count aggregation
        _countSubscription = _source.CountChanged
            .Subscribe(count => CountLabel.Text = count.ToString());

        // Sum aggregation
        _sumSubscription = _source.Connect()
            .Sum()
            .Subscribe(sum => SumLabel.Text = sum.ToString());

        // Min aggregation - selector returns int (struct), result is int
        _minSubscription = _source.Connect()
            .Min(x => x)
            .Subscribe(min => MinLabel.Text = min == 0 && _source.Count == 0 ? "—" : min.ToString());

        // Max aggregation - selector returns int (struct), result is int
        _maxSubscription = _source.Connect()
            .Max(x => x)
            .Subscribe(max => MaxLabel.Text = max == 0 && _source.Count == 0 ? "—" : max.ToString());

        // Average aggregation - returns double
        _avgSubscription = _source.Connect()
            .Avg(x => x)
            .Subscribe(avg => AvgLabel.Text = _source.Count == 0 ? "—" : $"{avg:F2}");

        // Standard Deviation aggregation - returns double
        _stdDevSubscription = _source.Connect()
            .StdDev(x => x)
            .Subscribe(stdDev => StdDevLabel.Text = _source.Count == 0 ? "—" : $"{stdDev:F2}");
    }

    private void OnAddValue(object sender, EventArgs e)
    {
        if (int.TryParse(ValueEntry.Text, out var value))
        {
            _source.Add(value);
            ValueEntry.Text = string.Empty;
        }
    }

    private void OnAddRandom(object sender, EventArgs e)
    {
        var values = Enumerable.Range(0, 5).Select(_ => _random.Next(1, 100)).ToArray();
        _source.AddRange(values);
    }

    private void OnRemoveFirst(object sender, EventArgs e)
    {
        if (_source.Count > 0)
        {
            _source.RemoveAt(0);
        }
    }

    private void OnRemoveValue(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is int value)
        {
            _source.Remove(value);
        }
    }

    private void OnClear(object sender, EventArgs e)
    {
        _source.Clear();
    }
}
