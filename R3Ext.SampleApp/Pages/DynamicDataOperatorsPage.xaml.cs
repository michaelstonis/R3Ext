using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using R3.DynamicData.List;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.SampleApp;

#pragma warning disable CA1001
public partial class DynamicDataOperatorsPage : ContentPage
#pragma warning restore CA1001
{
    private readonly SourceList<PersonWithHobbies> _source = new();
    private readonly Random _random = new();
    
    private ReadOnlyObservableCollection<string> _transformedPeople = null!;
    private ReadOnlyObservableCollection<Group<string, PersonWithHobbies>> _groupedPeople = null!;
    private ReadOnlyObservableCollection<string> _distinctCities = null!;
    private ReadOnlyObservableCollection<string> _allHobbies = null!;
    
    private IDisposable? _transformSub;
    private IDisposable? _groupSub;
    private IDisposable? _distinctSub;
    private IDisposable? _hobbiesSub;
    private IDisposable? _sourceCountSub;
    private IDisposable? _transformCountSub;
    private IDisposable? _groupCountSub;
    private IDisposable? _distinctCountSub;
    private IDisposable? _hobbiesCountSub;

    private readonly string[] _names = { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Henry", "Ivy", "Jack" };
    private readonly string[] _cities = { "New York", "London", "Tokyo", "Paris", "Sydney" };
    private readonly string[] _hobbies = { "Reading", "Gaming", "Cooking", "Hiking", "Photography", "Music", "Sports", "Art" };

    public DynamicDataOperatorsPage()
    {
        this.InitializeComponent();

        // Transform operator: Person -> Display String
        _transformSub = _source
            .Connect()
            .Transform(p => $"{p.Name} from {p.City} ({p.Age})")
            .Bind(out _transformedPeople);
        this.TransformView.ItemsSource = _transformedPeople;

        // Group operator: Group by City
        _groupSub = _source
            .Connect()
            .Group(p => p.City)
            .Bind(out _groupedPeople);
        this.GroupView.ItemsSource = _groupedPeople;

        // DistinctValues operator: Get unique cities
        _distinctSub = _source
            .Connect()
            .DistinctValues(p => p.City)
            .Bind(out _distinctCities);

        // Subscribe to update the FlexLayout with badge-style labels
        _distinctSub = _distinctCities
            .ToObservable()
            .Subscribe(_ => this.UpdateDistinctCitiesDisplay());

        // TransformMany operator: Flatten all hobbies
        _hobbiesSub = _source
            .Connect()
            .TransformMany(p => p.Hobbies)
            .Bind(out _allHobbies);
        this.HobbiesView.ItemsSource = _allHobbies;

        // Subscribe to counts
        _sourceCountSub = _source.CountChanged
            .Subscribe(count => this.SourceCountLabel.Text = $"Source: {count} people");

        _transformCountSub = _transformedPeople
            .ToObservable()
            .Subscribe(_ => this.TransformCountLabel.Text = $"Transformed: {_transformedPeople.Count}");

        _groupCountSub = _groupedPeople
            .ToObservable()
            .Subscribe(_ => this.GroupCountLabel.Text = $"Groups: {_groupedPeople.Count}");

        _distinctCountSub = _distinctCities
            .ToObservable()
            .Subscribe(_ => this.DistinctCountLabel.Text = $"Unique Cities: {_distinctCities.Count}");

        _hobbiesCountSub = _allHobbies
            .ToObservable()
            .Subscribe(_ => this.HobbiesCountLabel.Text = $"Total Hobbies: {_allHobbies.Count}");

        // Add initial data
        this.OnAddMultiple(this, EventArgs.Empty);
    }

    private void OnAddPerson(object sender, EventArgs e)
    {
        var person = this.CreateRandomPerson();
        _source.Add(person);
    }

    private void OnAddMultiple(object sender, EventArgs e)
    {
        var people = Enumerable.Range(0, 5)
            .Select(_ => this.CreateRandomPerson())
            .ToArray();
        _source.AddRange(people);
    }

    private void OnClear(object sender, EventArgs e)
    {
        _source.Clear();
    }

    private PersonWithHobbies CreateRandomPerson()
    {
        var name = _names[_random.Next(_names.Length)];
        var city = _cities[_random.Next(_cities.Length)];
        var age = _random.Next(18, 65);
        var hobbyCount = _random.Next(1, 4);
        var hobbies = Enumerable.Range(0, hobbyCount)
            .Select(_ => _hobbies[_random.Next(_hobbies.Length)])
            .Distinct()
            .ToList();

        return new PersonWithHobbies
        {
            Name = name,
            City = city,
            Age = age,
            Hobbies = hobbies
        };
    }

    private void UpdateDistinctCitiesDisplay()
    {
        this.DistinctCitiesLayout.Children.Clear();
        
        foreach (var city in _distinctCities)
        {
            var frame = new Frame
            {
                Padding = new Thickness(8, 4),
                Margin = new Thickness(4),
                CornerRadius = 12,
                HasShadow = false,
                BackgroundColor = Microsoft.Maui.Graphics.Colors.Green,
                Content = new Label
                {
                    Text = city,
                    TextColor = Microsoft.Maui.Graphics.Colors.White,
                    FontSize = 12
                }
            };
            this.DistinctCitiesLayout.Children.Add(frame);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _transformSub?.Dispose();
        _groupSub?.Dispose();
        _distinctSub?.Dispose();
        _hobbiesSub?.Dispose();
        _sourceCountSub?.Dispose();
        _transformCountSub?.Dispose();
        _groupCountSub?.Dispose();
        _distinctCountSub?.Dispose();
        _hobbiesCountSub?.Dispose();
        _source.Dispose();
    }
}
