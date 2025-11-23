using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using R3;
using R3.DynamicData.List;

namespace R3Ext.SampleApp;

#pragma warning disable CA1001
public partial class DDLogicalOperatorsPage : ContentPage
#pragma warning restore CA1001
{
    private readonly SourceList<int> _listA = new();
    private readonly SourceList<int> _listB = new();
    private readonly ReadOnlyObservableCollection<int> _itemsA = null!;
    private readonly ReadOnlyObservableCollection<int> _itemsB = null!;
    private readonly IDisposable _bindASubscription;
    private readonly IDisposable _bindBSubscription;
    private readonly IDisposable _andSubscription;
    private readonly IDisposable _orSubscription;
    private readonly IDisposable _exceptSubscription;
    private readonly IDisposable _xorSubscription;

    public DDLogicalOperatorsPage()
    {
        InitializeComponent();

        // Bind source lists to UI
        _bindASubscription = _listA.Connect().Bind(out _itemsA);
        _bindBSubscription = _listB.Connect().Bind(out _itemsB);
        ListAView.ItemsSource = _itemsA;
        ListBView.ItemsSource = _itemsB;

        // AND - Items in both A and B
        _andSubscription = _listA.Connect()
            .And(_listB.Connect())
            .ToCollection()
            .Subscribe(items => AndResultLabel.Text = items.Any()
                ? string.Join(", ", items.OrderBy(x => x))
                : "(empty)");

        // OR - Items in A or B or both
        _orSubscription = _listA.Connect()
            .Or(_listB.Connect())
            .ToCollection()
            .Subscribe(items => OrResultLabel.Text = items.Any()
                ? string.Join(", ", items.OrderBy(x => x))
                : "(empty)");

        // EXCEPT - Items in A but not in B
        _exceptSubscription = _listA.Connect()
            .Except(_listB.Connect())
            .ToCollection()
            .Subscribe(items => ExceptResultLabel.Text = items.Any()
                ? string.Join(", ", items.OrderBy(x => x))
                : "(empty)");

        // XOR - Items in A or B but not both
        _xorSubscription = _listA.Connect()
            .Xor(_listB.Connect())
            .ToCollection()
            .Subscribe(items => XorResultLabel.Text = items.Any()
                ? string.Join(", ", items.OrderBy(x => x))
                : "(empty)");
    }

    private void OnAddToListA(object sender, EventArgs e)
    {
        if (int.TryParse(ListAEntry.Text, out var value))
        {
            if (!_listA.Items.Contains(value))
            {
                _listA.Add(value);
            }

            ListAEntry.Text = string.Empty;
        }
    }

    private void OnAddToListB(object sender, EventArgs e)
    {
        if (int.TryParse(ListBEntry.Text, out var value))
        {
            if (!_listB.Items.Contains(value))
            {
                _listB.Add(value);
            }

            ListBEntry.Text = string.Empty;
        }
    }

    private void OnRemoveFromListA(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is int value)
        {
            _listA.Remove(value);
        }
    }

    private void OnRemoveFromListB(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is int value)
        {
            _listB.Remove(value);
        }
    }

    private void OnClearListA(object sender, EventArgs e)
    {
        _listA.Clear();
    }

    private void OnClearListB(object sender, EventArgs e)
    {
        _listB.Clear();
    }
}
