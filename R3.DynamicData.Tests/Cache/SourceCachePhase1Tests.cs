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

        Assert.Single(results); // no initial snapshot for empty cache
        var changes = results[0];
        Assert.Equal(3, changes.Count);
        Assert.All(changes, c => Assert.Equal(ChangeReason.Add, c.Reason));

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

    [Fact]
    public void Watch_DoesNotEmitForNonExistentKey()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var changes = new List<Change<Person, int>>();
        using var sub = cache.Watch(999).Subscribe(changes.Add);

        cache.AddOrUpdate(new Person { Id = 1, Name = "Alice" });
        cache.AddOrUpdate(new Person { Id = 2, Name = "Bob" });
        cache.Remove(1);

        Assert.Empty(changes);

        cache.Dispose();
    }

    [Fact]
    public void Watch_CompletesWhenCacheDisposed()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var completed = false;
        using var sub = cache.Watch(1).Subscribe(_ => { }, _ => completed = true);

        cache.AddOrUpdate(new Person { Id = 1, Name = "Alice" });
        cache.Dispose();

        Assert.True(completed);
    }

    [Fact]
    public void CountChanged_EmitsZeroOnClear()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var counts = new List<int>();
        using var sub = cache.CountChanged.Subscribe(counts.Add);

        cache.AddOrUpdate(new Person { Id = 1, Name = "Alice" });
        cache.AddOrUpdate(new Person { Id = 2, Name = "Bob" });
        cache.Clear();

        Assert.Equal(3, counts.Count);
        Assert.Equal(1, counts[0]);
        Assert.Equal(2, counts[1]);
        Assert.Equal(0, counts[2]);

        cache.Dispose();
    }

    [Fact]
    public void CountChanged_EmitsOnUpdate()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var counts = new List<int>();
        using var sub = cache.CountChanged.Subscribe(counts.Add);

        cache.AddOrUpdate(new Person { Id = 1, Name = "Alice" });
        cache.AddOrUpdate(new Person { Id = 1, Name = "Alice Updated" }); // update still emits count

        Assert.Equal(2, counts.Count);
        Assert.Equal(1, counts[0]);
        Assert.Equal(1, counts[1]); // count unchanged but emitted

        cache.Dispose();
    }

    [Fact]
    public void AddOrUpdateBatch_MixedAddAndUpdate()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person { Id = 1, Name = "Alice" });

        var results = new List<IChangeSet<Person, int>>();
        using var sub = cache.Connect().Subscribe(results.Add);

        cache.AddOrUpdate(new[]
        {
            new Person { Id = 1, Name = "Alice Updated" }, // update
            new Person { Id = 2, Name = "Bob" }, // add
            new Person { Id = 3, Name = "Charlie" }, // add
        });

        var lastChanges = results[1];
        Assert.Equal(3, lastChanges.Count);
        Assert.Contains(lastChanges, c => c.Key == 1 && c.Reason == ChangeReason.Update);
        Assert.Contains(lastChanges, c => c.Key == 2 && c.Reason == ChangeReason.Add);
        Assert.Contains(lastChanges, c => c.Key == 3 && c.Reason == ChangeReason.Add);

        cache.Dispose();
    }

    [Fact]
    public void AddOrUpdateBatch_EmptyEnumerable()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var results = new List<IChangeSet<Person, int>>();
        using var sub = cache.Connect().Subscribe(results.Add);

        cache.AddOrUpdate(Array.Empty<Person>());

        Assert.Empty(results); // no changes = no emission

        cache.Dispose();
    }

    [Fact]
    public void Preview_ReturnsEmptyForEmptyCache()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var snapshot = cache.Preview();

        Assert.Empty(snapshot);

        cache.Dispose();
    }

    [Fact]
    public void Preview_DoesNotIncludeRemovedItems()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person { Id = 1, Name = "Alice" });
        cache.AddOrUpdate(new Person { Id = 2, Name = "Bob" });
        cache.Remove(1);

        var snapshot = cache.Preview();

        Assert.Single(snapshot);
        Assert.Contains(snapshot, p => p.Name == "Bob");
        Assert.DoesNotContain(snapshot, p => p.Name == "Alice");

        cache.Dispose();
    }

    [Fact]
    public void Watch_MultipleSubscribers()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var changes1 = new List<Change<Person, int>>();
        var changes2 = new List<Change<Person, int>>();

        using var sub1 = cache.Watch(1).Subscribe(changes1.Add);
        using var sub2 = cache.Watch(1).Subscribe(changes2.Add);

        cache.AddOrUpdate(new Person { Id = 1, Name = "Alice" });

        Assert.Single(changes1);
        Assert.Single(changes2);
        Assert.Equal(changes1[0].Current.Name, changes2[0].Current.Name);

        cache.Dispose();
    }

    [Fact]
    public void CountChanged_EmitsForBatchOperations()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var counts = new List<int>();
        using var sub = cache.CountChanged.Subscribe(counts.Add);

        cache.AddOrUpdate(new[]
        {
            new Person { Id = 1, Name = "Alice" },
            new Person { Id = 2, Name = "Bob" },
            new Person { Id = 3, Name = "Charlie" },
        });

        Assert.Single(counts);
        Assert.Equal(3, counts[0]);

        cache.Dispose();
    }

    private class Person
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
