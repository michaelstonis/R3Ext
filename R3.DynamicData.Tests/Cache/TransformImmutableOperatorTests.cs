// Port of DynamicData tests to R3.

using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;
using R3.DynamicData.Operators;
using Xunit;

namespace R3.DynamicData.Tests.Cache;

public sealed class TransformImmutableOperatorTests
{
    [Fact]
    public void TransformImmutable_TransformsInitialItems()
    {
        var cache = new SourceCache<Item, int>(p => p.Id);
        cache.AddOrUpdate(new Item { Id = 1, Name = "Item #1" });

        var changesList = new List<IChangeSet<string, int>>();

        cache.Connect()
            .TransformImmutable(item => item.Name)
            .Subscribe(changes => changesList.Add(changes));

        Assert.Single(changesList);
        var change = Assert.Single(changesList[0]);
        Assert.Equal(ChangeReason.Add, change.Reason);
        Assert.Equal(1, change.Key);
        Assert.Equal("Item #1", change.Current);
    }

    [Fact]
    public void TransformImmutable_TransformsNewItems()
    {
        var cache = new SourceCache<Item, int>(p => p.Id);
        var changesList = new List<IChangeSet<string, int>>();

        cache.Connect()
            .TransformImmutable(item => item.Name.ToUpper())
            .Subscribe(changes => changesList.Add(changes));

        changesList.Clear();

        cache.AddOrUpdate(new Item { Id = 1, Name = "Bob" });

        Assert.Single(changesList);
        var change = Assert.Single(changesList[0]);
        Assert.Equal(ChangeReason.Add, change.Reason);
        Assert.Equal(1, change.Key);
        Assert.Equal("BOB", change.Current);
    }

    [Fact]
    public void TransformImmutable_TransformsUpdatedItems()
    {
        var cache = new SourceCache<Item, int>(p => p.Id);
        cache.AddOrUpdate(new Item { Id = 1, Name = "Charlie" });

        var changesList = new List<IChangeSet<string, int>>();

        cache.Connect()
            .TransformImmutable(item => $"[{item.Name}]")
            .Subscribe(changes => changesList.Add(changes));

        changesList.Clear();

        cache.AddOrUpdate(new Item { Id = 1, Name = "David" });

        Assert.Single(changesList);
        var change = Assert.Single(changesList[0]);
        Assert.Equal(ChangeReason.Update, change.Reason);
        Assert.Equal(1, change.Key);
        Assert.Equal("[David]", change.Current);
        Assert.True(change.Previous.HasValue);
        Assert.Equal("[Charlie]", change.Previous.Value);
    }

    [Fact]
    public void TransformImmutable_TransformsRemovedItems()
    {
        var cache = new SourceCache<Item, int>(p => p.Id);
        cache.AddOrUpdate(new Item { Id = 1, Name = "Eve" });

        var changesList = new List<IChangeSet<string, int>>();

        cache.Connect()
            .TransformImmutable(item => item.Name.ToLower())
            .Subscribe(changes => changesList.Add(changes));

        changesList.Clear();

        cache.Remove(1);

        Assert.Single(changesList);
        var change = Assert.Single(changesList[0]);
        Assert.Equal(ChangeReason.Remove, change.Reason);
        Assert.Equal(1, change.Key);
        Assert.Equal("eve", change.Current);
    }

    [Fact]
    public void TransformImmutable_TransformsBatchOfOperations()
    {
        var cache = new SourceCache<Item, int>(p => p.Id);
        var changesList = new List<IChangeSet<string, int>>();

        cache.Connect()
            .TransformImmutable(item => item.Name)
            .Subscribe(changes => changesList.Add(changes));

        changesList.Clear();

        cache.Edit(updater =>
        {
            updater.AddOrUpdate(new Item { Id = 1, Name = "Item #1" });
            updater.AddOrUpdate(new Item { Id = 2, Name = "Item #2" });
            updater.AddOrUpdate(new Item { Id = 3, Name = "Item #3" });
        });

        Assert.Single(changesList);
        Assert.Equal(3, changesList[0].Count);
        Assert.All(changesList[0], change => Assert.Equal(ChangeReason.Add, change.Reason));
    }

    [Fact]
    public void TransformImmutable_SourceIsNull_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TransformOperator.TransformImmutable(
                source: (null as Observable<IChangeSet<Item, int>>)!,
                transformFactory: item => item.Name));
    }

    [Fact]
    public void TransformImmutable_TransformFactoryIsNull_ThrowsException()
    {
        var cache = new SourceCache<Item, int>(p => p.Id);

        Assert.Throws<ArgumentNullException>(() =>
            cache.Connect().TransformImmutable<Item, int, string>(transformFactory: null!));
    }

    [Fact]
    public void TransformImmutable_ValueTypeDestination_WorksCorrectly()
    {
        var cache = new SourceCache<string, string>(s => s);
        var changesList = new List<IChangeSet<int, string>>();

        cache.Connect()
            .TransformImmutable(value => value.Length)
            .Subscribe(changes => changesList.Add(changes));

        cache.AddOrUpdate("Item #1");

        Assert.Single(changesList);
        var change = Assert.Single(changesList[0]);
        Assert.Equal(ChangeReason.Add, change.Reason);
        Assert.Equal("Item #1", change.Key);
        Assert.Equal(7, change.Current);
    }

    private class Item
    {
        public required int Id { get; init; }

        public required string Name { get; init; }
    }
}
