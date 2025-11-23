using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.List;

namespace R3.DynamicData.Tests.Cache;

public class CacheSortOperatorTests
{
    [Fact]
    public void Sort_InitialEmptyCache_ReturnsEmptyList()
    {
        var cache = new SourceCache<Person, int>(x => x.Id);
        var results = new List<IChangeSet<Person>>();

        using var sub = cache.Connect()
            .Sort(Comparer<Person>.Create((x, y) => x.Name.CompareTo(y.Name)))
            .Subscribe(changes => results.Add(changes));

        Assert.Empty(results);
    }

    [Fact]
    public void Sort_AddItems_EmitsSortedOrder()
    {
        var cache = new SourceCache<Person, int>(x => x.Id);
        var results = new List<IChangeSet<Person>>();
        var currentState = new List<Person>();

        using var sub = cache.Connect()
            .Sort(Comparer<Person>.Create((x, y) => x.Name.CompareTo(y.Name)))
            .Subscribe(changes =>
            {
                results.Add(changes);
                ApplyChanges(currentState, changes);
            });

        cache.AddOrUpdate(new Person { Id = 1, Name = "Charlie" });
        cache.AddOrUpdate(new Person { Id = 2, Name = "Alice" });
        cache.AddOrUpdate(new Person { Id = 3, Name = "Bob" });

        Assert.Equal(3, results.Count);
        Assert.Equal(3, currentState.Count);
        Assert.Equal("Alice", currentState[0].Name);
        Assert.Equal("Bob", currentState[1].Name);
        Assert.Equal("Charlie", currentState[2].Name);
    }

    [Fact]
    public void Sort_Update_MaintainsSortOrder()
    {
        var cache = new SourceCache<Person, int>(x => x.Id);
        var results = new List<IChangeSet<Person>>();
        var currentState = new List<Person>();

        using var sub = cache.Connect()
            .Sort(Comparer<Person>.Create((x, y) => x.Name.CompareTo(y.Name)))
            .Subscribe(changes =>
            {
                results.Add(changes);
                ApplyChanges(currentState, changes);
            });

        cache.AddOrUpdate(new Person { Id = 1, Name = "Alice" });
        cache.AddOrUpdate(new Person { Id = 2, Name = "Bob" });
        cache.AddOrUpdate(new Person { Id = 3, Name = "Charlie" });

        results.Clear();
        cache.AddOrUpdate(new Person { Id = 2, Name = "Zoe" }); // Update Bob -> Zoe

        Assert.Single(results);
        Assert.Equal(3, currentState.Count);
        Assert.Equal("Alice", currentState[0].Name);
        Assert.Equal("Charlie", currentState[1].Name);
        Assert.Equal("Zoe", currentState[2].Name);
    }

    [Fact]
    public void Sort_Remove_MaintainsSortOrder()
    {
        var cache = new SourceCache<Person, int>(x => x.Id);
        var results = new List<IChangeSet<Person>>();
        var currentState = new List<Person>();

        using var sub = cache.Connect()
            .Sort(Comparer<Person>.Create((x, y) => x.Name.CompareTo(y.Name)))
            .Subscribe(changes =>
            {
                results.Add(changes);
                ApplyChanges(currentState, changes);
            });

        cache.AddOrUpdate(new Person { Id = 1, Name = "Alice" });
        cache.AddOrUpdate(new Person { Id = 2, Name = "Bob" });
        cache.AddOrUpdate(new Person { Id = 3, Name = "Charlie" });

        results.Clear();
        cache.Remove(2); // Remove Bob

        Assert.Single(results);
        Assert.Equal(2, currentState.Count);
        Assert.Equal("Alice", currentState[0].Name);
        Assert.Equal("Charlie", currentState[1].Name);
    }

    [Fact]
    public void Sort_WithKeySelectorFunc_SortsByProperty()
    {
        var cache = new SourceCache<Person, int>(x => x.Id);
        var results = new List<IChangeSet<Person>>();
        var currentState = new List<Person>();

        using var sub = cache.Connect()
            .Sort<Person, int, string>(p => p.Name) // Sort by Name property
            .Subscribe(changes =>
            {
                results.Add(changes);
                ApplyChanges(currentState, changes);
            });

        cache.AddOrUpdate(new Person { Id = 3, Name = "Zebra" });
        cache.AddOrUpdate(new Person { Id = 1, Name = "Apple" });
        cache.AddOrUpdate(new Person { Id = 2, Name = "Banana" });

        Assert.Equal(3, results.Count);
        Assert.Equal("Apple", currentState[0].Name);
        Assert.Equal("Banana", currentState[1].Name);
        Assert.Equal("Zebra", currentState[2].Name);
    }

