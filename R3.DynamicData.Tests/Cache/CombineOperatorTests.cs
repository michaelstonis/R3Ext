using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.Operators;
using Xunit;

namespace R3.DynamicData.Tests.Cache;

public class CombineOperatorTests
{
    private class Person
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public int Age { get; set; }
    }

    [Fact]
    public void Combine_MergesMultipleSources()
    {
        var source1 = new SourceCache<Person, int>(p => p.Id);
        var source2 = new SourceCache<Person, int>(p => p.Id);

        var changesList = new List<IChangeSet<Person, int>>();

        CombineOperator.Combine(source1.Connect(), source2.Connect())
            .Subscribe(changes => changesList.Add(changes));

        source1.AddOrUpdate(new Person { Id = 1, Name = "Alice", Age = 30 });
        source2.AddOrUpdate(new Person { Id = 2, Name = "Bob", Age = 25 });

        Assert.Equal(2, changesList.Count);
        Assert.Single(changesList[0]);
        Assert.Single(changesList[1]);
    }

    [Fact]
    public void Combine_LastSourceWinsForSameKey()
    {
        var source1 = new SourceCache<Person, int>(p => p.Id);
        var source2 = new SourceCache<Person, int>(p => p.Id);

        var changesList = new List<IChangeSet<Person, int>>();

        CombineOperator.Combine(source1.Connect(), source2.Connect())
            .Subscribe(changes => changesList.Add(changes));

        source1.AddOrUpdate(new Person { Id = 1, Name = "Alice", Age = 30 });
        source2.AddOrUpdate(new Person { Id = 1, Name = "Bob", Age = 25 });

        // First add from source1, then update from source2 (higher priority)
        Assert.Equal(2, changesList.Count);

        var secondChange = changesList[1].First();
        Assert.Equal(Kernel.ChangeReason.Update, secondChange.Reason);
        Assert.Equal("Bob", secondChange.Current.Name);
    }

    [Fact]
    public void Combine_RemoveFromOwningSource()
    {
        var source1 = new SourceCache<Person, int>(p => p.Id);
        var source2 = new SourceCache<Person, int>(p => p.Id);

        var changesList = new List<IChangeSet<Person, int>>();

        CombineOperator.Combine(source1.Connect(), source2.Connect())
            .Subscribe(changes => changesList.Add(changes));

        source1.AddOrUpdate(new Person { Id = 1, Name = "Alice", Age = 30 });
        source2.AddOrUpdate(new Person { Id = 1, Name = "Bob", Age = 25 });
        source2.Remove(1); // source2 owns the key now

        Assert.Equal(3, changesList.Count);

        var removeChange = changesList[2].First();
        Assert.Equal(Kernel.ChangeReason.Remove, removeChange.Reason);
        Assert.Equal(1, removeChange.Key);
    }

    [Fact]
    public void Combine_IgnoresRemoveFromNonOwningSource()
    {
        var source1 = new SourceCache<Person, int>(p => p.Id);
        var source2 = new SourceCache<Person, int>(p => p.Id);

        var changesList = new List<IChangeSet<Person, int>>();

        CombineOperator.Combine(source1.Connect(), source2.Connect())
            .Subscribe(changes => changesList.Add(changes));

        source1.AddOrUpdate(new Person { Id = 1, Name = "Alice", Age = 30 });
        source2.AddOrUpdate(new Person { Id = 1, Name = "Bob", Age = 25 });
        source1.Remove(1); // source1 no longer owns this key

        // Should have 2 changes (add from source1, update from source2)
        // No remove because source1 doesn't own the key anymore
        Assert.Equal(2, changesList.Count);
    }
}
