using System;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using R3.DynamicData.List;

namespace R3Ext.SampleApp;

#pragma warning disable CA1001
public partial class DynamicDataBasicsPage : ContentPage
#pragma warning restore CA1001
{
    private readonly SourceList<int> _source = new();
    private readonly Random _random = new();
    private ReadOnlyObservableCollection<int> _items = null!;
    private IDisposable? _subscription;

    public DynamicDataBasicsPage()
    {
        this.InitializeComponent();

        _subscription = _source
            .Connect()
            .Bind(out _items);

        this.ItemsView.ItemsSource = _items;
    }

    private void OnAddItem(object sender, EventArgs e)
    {
        if (int.TryParse(this.NewItemEntry.Text, out var value))
        {
            _source.Add(value);
            this.NewItemEntry.Text = string.Empty;
        }
    }

    private void OnAddRandom(object sender, EventArgs e)
    {
        _source.Add(_random.Next(0, 100));
    }

    private void OnRemoveSelected(object sender, EventArgs e)
    {
        if (this.ItemsView.SelectedItem is int selected)
        {
            _source.Remove(selected);
        }
    }

    private void OnClear(object sender, EventArgs e)
    {
        _source.Clear();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _subscription?.Dispose();
        _source.Dispose();
    }
}
