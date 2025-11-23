using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using R3;
using R3.DynamicData.List;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.SampleApp;

#pragma warning disable CA1001
public partial class DDTransformationPage : ContentPage
#pragma warning restore CA1001
{
    private readonly SourceList<PersonWithHobbies> _source = new();
    private readonly ReadOnlyObservableCollection<string> _transformedItems = null!;
    private readonly ReadOnlyObservableCollection<string> _asyncTransformedItems = null!;
    private readonly ReadOnlyObservableCollection<string> _transformManyItems = null!;
    private readonly IDisposable _transformSubscription;
    private readonly IDisposable _asyncTransformSubscription;
    private readonly IDisposable _transformManySubscription;

    public DDTransformationPage()
    {
        InitializeComponent();

        // Transform: Person → DisplayName
        _transformSubscription = _source.Connect()
            .Transform(p => $"{p.Name} (Age {p.Age})")
            .Bind(out _transformedItems);
        TransformView.ItemsSource = _transformedItems;

        // TransformAsync: Simulate loading avatar URL
        _asyncTransformSubscription = _source.Connect()
            .TransformAsync(async (p, ct) =>
            {
                await Task.Delay(100, ct); // Simulate async loading
                return $"{p.Name}'s Avatar: https://example.com/avatar/{p.Name.ToLower()}";
            })
            .Bind(out _asyncTransformedItems);
        TransformAsyncView.ItemsSource = _asyncTransformedItems;

        // TransformMany: Person → Hobbies (flattens all hobbies)
        _transformManySubscription = _source.Connect()
            .TransformMany(p => p.Hobbies)
            .Bind(out _transformManyItems);
        TransformManyView.ItemsSource = _transformManyItems;
    }

    private void OnAddPerson(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(NameEntry.Text) && int.TryParse(AgeEntry.Text, out var age))
        {
            _source.Add(new PersonWithHobbies
            {
                Name = NameEntry.Text,
                Age = age,
                City = "Unknown",
                Hobbies = GenerateRandomHobbies(),
            });
            NameEntry.Text = string.Empty;
            AgeEntry.Text = string.Empty;
        }
    }

    private void OnAddSampleData(object sender, EventArgs e)
    {
        _source.AddRange(new[]
        {
            new PersonWithHobbies { Name = "Alice", Age = 28, City = "New York", Hobbies = new List<string> { "Reading", "Gaming" } },
            new PersonWithHobbies { Name = "Bob", Age = 35, City = "London", Hobbies = new List<string> { "Cooking", "Hiking" } },
            new PersonWithHobbies { Name = "Carol", Age = 42, City = "Tokyo", Hobbies = new List<string> { "Painting", "Yoga", "Photography" } },
            new PersonWithHobbies { Name = "Dave", Age = 30, City = "Paris", Hobbies = new List<string> { "Music", "Sports" } },
        });
    }

    private void OnClear(object sender, EventArgs e)
    {
        _source.Clear();
    }

    private static List<string> GenerateRandomHobbies()
    {
        var allHobbies = new[] { "Reading", "Gaming", "Cooking", "Hiking", "Painting", "Yoga", "Music", "Sports", "Photography", "Dancing" };
        var count = Random.Shared.Next(1, 4);
        return allHobbies.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
    }
}
