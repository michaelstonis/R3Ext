
// Port of DynamicData to R3.

using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;
using R3.DynamicData.Operators;
using Xunit;

namespace R3.DynamicData.Tests;

public class TransformOperatorTests
{
    private class Person
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public int Age { get; set; }
    }

    private class PersonDto
    {
        public int Id { get; set; }

        public string? DisplayName { get; set; }

        public bool IsAdult { get; set; }
    }

    [Fact]
    public void Transform_TransformsInitialItems()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person { Id = 1, Name = "Alice", Age = 30 });

        var changesList = new List<IChangeSet<PersonDto, int>>();

        cache.Connect()
            .Transform(p => new PersonDto
            {
                Id = p.Id,
                DisplayName = p.Name?.ToUpper(),
                IsAdult = p.Age >= 18,
            })
            .Subscribe(changes => changesList.Add(changes));

        Assert.Equal(1, changesList.Count);
        var change = Assert.Single(changesList[0]);
        Assert.Equal(ChangeReason.Add, change.Reason);
        Assert.Equal(1, change.Key);
        Assert.Equal("ALICE", change.Current.DisplayName);
        Assert.True(change.Current.IsAdult);
    }

    [Fact]
    public void Transform_TransformsNewItems()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var changesList = new List<IChangeSet<PersonDto, int>>();

        cache.Connect()
            .Transform(p => new PersonDto
            {
                Id = p.Id,
                DisplayName = $"[{p.Name}]",
                IsAdult = p.Age >= 18,
            })
            .Subscribe(changes => changesList.Add(changes));

        changesList.Clear();

        cache.AddOrUpdate(new Person { Id = 1, Name = "Bob", Age = 25 });

        Assert.Equal(1, changesList.Count);
        var change = Assert.Single(changesList[0]);
        Assert.Equal(ChangeReason.Add, change.Reason);
        Assert.Equal(1, change.Key);
        Assert.Equal("[Bob]", change.Current.DisplayName);
        Assert.True(change.Current.IsAdult);
    }

    [Fact]
    public void Transform_TransformsUpdatedItems()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person { Id = 1, Name = "Charlie", Age = 15 });

        var changesList = new List<IChangeSet<PersonDto, int>>();

        cache.Connect()
            .Transform(p => new PersonDto
            {
                Id = p.Id,
                DisplayName = p.Name,
                IsAdult = p.Age >= 18,
            })
            .Subscribe(changes => changesList.Add(changes));

        changesList.Clear();

        cache.AddOrUpdate(new Person { Id = 1, Name = "Charlie", Age = 20 });

        Assert.Equal(1, changesList.Count);
        var change = Assert.Single(changesList[0]);
        Assert.Equal(ChangeReason.Update, change.Reason);
        Assert.Equal(1, change.Key);
        Assert.Equal("Charlie", change.Current.DisplayName);
        Assert.True(change.Current.IsAdult);
        Assert.True(change.Previous.HasValue);
        Assert.False(change.Previous.Value.IsAdult);
    }

    [Fact]
    public void Transform_TransformsRemovedItems()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person { Id = 1, Name = "Diana", Age = 28 });

        var changesList = new List<IChangeSet<PersonDto, int>>();

        cache.Connect()
            .Transform(p => new PersonDto
            {
                Id = p.Id,
                DisplayName = p.Name,
                IsAdult = p.Age >= 18,
            })
            .Subscribe(changes => changesList.Add(changes));

        changesList.Clear();

        cache.Remove(1);

        Assert.Equal(1, changesList.Count);
        var change = Assert.Single(changesList[0]);
        Assert.Equal(ChangeReason.Remove, change.Reason);
        Assert.Equal(1, change.Key);
        Assert.Equal("Diana", change.Current.DisplayName);
        Assert.True(change.Current.IsAdult);
    }

    [Fact]
    public void Transform_WithKey_IncludesKeyInTransformation()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person { Id = 42, Name = "Frank", Age = 30 });

        var changesList = new List<IChangeSet<PersonDto, int>>();

        cache.Connect()
            .Transform((p, key) => new PersonDto
            {
                Id = key,
                DisplayName = $"{p.Name} (ID: {key})",
                IsAdult = p.Age >= 18,
            })
            .Subscribe(changes => changesList.Add(changes));

        Assert.Equal(1, changesList.Count);
        var change = Assert.Single(changesList[0]);
        Assert.Equal(ChangeReason.Add, change.Reason);
        Assert.Equal(42, change.Key);
        Assert.Equal("Frank (ID: 42)", change.Current.DisplayName);
        Assert.True(change.Current.IsAdult);
    }

    [Fact]
    public void Transform_ChainedWithFilter()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        cache.AddOrUpdate(new Person { Id = 1, Name = "Alice", Age = 30 });
        cache.AddOrUpdate(new Person { Id = 2, Name = "Bob", Age = 15 });

        var changesList = new List<IChangeSet<PersonDto, int>>();

        cache.Connect()
            .Filter(p => p.Age >= 18)
            .Transform(p => new PersonDto
            {
                Id = p.Id,
                DisplayName = p.Name,
                IsAdult = true,
            })
            .Subscribe(changes => changesList.Add(changes));

        Assert.Equal(1, changesList.Count);
        var change = Assert.Single(changesList[0]);
        Assert.Equal(ChangeReason.Add, change.Reason);
        Assert.Equal(1, change.Key);
        Assert.Equal("Alice", change.Current.DisplayName);
    }

    [Fact]
    public void Transform_HandlesMultipleChanges()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var changesList = new List<IChangeSet<PersonDto, int>>();

        cache.Connect()
            .Transform(p => new PersonDto
            {
                Id = p.Id,
                DisplayName = p.Name,
                IsAdult = p.Age >= 18,
            })
            .Subscribe(changes => changesList.Add(changes));

        changesList.Clear();

        cache.Edit(updater =>
        {
            updater.AddOrUpdate(new Person { Id = 1, Name = "Alice", Age = 30 });
            updater.AddOrUpdate(new Person { Id = 2, Name = "Bob", Age = 25 });
            updater.AddOrUpdate(new Person { Id = 3, Name = "Charlie", Age = 20 });
        });

        Assert.Equal(1, changesList.Count);
        Assert.Equal(3, changesList[0].Count);

        var alice = changesList[0].First(c => c.Key == 1);
        Assert.Equal("Alice", alice.Current.DisplayName);

        var bob = changesList[0].First(c => c.Key == 2);
        Assert.Equal("Bob", bob.Current.DisplayName);

        var charlie = changesList[0].First(c => c.Key == 3);
        Assert.Equal("Charlie", charlie.Current.DisplayName);
    }
}
