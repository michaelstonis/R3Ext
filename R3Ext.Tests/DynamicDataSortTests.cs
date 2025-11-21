using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using R3;
using R3.DynamicData.Binding;
using R3.DynamicData.List;
using Xunit;

namespace R3Ext.Tests;

public class DynamicDataSortTests
{
    [Fact]
    public void Sort_WithInitialComparerLoadsExistingItems()
    {
        using var source = new SourceList<TestPerson>();
        source.AddRange(new[]
        {
            new TestPerson("Charlie"),
            new TestPerson("Alice"),
            new TestPerson("Bob"),
        });

        using var comparerSubject = new Subject<IComparer<TestPerson>>();
        var target = new ObservableCollectionExtended<TestPerson>();

        using var subscription = source
            .Connect()
            .Sort(PersonComparers.Ascending, comparerSubject)
            .Bind(target);

        Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, target.Select(p => p.Name));
    }

    [Fact]
    public void Sort_UpdatesOrderWhenComparerChanges()
    {
        using var source = new SourceList<TestPerson>();
        source.AddRange(new[]
        {
            new TestPerson("Charlie"),
            new TestPerson("Alice"),
            new TestPerson("Bob"),
        });

        using var comparerSubject = new Subject<IComparer<TestPerson>>();
        var target = new ObservableCollectionExtended<TestPerson>();

        using var subscription = source
            .Connect()
            .Sort(PersonComparers.Ascending, comparerSubject)
            .Bind(target);

        Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, target.Select(p => p.Name));

        comparerSubject.OnNext(PersonComparers.Descending);

        Assert.Equal(new[] { "Charlie", "Bob", "Alice" }, target.Select(p => p.Name));
    }

    [Fact]
    public void AutoRefresh_ResortsWhenTrackedPropertyChanges()
    {
        using var source = new SourceList<TestPerson>();
        source.AddRange(new[]
        {
            new TestPerson("Charlie"),
            new TestPerson("Alice"),
            new TestPerson("Bob"),
        });

        using var comparerSubject = new Subject<IComparer<TestPerson>>();
        var target = new ObservableCollectionExtended<TestPerson>();

        using var subscription = source
            .Connect()
            .AutoRefresh(p => p.Name)
            .Sort(PersonComparers.Ascending, comparerSubject)
            .Bind(target);

        Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, target.Select(p => p.Name));

        // Move "Bob" to the beginning by changing the observed property.
        var bob = target.Single(p => p.Name == "Bob");
        bob.Name = "Aaron";

        Assert.Equal(new[] { "Aaron", "Alice", "Charlie" }, target.Select(p => p.Name));
    }

    private static class PersonComparers
    {
        public static readonly IComparer<TestPerson> Ascending = Comparer<TestPerson>.Create((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        public static readonly IComparer<TestPerson> Descending = Comparer<TestPerson>.Create((a, b) => string.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TestPerson : INotifyPropertyChanged
    {
        private string _name;

        public TestPerson(string name)
        {
            _name = name;
        }

        public string Name
        {
            get => _name;
            set
            {
                if (string.Equals(_name, value, StringComparison.Ordinal))
                {
                    return;
                }

                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
