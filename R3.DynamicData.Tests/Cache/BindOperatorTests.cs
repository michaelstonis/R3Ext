// Port of DynamicData to R3.

using R3.DynamicData.Cache;

namespace R3.DynamicData.Tests.Cache;

public class BindOperatorTests
{
    [Fact]
    public void Bind_TracksTargetList()
    {
        var source = new SourceCache<Person, int>(p => p.Id);
        var target = new List<Person>();

        using var sub = source.Connect().Bind(target);

        // Add items
        source.AddOrUpdate(new Person { Id = 1, Name = "Alice" });
        source.AddOrUpdate(new Person { Id = 2, Name = "Bob" });
        source.AddOrUpdate(new Person { Id = 3, Name = "Charlie" });

        Assert.Equal(3, target.Count);
        Assert.Contains(target, p => p.Name == "Alice");
        Assert.Contains(target, p => p.Name == "Bob");
        Assert.Contains(target, p => p.Name == "Charlie");

        // Update item
        source.AddOrUpdate(new Person { Id = 2, Name = "Bobby" });
        Assert.Equal(3, target.Count);
        Assert.Contains(target, p => p.Name == "Bobby");
        Assert.DoesNotContain(target, p => p.Name == "Bob");

        // Remove item
        source.Remove(1);
        Assert.Equal(2, target.Count);
        Assert.DoesNotContain(target, p => p.Name == "Alice");

        // Clear
        source.Clear();
        Assert.Empty(target);
    }

    [Fact]
    public void Bind_OutReadOnlyObservableCollection_Works()
    {
        var source = new SourceCache<Person, int>(p => p.Id);
        using var sub = source.Connect().Bind(out var readOnlyCollection);

        source.AddOrUpdate(new Person { Id = 1, Name = "Alice" });
        source.AddOrUpdate(new Person { Id = 2, Name = "Bob" });
        source.AddOrUpdate(new Person { Id = 3, Name = "Charlie" });

        Assert.Equal(3, readOnlyCollection.Count);
        Assert.Contains(readOnlyCollection, p => p.Name == "Alice");

        source.Clear();
        Assert.Empty(readOnlyCollection);
    }

    [Fact]
    public void Bind_ObservableCollectionExtended_Works()
    {
        var source = new SourceCache<Person, int>(p => p.Id);
        var target = new Binding.ObservableCollectionExtended<Person>();
        using var sub = source.Connect().Bind(target);

        source.AddOrUpdate(new Person { Id = 1, Name = "Alice" });
        source.AddOrUpdate(new Person { Id = 2, Name = "Bob" });
        source.AddOrUpdate(new Person { Id = 3, Name = "Charlie" });

        Assert.Equal(3, target.Count);
        Assert.Contains(target, p => p.Name == "Alice");

        source.Clear();
        Assert.Empty(target);
    }

    [Fact]
    public void Bind_HandlesUpdatesCorrectly()
    {
        var source = new SourceCache<Person, int>(p => p.Id);
        var target = new List<Person>();
        using var sub = source.Connect().Bind(target);

        // Add initial item
        source.AddOrUpdate(new Person { Id = 1, Name = "Alice", Age = 25 });
        Assert.Single(target);
        Assert.Equal("Alice", target[0].Name);
        Assert.Equal(25, target[0].Age);

        // Update the item
        source.AddOrUpdate(new Person { Id = 1, Name = "Alice", Age = 26 });
        Assert.Single(target);
        Assert.Equal("Alice", target[0].Name);
        Assert.Equal(26, target[0].Age);
    }

    [Fact]
    public void Bind_BatchOperations_Work()
    {
        var source = new SourceCache<Person, int>(p => p.Id);
        var target = new List<Person>();
        using var sub = source.Connect().Bind(target);

        // Batch add
        source.AddOrUpdate(new[]
        {
            new Person { Id = 1, Name = "Alice" },
            new Person { Id = 2, Name = "Bob" },
            new Person { Id = 3, Name = "Charlie" },
        });

        Assert.Equal(3, target.Count);

        // Batch update
        source.AddOrUpdate(new[]
        {
            new Person { Id = 1, Name = "Alicia" },
            new Person { Id = 2, Name = "Bobby" },
        });

        Assert.Equal(3, target.Count);
        Assert.Contains(target, p => p.Name == "Alicia");
        Assert.Contains(target, p => p.Name == "Bobby");
    }

    private class Person
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }
    }
}
