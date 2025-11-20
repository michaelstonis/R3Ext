
// Port of DynamicData to R3.

using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public class SortOperatorTests
{
    [Fact]
    public void Sort_InitialItems_AreSorted()
    {
        var list = new SourceList<int>();
        list.AddRange(new[] { 3, 1, 4, 1, 5, 9, 2, 6 });

        var results = new List<IChangeSet<int>>();
        list.Connect()
            .Sort(Comparer<int>.Default)
            .Subscribe(results.Add);

        Assert.Single(results);
        var changes = results[0];
        var sortedItems = changes.Select(c => c.Item).ToList();
        Assert.Equal(new[] { 1, 1, 2, 3, 4, 5, 6, 9 }, sortedItems);
    }

    [Fact]
    public void Sort_AddItem_InsertsInSortedPosition()
    {
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 3, 5 });

        var results = new List<IChangeSet<int>>();
        list.Connect()
            .Sort(Comparer<int>.Default)
            .Subscribe(results.Add);

        list.Add(4);

        Assert.Equal(2, results.Count);
        var changes = results[1];
        Assert.Single(changes);
        var change = changes.First();
        Assert.Equal(4, change.Item);
        Assert.Equal(2, change.CurrentIndex);
    }

    [Fact]
    public void Sort_RemoveItem_MaintainsSort()
    {
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 2, 3, 4, 5 });

        var results = new List<IChangeSet<int>>();
        list.Connect()
            .Sort(Comparer<int>.Default)
            .Subscribe(results.Add);

        list.Remove(3);

        Assert.Equal(2, results.Count);
        var changes = results[1];
        Assert.Single(changes);
        var change = changes.First();
        Assert.Equal(3, change.Item);
        Assert.Equal(2, change.CurrentIndex);
        Assert.Equal(ListChangeReason.Remove, change.Reason);
    }

    [Fact]
    public void Sort_ReplaceItem_ResortsCorrectly()
    {
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 2, 3, 4, 5 });

        var results = new List<IChangeSet<int>>();
        list.Connect()
            .Sort(Comparer<int>.Default)
            .Subscribe(results.Add);

        list.ReplaceAt(2, 10);

        Assert.Equal(2, results.Count);
        var changes = results[1];
        Assert.Equal(2, changes.Count);
        Assert.Contains(changes, c => c.Item == 3 && c.Reason == ListChangeReason.Remove);
        Assert.Contains(changes, c => c.Item == 10 && c.Reason == ListChangeReason.Add);
    }

    [Fact]
    public void Sort_Clear_RemovesAllItems()
    {
        var list = new SourceList<int>();
        list.AddRange(new[] { 3, 1, 4 });

        var results = new List<IChangeSet<int>>();
        list.Connect()
            .Sort(Comparer<int>.Default)
            .Subscribe(results.Add);

        list.Clear();

        Assert.Equal(2, results.Count);
        var changes = results[1];
        Assert.Single(changes);
        Assert.Equal(ListChangeReason.Clear, changes.First().Reason);
    }

    [Fact]
    public void Sort_WithKeySelector_SortsCorrectly()
    {
        var list = new SourceList<Person>();
        list.AddRange(new[]
        {
            new Person { Name = "Charlie", Age = 30 },
            new Person { Name = "Alice", Age = 25 },
            new Person { Name = "Bob", Age = 35 },
        });

        var results = new List<IChangeSet<Person>>();
        list.Connect()
            .Sort(p => p.Name)
            .Subscribe(results.Add);

        Assert.Single(results);
        var changes = results[0];
        var names = changes.Select(c => c.Item.Name).ToList();
        Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, names);
    }

    [Fact]
    public void Sort_Descending_SortsInReverseOrder()
    {
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 5, 3, 2, 4 });

        var descendingComparer = Comparer<int>.Create((x, y) => y.CompareTo(x));

        var results = new List<IChangeSet<int>>();
        list.Connect()
            .Sort(descendingComparer)
            .Subscribe(results.Add);

        Assert.Single(results);
        var changes = results[0];
        var sortedItems = changes.Select(c => c.Item).ToList();
        Assert.Equal(new[] { 5, 4, 3, 2, 1 }, sortedItems);
    }

    [Fact]
    public void Sort_WithBinarySearch_InsertsCorrectly()
    {
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 3, 5, 7, 9 });

        var results = new List<IChangeSet<int>>();
        list.Connect()
            .Sort(Comparer<int>.Default, SortOptions.UseBinarySearch)
            .Subscribe(results.Add);

        list.Add(4);

        Assert.Equal(2, results.Count);
        var changes = results[1];
        var change = changes.First();
        Assert.Equal(4, change.Item);
        Assert.Equal(2, change.CurrentIndex);
    }

    [Fact]
    public void Sort_AddRange_InsertsAllInSortedOrder()
    {
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 5, 9 });

        var results = new List<IChangeSet<int>>();
        list.Connect()
            .Sort(Comparer<int>.Default)
            .Subscribe(results.Add);

        list.AddRange(new[] { 3, 7, 2 });

        Assert.Equal(2, results.Count);
        var changes = results[1];
        Assert.Equal(3, changes.Count);
        Assert.All(changes, c => Assert.Equal(ListChangeReason.Add, c.Reason));
    }

    [Fact]
    public void Sort_RemoveRange_RemovesAllItems()
    {
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        var results = new List<IChangeSet<int>>();
        list.Connect()
            .Sort(Comparer<int>.Default)
            .Subscribe(results.Add);

        list.RemoveRange(2, 3);

        Assert.Equal(2, results.Count);
        var changes = results[1];
        Assert.Equal(3, changes.Count);
        Assert.Equal(3, changes.Removes);
    }

    [Fact]
    public void Sort_EmptyList_HandlesAddCorrectly()
    {
        var list = new SourceList<int>();

        var results = new List<IChangeSet<int>>();
        list.Connect()
            .Sort(Comparer<int>.Default)
            .Subscribe(results.Add);

        list.Add(42);

        Assert.Single(results);
        var changes = results[0];
        Assert.Single(changes);
        var change = changes.First();
        Assert.Equal(42, change.Item);
        Assert.Equal(0, change.CurrentIndex);
    }

    [Fact]
    public void Sort_ComplexObject_SortsByProperty()
    {
        var list = new SourceList<Person>();
        list.AddRange(new[]
        {
            new Person { Name = "Alice", Age = 30 },
            new Person { Name = "Bob", Age = 25 },
            new Person { Name = "Charlie", Age = 35 },
        });

        var results = new List<IChangeSet<Person>>();
        list.Connect()
            .Sort(p => p.Age)
            .Subscribe(results.Add);

        Assert.Single(results);
        var changes = results[0];
        var ages = changes.Select(c => c.Item.Age).ToList();
        Assert.Equal(new[] { 25, 30, 35 }, ages);
    }

    private sealed class Person
    {
        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }
    }
}
