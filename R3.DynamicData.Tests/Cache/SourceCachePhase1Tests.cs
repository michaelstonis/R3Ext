// Port of DynamicData to R3 - Tests.

using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;
using Xunit;

namespace R3.DynamicData.Tests.Cache;

public class SourceCachePhase1Tests
{
    [Fact]
    public void AddOrUpdateBatch_AddsMultipleItems()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var results = new List<IChangeSet<Person, int>>();
        using var sub = cache.Connect().Subscribe(results.Add);

        cache.AddOrUpdate(new[]
        {
            new Person { Id = 1, Name = "Alice" },
            new Person { Id = 2, Name = "Bob" },
            new Person { Id = 3, Name = "Charlie" },
        });

        Assert.Equal(2, results.Count); // initial empty + batch add
        var lastChanges = results[1];
        Assert.Equal(3, lastChanges.Count);
        Assert.All(lastChanges, c => Assert.Equal(ChangeReason.Add, c.Reason));

        cache.Dispose();
    }

    [Fact]
    public void CountChanged_EmitsOnAddRemove()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var counts = new List<int>();
        using var sub = cache.CountChanged.Subscribe(counts.Add);

        cache.AddOrUpdate(new Person { Id = 1, Name = "Alice" });
        cache.AddOrUpdate(new Person { Id = 2, Name = "Bob" });
        cache.Remove(1);

        Assert.Equal(3, counts.Count);
        Assert.Equal(1, counts[0]);
        Assert.Equal(2, counts[1]);
        Assert.Equal(1, counts[2]);

        cache.Dispose();
    }

    [Fact]
    public void Preview_ReturnsSnapshot()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person { Id = 1, Name = "Alice" });
        cache.AddOrUpdate(new Person { Id = 2, Name = "Bob" });

        var snapshot = cache.Preview();

        Assert.Equal(2, snapshot.Count);
        Assert.Contains(snapshot, p => p.Name == "Alice");
        Assert.Contains(snapshot, p => p.Name == "Bob");

        cache.Dispose();
    }

    [Fact]
    public void Watch_TracksSpecificKey()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var changes = new List<Change<Person, int>>();
        using var sub = cache.Watch(42).Subscribe(changes.Add);

        cache.AddOrUpdate(new Person { Id = 42, Name = "Alice" });
        cache.AddOrUpdate(new Person { Id = 99, Name = "Bob" }); // should not trigger
        cache.AddOrUpdate(new Person { Id = 42, Name = "Alice Updated" });
        cache.Remove(42);

        Assert.Equal(3, changes.Count);
        Assert.Equal(ChangeReason.Add, changes[0].Reason);
        Assert.Equal("Alice", changes[0].Current.Name);
        Assert.Equal(ChangeReason.Update, changes[1].Reason);
        Assert.Equal("Alice Updated", changes[1].Current.Name);
        Assert.Equal(ChangeReason.Remove, changes[2].Reason);

        cache.Dispose();
    }

    private class Person
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
