// Port of DynamicData to R3.

using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public class TransformManyOperatorTests
{
    private sealed class Person
    {
        public Person(string name, params int[] children)
        {
            Name = name;
            Children = children.ToList();
        }

        public string Name { get; }

        public List<int> Children { get; }
    }

    [Fact]
    public void TransformMany_AddsFlattenedChildren()
    {
        var source = new SourceList<Person>();
        var results = new List<IChangeSet<int>>();
        using var sub = source.Connect().TransformMany(p => p.Children).Subscribe(results.Add);

        source.Add(new Person("A", 1, 2));
        source.Add(new Person("B", 3));

        Assert.Equal(2, results.Count);
        Assert.Equal(2, results[0].Adds); // children 1,2
        Assert.Equal(1, results[1].Adds); // child 3
        var all = results.SelectMany(r => r.Select(c => c.Item)).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, all);
    }

    [Fact]
    public void TransformMany_RemovesChildrenOnParentRemoval()
    {
        var source = new SourceList<Person>();
        var results = new List<IChangeSet<int>>();
        using var sub = source.Connect().TransformMany(p => p.Children).Subscribe(results.Add);

        source.Add(new Person("A", 1, 2));
        source.Add(new Person("B", 3, 4));
        results.Clear();

        source.RemoveAt(0); // remove A -> removes 1,2

        Assert.Single(results);
        Assert.Equal(2, results[0].Removes);
        var removed = results[0].Select(c => c.Item).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 1, 2 }, removed);
    }

    [Fact]
    public void TransformMany_ReplaceDiffsChildren()
    {
        var source = new SourceList<Person>();
        var results = new List<IChangeSet<int>>();
        using var sub = source.Connect().TransformMany(p => p.Children).Subscribe(results.Add);

        source.Add(new Person("A", 1, 2));
        results.Clear();

        // Replace with children 2,3 (1 removed, 3 added)
        source.Replace(source.Items[0], new Person("A", 2, 3));

        Assert.Single(results);
        Assert.Equal(1, results[0].Removes);
        Assert.Equal(1, results[0].Adds);
        var items = results[0].Select(c => c.Item).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 1, 3 }, items);
    }

    [Fact]
    public void TransformMany_ClearRemovesAllChildren()
    {
        var source = new SourceList<Person>();
        var results = new List<IChangeSet<int>>();
        using var sub = source.Connect().TransformMany(p => p.Children).Subscribe(results.Add);

        source.Add(new Person("A", 1));
        source.Add(new Person("B", 2, 3));
        results.Clear();

        source.Clear();

        Assert.Single(results);
        Assert.Equal(3, results[0].Removes);
        var removed = results[0].Select(c => c.Item).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, removed);
    }
}
