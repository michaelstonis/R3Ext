using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.List;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.SampleApp;

#pragma warning disable SA1649
#pragma warning disable CA1001
public partial class DynamicDataTransformManyPage : ContentPage
#pragma warning restore CA1001
{
    private readonly SourceCache<PersonWithTags, int> _peopleCache;
    private readonly List<IDisposable> _subscriptions = new();
    private int _nextId = 1;
    private bool _useDedupe = false;

    public DynamicDataTransformManyPage()
    {
        InitializeComponent();
        _peopleCache = new SourceCache<PersonWithTags, int>(p => p.Id);
        SetupBindings();
    }

    private void SetupBindings()
    {
        // Bind people to the people view
        var peopleCollection = new ObservableCollection<PersonWithTags>();
        PeopleView.ItemsSource = peopleCollection;
        _subscriptions.Add(_peopleCache.Connect().Bind(peopleCollection));

        // Set up initial binding without dedup
        UpdateTransformManyBinding();

        // Track people count
        _subscriptions.Add(
            _peopleCache.Connect()
                .QueryWhenChanged(q => q.Count)
                .Subscribe(count => PeopleCountLabel.Text = count.ToString()));
    }

    private void UpdateTransformManyBinding()
    {
        // Create new observable collection for tags
        var tagsCollection = new ObservableCollection<string>();
        TagsView.ItemsSource = tagsCollection;

        // Create TransformMany with or without deduplication
        var tagsObservable = _useDedupe
            ? _peopleCache.Connect().TransformMany(p => p.Tags, EqualityComparer<string>.Default)
            : _peopleCache.Connect().TransformMany(p => p.Tags);

        // Bind to collection and track count
        _subscriptions.Add(tagsObservable.Bind(tagsCollection));

        _subscriptions.Add(
            tagsObservable
                .Subscribe(changes =>
                {
                    TagsCountLabel.Text = tagsCollection.Count.ToString();
                }));
    }

    private void OnAddPerson(object? sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = $"Person {_nextId}";
        }

        var tagsText = TagsEntry.Text?.Trim() ?? string.Empty;
        var tags = string.IsNullOrEmpty(tagsText)
            ? new List<string> { "general" }
            : tagsText.Split(',').Select(t => t.Trim().ToLower()).Where(t => !string.IsNullOrEmpty(t)).ToList();

        var person = new PersonWithTags
        {
            Id = _nextId++,
            Name = name,
            Tags = tags,
        };

        _peopleCache.AddOrUpdate(person);

        // Clear inputs
        NameEntry.Text = string.Empty;
        TagsEntry.Text = string.Empty;
    }

    private void OnAddBatch(object? sender, EventArgs e)
    {
        var sampleData = new[]
        {
            new { Name = "Alice", Tags = new[] { "coding", "gaming", "reading" } },
            new { Name = "Bob", Tags = new[] { "gaming", "sports", "music" } },
            new { Name = "Charlie", Tags = new[] { "coding", "music", "art" } },
        };

        foreach (var data in sampleData)
        {
            _peopleCache.AddOrUpdate(new PersonWithTags
            {
                Id = _nextId++,
                Name = data.Name,
                Tags = data.Tags.ToList(),
            });
        }
    }

    private void OnRemovePerson(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is PersonWithTags person)
        {
            _peopleCache.Remove(person.Id);
        }
    }

    private void OnClearAll(object? sender, EventArgs e)
    {
        _peopleCache.Clear();
        _nextId = 1;
    }

    private void OnDedupToggled(object? sender, ToggledEventArgs e)
    {
        _useDedupe = e.Value;
        DedupStatusLabel.Text = _useDedupe
            ? "Mode: Deduplication Enabled (reference counting)"
            : "Mode: Allow Duplicates";

        // Rebuild the TransformMany binding with new mode
        UpdateTransformManyBinding();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        foreach (var sub in _subscriptions)
        {
            sub?.Dispose();
        }

        _subscriptions.Clear();
        _peopleCache.Dispose();
    }
}

public class PersonWithTags : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private int _id;
    private string _name = string.Empty;
    private List<string> _tags = new();

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public List<string> Tags
    {
        get => _tags;
        set => SetProperty(ref _tags, value);
    }
}