    [Fact]
    public void Sort_DynamicComparerChange_ResortsAll()
    {
        var cache = new SourceCache<Person, int>(x => x.Id);
        var results = new List<IChangeSet<Person>>();
        var currentState = new List<Person>();
        var comparerSubject = new Subject<IComparer<Person>>();

        using var sub = cache.Connect()
            .Sort(
                Comparer<Person>.Create((x, y) => x.Name.CompareTo(y.Name)),
                comparerSubject)
            .Subscribe(changes =>
            {
                results.Add(changes);
                ApplyChanges(currentState, changes);
            });

        cache.AddOrUpdate(new Person { Id = 1, Name = "Charlie", Age = 30 });
        cache.AddOrUpdate(new Person { Id = 2, Name = "Alice", Age = 25 });
        cache.AddOrUpdate(new Person { Id = 3, Name = "Bob", Age = 35 });

        // Initially sorted by Name
        Assert.Equal("Alice", currentState[0].Name);
        Assert.Equal("Bob", currentState[1].Name);
        Assert.Equal("Charlie", currentState[2].Name);

        results.Clear();

        // Change comparer to sort by Age
        comparerSubject.OnNext(Comparer<Person>.Create((x, y) => x.Age.CompareTo(y.Age)));

        Assert.Single(results); // Should emit one re-sort changeset
        Assert.Equal("Alice", currentState[0].Name); // Age 25
        Assert.Equal("Charlie", currentState[1].Name); // Age 30
        Assert.Equal("Bob", currentState[2].Name); // Age 35
    }

    [Fact]
    public void Sort_WithBinarySearch_MaintainsCorrectOrder()
    {
        var cache = new SourceCache<Person, int>(x => x.Id);
        var results = new List<IChangeSet<Person>>();
        var currentState = new List<Person>();

        using var sub = cache.Connect()
            .Sort(
                Comparer<Person>.Create((x, y) => x.Age.CompareTo(y.Age)),
                SortOptions.UseBinarySearch)
            .Subscribe(changes =>
            {
                results.Add(changes);
                ApplyChanges(currentState, changes);
            });

        // Add in random order
        cache.AddOrUpdate(new Person { Id = 1, Name = "Alice", Age = 50 });
        cache.AddOrUpdate(new Person { Id = 2, Name = "Bob", Age = 25 });
        cache.AddOrUpdate(new Person { Id = 3, Name = "Charlie", Age = 35 });
        cache.AddOrUpdate(new Person { Id = 4, Name = "David", Age = 40 });
        cache.AddOrUpdate(new Person { Id = 5, Name = "Eve", Age = 30 });

        Assert.Equal(5, currentState.Count);
        Assert.Equal(25, currentState[0].Age); // Bob
        Assert.Equal(30, currentState[1].Age); // Eve
        Assert.Equal(35, currentState[2].Age); // Charlie
        Assert.Equal(40, currentState[3].Age); // David
        Assert.Equal(50, currentState[4].Age); // Alice
    }

    [Fact]
    public void Sort_DuplicateValues_MaintainsStableSort()
    {
        var cache = new SourceCache<Person, int>(x => x.Id);
        var results = new List<IChangeSet<Person>>();
        var currentState = new List<Person>();

        using var sub = cache.Connect()
            .Sort(Comparer<Person>.Create((x, y) => x.Age.CompareTo(y.Age)))
            .Subscribe(changes =>
            {
                results.Add(changes);
                ApplyChanges(currentState, changes);
            });

        cache.AddOrUpdate(new Person { Id = 1, Name = "Alice", Age = 30 });
        cache.AddOrUpdate(new Person { Id = 2, Name = "Bob", Age = 30 });
        cache.AddOrUpdate(new Person { Id = 3, Name = "Charlie", Age = 25 });

        Assert.Equal(3, currentState.Count);
        Assert.Equal(25, currentState[0].Age);
        Assert.Equal(30, currentState[1].Age);
        Assert.Equal(30, currentState[2].Age);
    }

    [Fact]
    public void Sort_LargeDataset_PerformsEfficiently()
    {
        var cache = new SourceCache<Person, int>(x => x.Id);
        var currentState = new List<Person>();

        using var sub = cache.Connect()
            .Sort(
                Comparer<Person>.Create((x, y) => x.Age.CompareTo(y.Age)),
                SortOptions.UseBinarySearch)
            .Subscribe(changes => ApplyChanges(currentState, changes));

        var random = new Random(42);
        for (int i = 0; i < 1000; i++)
        {
            cache.AddOrUpdate(new Person { Id = i, Name = $"Person{i}", Age = random.Next(18, 80) });
        }

        Assert.Equal(1000, currentState.Count);

        // Verify sorting
        for (int i = 1; i < currentState.Count; i++)
        {
            Assert.True(currentState[i - 1].Age <= currentState[i].Age);
        }
    }

    private void ApplyChanges(List<Person> state, IChangeSet<Person> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    if (change.CurrentIndex >= 0 && change.CurrentIndex <= state.Count)
                    {
                        state.Insert(change.CurrentIndex, change.Item);
                    }
                    else
                    {
                        state.Add(change.Item);
                    }

                    break;

                case ListChangeReason.Remove:
                    if (change.CurrentIndex >= 0 && change.CurrentIndex < state.Count)
                    {
                        state.RemoveAt(change.CurrentIndex);
                    }
                    else
                    {
                        state.Remove(change.Item);
                    }

                    break;

                case ListChangeReason.Clear:
                    state.Clear();
                    break;

                case ListChangeReason.AddRange:
                    if (change.Range.Count > 0)
                    {
                        state.AddRange(change.Range);
                    }

                    break;
            }
        }
    }

    private class Person
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }
    }
}
