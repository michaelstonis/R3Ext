using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using R3;
using R3.DynamicData.List;

namespace R3Ext.SampleApp;

#pragma warning disable CA1001
public partial class DynamicDataBasicsPage : ContentPage
#pragma warning restore CA1001
{
    private readonly SourceList<int> _source = new();
    private readonly Random _random = new();
    private readonly Subject<Func<int, bool>> _filterSubject = new();
    private readonly Subject<IComparer<int>> _sortSubject = new();
    private ReadOnlyObservableCollection<int> _items = null!;
    private IDisposable? _bindSubscription;
    private IDisposable? _countSubscription;
    private IDisposable? _sumSubscription;
    private string _currentFilter = "None";

    public DynamicDataBasicsPage()
    {
        this.InitializeComponent();

        // Initialize filter with "show all"
        _filterSubject.OnNext(_ => true);

        // Initialize sort with ascending
        _sortSubject.OnNext(Comparer<int>.Create((a, b) => a.CompareTo(b)));

        // Build pipeline: Filter -> Sort -> Bind
        _bindSubscription = _source
            .Connect()
            .Filter(_filterSubject)
            .Sort(Comparer<int>.Default, _sortSubject)
            .Bind(out _items);

        this.ItemsView.ItemsSource = _items;

        // Subscribe to count changes
        _countSubscription = _source.CountChanged
            .Subscribe(count => this.CountLabel.Text = $"Count: {count}");

        // Subscribe to sum changes
        _sumSubscription = _source
            .Connect()
            .ToCollection()
            .Subscribe(list =>
            {
                var sum = list.Sum();
                this.SumLabel.Text = $"Sum: {sum}";
            });
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

    private void OnAddRange(object sender, EventArgs e)
    {
        _source.AddRange(Enumerable.Range(1, 10));
    }

    private void OnRemoveItem(object sender, EventArgs e)
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

    private void OnSortAscending(object sender, EventArgs e)
    {
        _sortSubject.OnNext(Comparer<int>.Create((a, b) => a.CompareTo(b)));
    }

    private void OnSortDescending(object sender, EventArgs e)
    {
        _sortSubject.OnNext(Comparer<int>.Create((a, b) => b.CompareTo(a)));
    }

    private void OnFilterEven(object sender, EventArgs e)
    {
        _filterSubject.OnNext(x => x % 2 == 0);
        _currentFilter = "Even numbers only";
        this.FilterLabel.Text = $"Filter: {_currentFilter}";
    }

    private void OnShowAll(object sender, EventArgs e)
    {
        _filterSubject.OnNext(_ => true);
        _currentFilter = "None";
        this.FilterLabel.Text = $"Filter: {_currentFilter}";
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _bindSubscription?.Dispose();
        _countSubscription?.Dispose();
        _sumSubscription?.Dispose();
        _filterSubject.Dispose();
        _sortSubject.Dispose();
        _source.Dispose();
    }
}
