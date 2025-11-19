// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;
using R3.DynamicData.Operators;

namespace R3.DynamicData.Tests;

public class FilterOperatorTests
{
    private record Person(int Id, string Name, int Age);

    [Fact]
    public void Filter_FiltersInitialItems()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person(1, "Alice", 30));
        cache.AddOrUpdate(new Person(2, "Bob", 25));
        cache.AddOrUpdate(new Person(3, "Charlie", 35));
        var changesList = new List<IChangeSet<Person, int>>();
        using var subscription = cache.Connect()
            .Filter(p => p.Age > 30)
            .Subscribe(changes => changesList.Add(changes));

        // Act
        cache.AddOrUpdate(new Person(4, "David", 40));

        // Assert
        Assert.Equal(2, changesList.Count); // Initial filtered snapshot + new add
        Assert.Equal(1, changesList[0].Adds);
        Assert.Equal("Charlie", changesList[0].First().Current.Name);
        Assert.Equal(1, changesList[1].Adds);
        Assert.Equal("David", changesList[1].First().Current.Name);
    }

    [Fact]
    public void Filter_AddsItemWhenMatchesFilter()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        var results = new List<IChangeSet<Person, int>>();

        using var subscription = cache.Connect()
            .Filter(p => p.Age > 30)
            .Subscribe(changes => results.Add(changes));

        // Act
        cache.AddOrUpdate(new Person(1, "Alice", 35));

        // Assert
        Assert.Single(results);
        Assert.Equal(1, results[0].Adds);
        Assert.Equal("Alice", results[0].First().Current.Name);
    }

    [Fact]
    public void Filter_DoesNotAddItemWhenDoesNotMatchFilter()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        var results = new List<IChangeSet<Person, int>>();

        using var subscription = cache.Connect()
            .Filter(p => p.Age > 30)
            .Subscribe(changes => results.Add(changes));

        // Act
        cache.AddOrUpdate(new Person(1, "Alice", 25));

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void Filter_UpdatesItemWhenStillMatchesFilter()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person(1, "Alice", 35));
        var results = new List<IChangeSet<Person, int>>();
        using var subscription = cache.Connect()
            .Filter(p => p.Age > 30)
            .Subscribe(changes => results.Add(changes));

        // Act
        cache.AddOrUpdate(new Person(1, "Alice", 36));

        // Assert
        Assert.Equal(2, results.Count); // Initial add + update
        Assert.Equal(1, results[0].Adds);
        Assert.Equal(1, results[1].Updates);
        Assert.Equal(36, results[1].First().Current.Age);
    }

    [Fact]
    public void Filter_RemovesItemWhenNoLongerMatchesFilter()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person(1, "Alice", 35));
        var results = new List<IChangeSet<Person, int>>();
        using var subscription = cache.Connect()
            .Filter(p => p.Age > 30)
            .Subscribe(changes => results.Add(changes));

        // Act
        cache.AddOrUpdate(new Person(1, "Alice", 25));

        // Assert
        Assert.Equal(2, results.Count); // Initial add + removal
        Assert.Equal(1, results[0].Adds);
        Assert.Equal(1, results[1].Removes);
    }

    [Fact]
    public void Filter_AddsItemWhenUpdatedToMatchFilter()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person(1, "Alice", 25));

        var results = new List<IChangeSet<Person, int>>();
        using var subscription = cache.Connect()
            .Filter(p => p.Age > 30)
            .Subscribe(changes => results.Add(changes));

        // Act
        cache.AddOrUpdate(new Person(1, "Alice", 35));

        // Assert
        Assert.Single(results);
        Assert.Equal(1, results[0].Adds);
        Assert.Equal(35, results[0].First().Current.Age);
    }

    [Fact]
    public void Filter_RemovesItemWhenDeleted()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person(1, "Alice", 35));
        var results = new List<IChangeSet<Person, int>>();
        using var subscription = cache.Connect()
            .Filter(p => p.Age > 30)
            .Subscribe(changes => results.Add(changes));

        // Act
        cache.Remove(1);

        // Assert
        Assert.Equal(2, results.Count); // Initial add + removal
        Assert.Equal(1, results[0].Adds);
        Assert.Equal(1, results[1].Removes);
    }

    [Fact]
    public void Filter_DynamicPredicate_RefiltersOnPredicateChange()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person(1, "Alice", 25));
        cache.AddOrUpdate(new Person(2, "Bob", 35));
        cache.AddOrUpdate(new Person(3, "Charlie", 45));

        var predicateSubject = new Subject<Func<Person, bool>>();
        var results = new List<IChangeSet<Person, int>>();

        using var subscription = cache.Connect()
            .Filter(predicateSubject)
            .Subscribe(changes => results.Add(changes));

        // Act - First predicate: Age > 30
        predicateSubject.OnNext(p => p.Age > 30);

        Assert.Single(results);
        Assert.Equal(2, results[0].Adds);

        // Act - Second predicate: Age > 40
        predicateSubject.OnNext(p => p.Age > 40);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal(0, results[1].Adds); // Charlie already present, no re-add
        Assert.Equal(1, results[1].Removes); // Bob (35) removed
    }

    [Fact]
    public void Filter_DynamicPredicate_HandlesItemUpdates()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        var predicateSubject = new Subject<Func<Person, bool>>();
        var results = new List<IChangeSet<Person, int>>();

        using var subscription = cache.Connect()
            .Filter(predicateSubject)
            .Subscribe(changes => results.Add(changes));

        predicateSubject.OnNext(p => p.Age > 30);
        results.Clear(); // Clear initial changes

        // Act
        cache.AddOrUpdate(new Person(1, "Alice", 35)); // Matches filter
        cache.AddOrUpdate(new Person(2, "Bob", 25)); // Does not match filter

        // Assert
        Assert.Single(results);
        Assert.Equal(1, results[0].Adds);
        Assert.Equal("Alice", results[0].First().Current.Name);
    }
}
