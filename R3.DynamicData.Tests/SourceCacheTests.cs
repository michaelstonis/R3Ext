// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;

namespace R3.DynamicData.Tests;

public class SourceCacheTests
{
    private record Person(int Id, string Name, int Age);

    [Fact]
    public void AddOrUpdate_AddsNewItem()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        var person = new Person(1, "Alice", 30);
        IChangeSet<Person, int>? receivedChanges = null;

        using var subscription = cache.Connect().Subscribe(changes => receivedChanges = changes);

        // Act
        cache.AddOrUpdate(person);

        // Assert
        Assert.NotNull(receivedChanges);
        Assert.Equal(1, receivedChanges.Count);
        Assert.Equal(1, receivedChanges.Adds);
        Assert.Equal(0, receivedChanges.Updates);

        var change = receivedChanges.First();
        Assert.Equal(ChangeReason.Add, change.Reason);
        Assert.Equal(1, change.Key);
        Assert.Equal(person, change.Current);
    }

    [Fact]
    public void AddOrUpdate_UpdatesExistingItem()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        var person1 = new Person(1, "Alice", 30);
        var person2 = new Person(1, "Alice", 31);
        var changesList = new List<IChangeSet<Person, int>>();
        cache.AddOrUpdate(person1);
        using var subscription = cache.Connect().Subscribe(changes => changesList.Add(changes));

        // Act
        cache.AddOrUpdate(person2);

        // Assert
        Assert.Equal(2, changesList.Count); // Initial snapshot + update
        var initial = changesList[0];
        Assert.Equal(1, initial.Adds);
        var updated = changesList[1];
        Assert.Equal(1, updated.Updates);
        var change = updated.First();
        Assert.Equal(ChangeReason.Update, change.Reason);
        Assert.Equal(person1, change.Previous.Value);
        Assert.Equal(person2, change.Current);
    }

    [Fact]
    public void Remove_RemovesExistingItem()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        var person = new Person(1, "Alice", 30);
        var changesList = new List<IChangeSet<Person, int>>();
        cache.AddOrUpdate(person);
        using var subscription = cache.Connect().Subscribe(changes => changesList.Add(changes));

        // Act
        cache.Remove(1);

        // Assert
        Assert.Equal(2, changesList.Count); // Initial add + remove
        Assert.Equal(1, changesList[0].Adds);
        Assert.Equal(1, changesList[1].Removes);
        var removeChange = changesList[1].First();
        Assert.Equal(ChangeReason.Remove, removeChange.Reason);
        Assert.Equal(person, removeChange.Current);
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person(1, "Alice", 30));
        cache.AddOrUpdate(new Person(2, "Bob", 25));
        cache.AddOrUpdate(new Person(3, "Charlie", 35));
        var changesList = new List<IChangeSet<Person, int>>();
        using var subscription = cache.Connect().Subscribe(changes => changesList.Add(changes));

        // Act
        cache.Clear();

        // Assert
        Assert.Equal(2, changesList.Count); // Initial adds + removals
        Assert.Equal(3, changesList[0].Adds);
        Assert.Equal(3, changesList[1].Removes);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Edit_BatchesMultipleOperations()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        IChangeSet<Person, int>? receivedChanges = null;
        using var subscription = cache.Connect().Subscribe(changes => receivedChanges = changes);

        // Act
        cache.Edit(updater =>
        {
            updater.AddOrUpdate(new Person(1, "Alice", 30));
            updater.AddOrUpdate(new Person(2, "Bob", 25));
            updater.AddOrUpdate(new Person(3, "Charlie", 35));
        });

        // Assert
        Assert.NotNull(receivedChanges);
        Assert.Equal(3, receivedChanges.Count);
        Assert.Equal(3, receivedChanges.Adds);
        Assert.Equal(3, cache.Count);
    }

    [Fact]
    public void Lookup_ReturnsValueForExistingKey()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        var person = new Person(1, "Alice", 30);
        cache.AddOrUpdate(person);

        // Act
        var result = cache.Lookup(1);

        // Assert
        Assert.True(result.HasValue);
        Assert.Equal(person, result.Value);
    }

    [Fact]
    public void Lookup_ReturnsNoneForMissingKey()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);

        // Act
        var result = cache.Lookup(999);

        // Assert
        Assert.False(result.HasValue);
    }

    [Fact]
    public void Count_ReturnsCorrectValue()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);

        // Act & Assert
        Assert.Equal(0, cache.Count);

        cache.AddOrUpdate(new Person(1, "Alice", 30));
        Assert.Equal(1, cache.Count);

        cache.AddOrUpdate(new Person(2, "Bob", 25));
        Assert.Equal(2, cache.Count);

        cache.Remove(1);
        Assert.Equal(1, cache.Count);

        cache.Clear();
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Keys_ReturnsAllKeys()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person(1, "Alice", 30));
        cache.AddOrUpdate(new Person(2, "Bob", 25));
        cache.AddOrUpdate(new Person(3, "Charlie", 35));

        // Act
        var keys = cache.Keys.ToList();

        // Assert
        Assert.Equal(3, keys.Count);
        Assert.Contains(1, keys);
        Assert.Contains(2, keys);
        Assert.Contains(3, keys);
    }

    [Fact]
    public void Items_ReturnsAllItems()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        var person1 = new Person(1, "Alice", 30);
        var person2 = new Person(2, "Bob", 25);

        cache.AddOrUpdate(person1);
        cache.AddOrUpdate(person2);

        // Act
        var items = cache.Items.ToList();

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Contains(person1, items);
        Assert.Contains(person2, items);
    }

    [Fact]
    public void RemoveKeys_RemovesMatchingKeys()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person(1, "Alice", 30));
        cache.AddOrUpdate(new Person(2, "Bob", 25));
        cache.AddOrUpdate(new Person(3, "Charlie", 35));
        var changesList = new List<IChangeSet<Person, int>>();
        using var subscription = cache.Connect().Subscribe(changes => changesList.Add(changes));

        // Act
        cache.Edit(updater => updater.RemoveKeys(key => key > 1));

        // Assert
        Assert.Equal(2, changesList.Count); // Initial adds + removals
        Assert.Equal(3, changesList[0].Adds);
        Assert.Equal(2, changesList[1].Removes);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void RemoveItems_RemovesMatchingItems()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person(1, "Alice", 30));
        cache.AddOrUpdate(new Person(2, "Bob", 25));
        cache.AddOrUpdate(new Person(3, "Charlie", 35));
        var changesList = new List<IChangeSet<Person, int>>();
        using var subscription = cache.Connect().Subscribe(changes => changesList.Add(changes));

        // Act
        cache.Edit(updater => updater.RemoveItems(p => p.Age < 30));

        // Assert
        Assert.Equal(2, changesList.Count);
        Assert.Equal(3, changesList[0].Adds);
        Assert.Equal(1, changesList[1].Removes);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void Refresh_EmitsRefreshChange()
    {
        // Arrange
        var cache = new SourceCache<Person, int>(p => p.Id);
        var person = new Person(1, "Alice", 30);
        cache.AddOrUpdate(person);
        var changesList = new List<IChangeSet<Person, int>>();
        using var subscription = cache.Connect().Subscribe(changes => changesList.Add(changes));

        // Act
        cache.Edit(updater => updater.Refresh(1));

        // Assert
        Assert.Equal(2, changesList.Count); // Initial add + refresh
        Assert.Equal(1, changesList[0].Adds);
        Assert.Equal(1, changesList[1].Refreshes);
        var refreshChange = changesList[1].First();
        Assert.Equal(ChangeReason.Refresh, refreshChange.Reason);
        Assert.Equal(person, refreshChange.Current);
    }
}
