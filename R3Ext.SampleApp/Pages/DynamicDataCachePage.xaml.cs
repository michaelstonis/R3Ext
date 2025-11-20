using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using R3;
using R3.DynamicData.Cache;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.SampleApp;

#pragma warning disable CA1001
public partial class DynamicDataCachePage : ContentPage
#pragma warning restore CA1001
{
    private readonly SourceCache<PersonWithId, int> _cache;
    private readonly Random _random = new();
    private ReadOnlyObservableCollection<PersonWithId> _people = null!;
    private IDisposable? _bindSubscription;
    private IDisposable? _countSubscription;
    private IDisposable? _watchSubscription;
    private int _currentWatchId = -1;

    public DynamicDataCachePage()
    {
        this.InitializeComponent();

        // Create cache with Id as key
        _cache = new SourceCache<PersonWithId, int>(p => p.Id);

        // Bind to collection view
        _bindSubscription = _cache
            .Connect()
            .Bind(out _people);

        this.PeopleView.ItemsSource = _people;

        // Subscribe to count changes
        _countSubscription = _cache.CountChanged
            .Subscribe(count => this.CountLabel.Text = $"Count: {count}");

        // Add initial demo data
        this.OnResetDemoData(this, EventArgs.Empty);
    }

    private void OnAddOrUpdatePerson(object sender, EventArgs e)
    {
        if (int.TryParse(this.IdEntry.Text, out var id) &&
            !string.IsNullOrWhiteSpace(this.NameEntry.Text))
        {
            var age = int.TryParse(this.AgeEntry.Text, out var ageValue) ? ageValue : 25;
            var city = string.IsNullOrWhiteSpace(this.CityEntry.Text) ? "Unknown" : this.CityEntry.Text;

            var person = new PersonWithId
            {
                Id = id,
                Name = this.NameEntry.Text,
                Age = age,
                City = city,
            };

            _cache.AddOrUpdate(person);

            // Clear inputs
            this.IdEntry.Text = string.Empty;
            this.NameEntry.Text = string.Empty;
            this.AgeEntry.Text = string.Empty;
            this.CityEntry.Text = string.Empty;
        }
    }

    private void OnAddBatch(object sender, EventArgs e)
    {
        var startId = _cache.Items.Any() ? _cache.Items.Max(p => p.Id) + 1 : 100;
        var names = new[] { "Alex", "Sam", "Jordan", "Taylor", "Morgan", "Casey", "Riley", "Avery" };
        var cities = new[] { "New York", "London", "Tokyo", "Paris", "Sydney", "Toronto", "Berlin", "Madrid" };

        var batch = Enumerable.Range(0, 3).Select(i => new PersonWithId
        {
            Id = startId + i,
            Name = names[_random.Next(names.Length)],
            Age = _random.Next(18, 65),
            City = cities[_random.Next(cities.Length)],
        }).ToArray();

        _cache.AddOrUpdate(batch);
    }

    private void OnRemovePerson(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is int id)
        {
            _cache.Remove(id);
        }
    }

    private void OnClearAll(object sender, EventArgs e)
    {
        _cache.Clear();
        this.PreviewLabel.Text = "Preview: None";
    }

    private void OnResetDemoData(object sender, EventArgs e)
    {
        _cache.Clear();
        _cache.AddOrUpdate(new[]
        {
            new PersonWithId { Id = 1, Name = "Alice Johnson", Age = 28, City = "New York" },
            new PersonWithId { Id = 2, Name = "Bob Smith", Age = 35, City = "London" },
            new PersonWithId { Id = 3, Name = "Charlie Davis", Age = 42, City = "Tokyo" },
            new PersonWithId { Id = 4, Name = "Diana Prince", Age = 31, City = "Paris" },
            new PersonWithId { Id = 5, Name = "Eve Miller", Age = 26, City = "Sydney" },
        });
    }

    private void OnRefreshPreview(object sender, EventArgs e)
    {
        var snapshot = _cache.Preview();
        if (snapshot.Any())
        {
            var summary = string.Join(", ", snapshot.Take(3).Select(p => p.Name));
            var more = snapshot.Count() > 3 ? $"... +{snapshot.Count() - 3} more" : string.Empty;
            this.PreviewLabel.Text = $"Preview: {summary}{more}";
        }
        else
        {
            this.PreviewLabel.Text = "Preview: Empty";
        }
    }

    private void OnStartWatching(object sender, EventArgs e)
    {
        if (int.TryParse(this.WatchIdEntry.Text, out var id))
        {
            // Dispose previous watch
            _watchSubscription?.Dispose();
            _currentWatchId = id;

            // Watch specific key
            _watchSubscription = _cache.Watch(id)
                .Subscribe(change =>
                {
                    var action = change.Reason switch
                    {
                        R3.DynamicData.Kernel.ChangeReason.Add => "Added",
                        R3.DynamicData.Kernel.ChangeReason.Update => "Updated",
                        R3.DynamicData.Kernel.ChangeReason.Remove => "Removed",
                        _ => "Changed",
                    };
                    this.WatchLabel.Text = $"ID {id} {action}: {change.Current.Name} ({change.Current.City})";
                    this.WatchLabel.TextColor = Microsoft.Maui.Graphics.Colors.Green;
                });

            this.WatchLabel.Text = $"Watching ID {id}...";
            this.WatchLabel.TextColor = Microsoft.Maui.Graphics.Colors.Blue;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _bindSubscription?.Dispose();
        _countSubscription?.Dispose();
        _watchSubscription?.Dispose();
        _cache.Dispose();
    }
}
