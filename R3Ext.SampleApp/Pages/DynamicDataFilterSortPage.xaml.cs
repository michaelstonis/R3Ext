using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using R3; // Subject, Observable
using R3.DynamicData.List;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.SampleApp;

#pragma warning disable CA1001
public partial class DynamicDataFilterSortPage : ContentPage
#pragma warning restore CA1001
{
    private static readonly IComparer<Person> AscendingComparer = Comparer<Person>.Create((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    private static readonly IComparer<Person> DescendingComparer = Comparer<Person>.Create((a, b) => string.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase));
    private readonly SourceList<Person> _source = new();
    private readonly Subject<Func<Person, bool>> _predicateSubject = new();
    private readonly Subject<IComparer<Person>> _comparerSubject = new();
    private readonly ReadOnlyObservableCollection<Person> _people;
    private readonly IDisposable _subscription;
    private string _currentFilter = string.Empty;

    public DynamicDataFilterSortPage()
    {
        this.InitializeComponent();

        // Seed with some sample data.
        _source.AddRange(new[]
        {
            new Person { Name = "Alice" },
            new Person { Name = "Bob" },
            new Person { Name = "Charlie" },
            new Person { Name = "Diana" },
            new Person { Name = "Eve" },
        });

        // Initialize subjects.
        _predicateSubject.OnNext(p => true);

        // Prepare target collection to enable readonly wrapper without out parameter.
        var target = new R3.DynamicData.Binding.ObservableCollectionExtended<Person>();
        _people = new ReadOnlyObservableCollection<Person>(target);

        // Single pipeline subscription: dynamic filter + dynamic sort.
        _subscription = _source
            .Connect()
            .AutoRefresh(p => p.Name)
            .Filter(_predicateSubject)
            .Sort(AscendingComparer, _comparerSubject)
            .Bind(target);

        this.PeopleView.ItemsSource = _people;
    }

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
    {
        _currentFilter = e.NewTextValue ?? string.Empty;
        _predicateSubject.OnNext(p => string.IsNullOrWhiteSpace(_currentFilter)
            || p.Name.Contains(_currentFilter, StringComparison.OrdinalIgnoreCase));
    }

    private void OnSortAscending(object sender, EventArgs e)
    {
        _comparerSubject.OnNext(AscendingComparer);
    }

    private void OnSortDescending(object sender, EventArgs e)
    {
        _comparerSubject.OnNext(DescendingComparer);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _subscription?.Dispose();
        _source.Dispose();
        _predicateSubject.Dispose();
        _comparerSubject.Dispose();
    }
}
