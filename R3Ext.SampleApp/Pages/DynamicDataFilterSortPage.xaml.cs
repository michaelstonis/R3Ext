using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using R3;
using R3.DynamicData.List;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.SampleApp;

#pragma warning disable CA1001
public partial class DynamicDataFilterSortPage : ContentPage
#pragma warning restore CA1001
{
    private static readonly IComparer<PersonWithAge> NameAscComparer = Comparer<PersonWithAge>.Create((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    private static readonly IComparer<PersonWithAge> NameDescComparer = Comparer<PersonWithAge>.Create((a, b) => string.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase));
    private static readonly IComparer<PersonWithAge> AgeLowHighComparer = Comparer<PersonWithAge>.Create((a, b) => a.Age.CompareTo(b.Age));
    private static readonly IComparer<PersonWithAge> AgeHighLowComparer = Comparer<PersonWithAge>.Create((a, b) => b.Age.CompareTo(a.Age));

    private readonly SourceList<PersonWithAge> _source = new();
    private readonly Subject<Func<PersonWithAge, bool>> _predicateSubject = new();
    private readonly Subject<IComparer<PersonWithAge>> _comparerSubject = new();
    private readonly ReadOnlyObservableCollection<PersonWithAge> _people;
    private readonly IDisposable _bindSubscription;
    private readonly IDisposable _countSubscription;
    private readonly IDisposable _avgAgeSubscription;
    private string _currentFilter = string.Empty;

    public DynamicDataFilterSortPage()
    {
        this.InitializeComponent();

        // Seed with some sample data
        _source.AddRange(new[]
        {
            new PersonWithAge { Name = "Alice Johnson", Age = 28 },
            new PersonWithAge { Name = "Bob Smith", Age = 35 },
            new PersonWithAge { Name = "Charlie Davis", Age = 42 },
            new PersonWithAge { Name = "Diana Prince", Age = 31 },
            new PersonWithAge { Name = "Eve Miller", Age = 26 },
        });

        // Initialize subjects
        _predicateSubject.OnNext(p => true);
        _comparerSubject.OnNext(NameAscComparer);

        // Prepare target collection
        var target = new R3.DynamicData.Binding.ObservableCollectionExtended<PersonWithAge>();
        _people = new ReadOnlyObservableCollection<PersonWithAge>(target);

        // Pipeline: AutoRefresh on Name and Age changes -> Filter -> Sort -> Bind
        _bindSubscription = _source
            .Connect()
            .AutoRefresh(p => p.Name)
            .AutoRefresh(p => p.Age)
            .Filter(_predicateSubject)
            .Sort(NameAscComparer, _comparerSubject)
            .Bind(target);

        this.PeopleView.ItemsSource = _people;

        // Subscribe to counts
        _countSubscription = _people
            .ToObservable()
            .Subscribe(_ => this.UpdateCounts());

        // Subscribe to average age of visible people
        _avgAgeSubscription = _people
            .ToObservable()
            .Subscribe(_ =>
            {
                var avg = _people.Any() ? _people.Average(p => p.Age) : 0;
                this.AvgAgeLabel.Text = $"Avg Age (visible): {avg:F1}";
            });

        this.UpdateCounts();
    }

    private void UpdateCounts()
    {
        this.CountLabel.Text = $"Visible: {_people.Count} / Total: {_source.Count}";
    }

    private void OnAddPerson(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(this.NameEntry.Text))
        {
            var age = int.TryParse(this.AgeEntry.Text, out var ageValue) ? ageValue : 25;
            _source.Add(new PersonWithAge { Name = this.NameEntry.Text, Age = age });
            this.NameEntry.Text = string.Empty;
            this.AgeEntry.Text = string.Empty;
        }
    }

    private void OnIncrementAge(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is PersonWithAge person)
        {
            person.Age++; // AutoRefresh will detect this change
        }
    }

    private void OnRemovePerson(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is PersonWithAge person)
        {
            _source.Remove(person);
        }
    }

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
    {
        _currentFilter = e.NewTextValue ?? string.Empty;
        _predicateSubject.OnNext(p => string.IsNullOrWhiteSpace(_currentFilter)
            || p.Name.Contains(_currentFilter, StringComparison.OrdinalIgnoreCase));
    }

    private void OnFilterAge30Plus(object sender, EventArgs e)
    {
        _predicateSubject.OnNext(p => p.Age >= 30);
    }

    private void OnShowAll(object sender, EventArgs e)
    {
        _currentFilter = string.Empty;
        this.FilterEntry.Text = string.Empty;
        _predicateSubject.OnNext(p => true);
    }

    private void OnSortNameAscending(object sender, EventArgs e)
    {
        _comparerSubject.OnNext(NameAscComparer);
    }

    private void OnSortNameDescending(object sender, EventArgs e)
    {
        _comparerSubject.OnNext(NameDescComparer);
    }

    private void OnSortAgeLowHigh(object sender, EventArgs e)
    {
        _comparerSubject.OnNext(AgeLowHighComparer);
    }

    private void OnSortAgeHighLow(object sender, EventArgs e)
    {
        _comparerSubject.OnNext(AgeHighLowComparer);
    }

    private void OnClear(object sender, EventArgs e)
    {
        _source.Clear();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _bindSubscription?.Dispose();
        _countSubscription?.Dispose();
        _avgAgeSubscription?.Dispose();
        _source.Dispose();
        _predicateSubject.Dispose();
        _comparerSubject.Dispose();
    }
}
