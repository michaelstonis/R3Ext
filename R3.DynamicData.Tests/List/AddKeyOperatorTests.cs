// Tests for AddKey operator (List variant)

using System;
using System.Collections.Generic;
using System.Linq;
using R3.DynamicData.Cache;
using R3.DynamicData.List;
using Xunit;

namespace R3.DynamicData.Tests.List;

public sealed class AddKeyOperatorTests : IDisposable
{
    private readonly SourceList<Person> _source;
    private readonly List<IDisposable> _disposables = new();

    public AddKeyOperatorTests()
    {
        _source = new SourceList<Person>();
        _disposables.Add(_source);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        _source.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void AddKey_ConvertsListToCacheChangeset()
    {
        // Arrange
        var changesList = new List<IChangeSet<Person, int>>();

        var subscription = _source.Connect()
            .AddKey(p => p.Id)
            .Subscribe(changes => changesList.Add(changes));
        _disposables.Add(subscription);

        // Act
        _source.Add(new Person { Id = 1, Name = "Alice", Age = 25 });

        // Assert
        var change = Assert.Single(changesList);
        var item = Assert.Single(change);
        Assert.Equal(Kernel.ChangeReason.Add, item.Reason);
        Assert.Equal(1, item.Key);
        Assert.Equal("Alice", item.Current.Name);
    }

    [Fact]
    public void AddKey_HandlesAddRange()
    {
        // Arrange
        var changesList = new List<IChangeSet<Person, int>>();

        var subscription = _source.Connect()
            .AddKey(p => p.Id)
            .Subscribe(changes => changesList.Add(changes));
        _disposables.Add(subscription);

        // Act
        _source.AddRange(new[]
        {
            new Person { Id = 1, Name = "Alice", Age = 25 },
            new Person { Id = 2, Name = "Bob", Age = 30 },
            new Person { Id = 3, Name = "Charlie", Age = 35 },
        });

        // Assert
        var change = Assert.Single(changesList);
        Assert.Equal(3, change.Count);
        Assert.All(change, c => Assert.Equal(Kernel.ChangeReason.Add, c.Reason));
    }

    [Fact]
    public void AddKey_HandlesRemove()
    {
        // Arrange
        _source.Add(new Person { Id = 1, Name = "Alice", Age = 25 });
        _source.Add(new Person { Id = 2, Name = "Bob", Age = 30 });

        var changesList = new List<IChangeSet<Person, int>>();

        var subscription = _source.Connect()
            .AddKey(p => p.Id)
            .Subscribe(changes => changesList.Add(changes));
        _disposables.Add(subscription);

        changesList.Clear();

        // Act
        _source.RemoveAt(0);

        // Assert
        var change = Assert.Single(changesList);
        var item = Assert.Single(change);
        Assert.Equal(Kernel.ChangeReason.Remove, item.Reason);
        Assert.Equal(1, item.Key);
    }

    [Fact]
    public void AddKey_HandlesReplace()
    {
        // Arrange
        _source.Add(new Person { Id = 1, Name = "Alice", Age = 25 });

        var changesList = new List<IChangeSet<Person, int>>();

        var subscription = _source.Connect()
            .AddKey(p => p.Id)
            .Subscribe(changes => changesList.Add(changes));
        _disposables.Add(subscription);

        changesList.Clear();

        // Act - Replace with same key
        _source.ReplaceAt(0, new Person { Id = 1, Name = "Alice Updated", Age = 26 });

        // Assert
        var change = Assert.Single(changesList);
        var item = Assert.Single(change);
        Assert.Equal(Kernel.ChangeReason.Update, item.Reason);
        Assert.Equal(1, item.Key);
        Assert.Equal("Alice Updated", item.Current.Name);
        Assert.True(item.Previous.HasValue);
        Assert.Equal("Alice", item.Previous.Value.Name);
    }

    [Fact]
    public void AddKey_HandlesReplaceWithDifferentKey()
    {
        // Arrange
        _source.Add(new Person { Id = 1, Name = "Alice", Age = 25 });

        var changesList = new List<IChangeSet<Person, int>>();

        var subscription = _source.Connect()
            .AddKey(p => p.Id)
            .Subscribe(changes => changesList.Add(changes));
        _disposables.Add(subscription);

        changesList.Clear();

        // Act - Replace with different key
        _source.ReplaceAt(0, new Person { Id = 2, Name = "Bob", Age = 30 });

        // Assert
        var change = Assert.Single(changesList);
        Assert.Equal(2, change.Count);

        var remove = change.First(c => c.Reason == Kernel.ChangeReason.Remove);
        Assert.Equal(1, remove.Key);
        Assert.Equal("Alice", remove.Current.Name);

        var add = change.First(c => c.Reason == Kernel.ChangeReason.Add);
        Assert.Equal(2, add.Key);
        Assert.Equal("Bob", add.Current.Name);
    }

    [Fact]
    public void AddKey_HandlesClear()
    {
        // Arrange
        _source.AddRange(new[]
        {
            new Person { Id = 1, Name = "Alice", Age = 25 },
            new Person { Id = 2, Name = "Bob", Age = 30 },
        });

        var changesList = new List<IChangeSet<Person, int>>();

        var subscription = _source.Connect()
            .AddKey(p => p.Id)
            .Subscribe(changes => changesList.Add(changes));
        _disposables.Add(subscription);

        changesList.Clear();

        // Act
        _source.Clear();

        // Assert
        var change = Assert.Single(changesList);
        Assert.Equal(2, change.Count);
        Assert.All(change, c => Assert.Equal(Kernel.ChangeReason.Remove, c.Reason));
    }

    [Fact]
    public void AddKey_HandlesMove()
    {
        // Arrange
        _source.AddRange(new[]
        {
            new Person { Id = 1, Name = "Alice", Age = 25 },
            new Person { Id = 2, Name = "Bob", Age = 30 },
        });

        var changesList = new List<IChangeSet<Person, int>>();

        var subscription = _source.Connect()
            .AddKey(p => p.Id)
            .Subscribe(changes => changesList.Add(changes));
        _disposables.Add(subscription);

        changesList.Clear();

        // Act
        _source.Move(0, 1);

        // Assert
        var change = Assert.Single(changesList);
        var item = Assert.Single(change);
        Assert.Equal(Kernel.ChangeReason.Refresh, item.Reason); // Move is represented as Refresh
    }

    // Test helper class
    private class Person
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }
    }
}
