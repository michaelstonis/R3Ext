using System.Collections.Generic;
using System.Linq;
using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public sealed class LogicalOperatorsTests
{
    private static HashSet<T> CollectItems<T>(List<IChangeSet<T>> results)
        where T : notnull
    {
        var allItems = new HashSet<T>();
        foreach (var cs in results)
        {
            foreach (var change in cs)
            {
                if (change.Reason == ListChangeReason.Add)
                {
                    allItems.Add(change.Item);
                }
                else if (change.Reason == ListChangeReason.AddRange)
                {
                    foreach (var item in change.Range)
                    {
                        allItems.Add(item);
                    }
                }
                else if (change.Reason == ListChangeReason.Clear)
                {
                    allItems.Clear();
                }
            }
        }

        return allItems;
    }

    [Fact]
    public void And_ReturnsIntersectionOfTwoSources()
    {
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source1.Connect().And(source2.Connect()).Subscribe(results.Add);

        source1.AddRange(new[] { 1, 2, 3 });
        source2.AddRange(new[] { 2, 3, 4 });

        var allItems = CollectItems(results);

        Assert.Equal(2, allItems.Count);
        Assert.Contains(2, allItems);
        Assert.Contains(3, allItems);
    }

    [Fact]
    public void And_AddsItemWhenItAppearsInBothSources()
    {
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source1.Connect().And(source2.Connect()).Subscribe(results.Add);

        source1.Add(1);
        results.Clear();

        source2.Add(1);

        var allItems = CollectItems(results);
        Assert.Single(allItems);
        Assert.Contains(1, allItems);
    }

    [Fact]
    public void And_RemovesItemWhenRemovedFromEitherSource()
    {
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source1.Connect().And(source2.Connect()).Subscribe(results.Add);

        source1.AddRange(new[] { 1, 2 });
        source2.AddRange(new[] { 1, 2 });
        results.Clear();

        source1.Remove(1);

        // Combiner does full recomputation, so we get all items as Adds again
        var allItems = CollectItems(results);

        // After removing 1 from source1, intersection should only contain 2
        Assert.Single(allItems);
        Assert.Contains(2, allItems);
        Assert.DoesNotContain(1, allItems);
    }

    [Fact]
    public void Or_ReturnsUnionOfTwoSources()
    {
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source1.Connect().Or(source2.Connect()).Subscribe(results.Add);

        source1.AddRange(new[] { 1, 2, 3 });
        source2.AddRange(new[] { 3, 4, 5 });

        var allItems = CollectItems(results);

        Assert.Equal(5, allItems.Count);
        Assert.Contains(1, allItems);
        Assert.Contains(2, allItems);
        Assert.Contains(3, allItems);
        Assert.Contains(4, allItems);
        Assert.Contains(5, allItems);
    }

    [Fact]
    public void Or_ItemRemovedOnlyWhenAbsentFromAllSources()
    {
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source1.Connect().Or(source2.Connect()).Subscribe(results.Add);

        source1.Add(1);
        source2.Add(1);
        results.Clear();

        source1.Remove(1);

        // Combiner does full recomputation - 1 is still in source2, so it's in the result
        var allItems = CollectItems(results);

        Assert.Contains(1, allItems);

        results.Clear();
        source2.Remove(1);

        // Now 1 is not in any source
        allItems.Clear();
        foreach (var cs in results)
        {
            foreach (var change in cs)
            {
                if (change.Reason == ListChangeReason.Add)
                {
                    allItems.Add(change.Item);
                }
            }
        }

        Assert.Empty(allItems); // Result should be empty
    }

    [Fact]
    public void Except_ReturnsItemsInSourceButNotInOthers()
    {
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source1.Connect().Except(source2.Connect()).Subscribe(results.Add);

        source1.AddRange(new[] { 1, 2, 3, 4 });
        source2.AddRange(new[] { 3, 4, 5 });

        // Look only at final changeset (Combiner does full recomputation)
        var allItems = CollectItems(results);

        Assert.Equal(2, allItems.Count);
        Assert.Contains(1, allItems);
        Assert.Contains(2, allItems);
    }

    [Fact]
    public void Except_RemovesItemWhenItAppearsInOtherSource()
    {
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source1.Connect().Except(source2.Connect()).Subscribe(results.Add);

        source1.AddRange(new[] { 1, 2, 3 });
        results.Clear();

        source2.Add(2);

        // Combiner does full recomputation - result should now be 1, 3 (excluding 2)
        var allItems = CollectItems(results);

        Assert.Equal(2, allItems.Count);
        Assert.Contains(1, allItems);
        Assert.Contains(3, allItems);
        Assert.DoesNotContain(2, allItems);
    }

    [Fact]
    public void Xor_ReturnsItemsInOnlyOneSource()
    {
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source1.Connect().Xor(source2.Connect()).Subscribe(results.Add);

        source1.AddRange(new[] { 1, 2, 3 });
        source2.AddRange(new[] { 3, 4, 5 });

        // Look only at final changeset (Combiner does full recomputation)
        var allItems = CollectItems(results);

        Assert.Equal(4, allItems.Count);
        Assert.Contains(1, allItems);
        Assert.Contains(2, allItems);
        Assert.Contains(4, allItems);
        Assert.Contains(5, allItems);
        Assert.DoesNotContain(3, allItems);
    }

    [Fact]
    public void Xor_RemovesItemWhenItAppearsInBothSources()
    {
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source1.Connect().Xor(source2.Connect()).Subscribe(results.Add);

        source1.Add(1);
        results.Clear();

        source2.Add(1);

        // Combiner does full recomputation - 1 is now in both sources, so result should be empty
        var allItems = CollectItems(results);

        Assert.Empty(allItems);
    }

    [Fact]
    public void And_WorksWithThreeSources()
    {
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        var source3 = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source1.Connect().And(source2.Connect(), source3.Connect()).Subscribe(results.Add);

        source1.AddRange(new[] { 1, 2, 7 });
        source2.AddRange(new[] { 2, 3, 7 });
        source3.AddRange(new[] { 3, 4, 7 });

        var allItems = CollectItems(results);

        Assert.Single(allItems);
        Assert.Contains(7, allItems);
    }

    [Fact]
    public void Or_WorksWithThreeSources()
    {
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        var source3 = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source1.Connect().Or(source2.Connect(), source3.Connect()).Subscribe(results.Add);

        source1.AddRange(new[] { 1, 2 });
        source2.AddRange(new[] { 3, 4 });
        source3.AddRange(new[] { 5, 6 });

        var allItems = CollectItems(results);

        Assert.Equal(6, allItems.Count);
        for (int i = 1; i <= 6; i++)
        {
            Assert.Contains(i, allItems);
        }
    }

    [Fact]
    public void Except_WorksWithMultipleSourcesToExclude()
    {
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        var source3 = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source1.Connect().Except(source2.Connect(), source3.Connect()).Subscribe(results.Add);

        source1.AddRange(new[] { 1, 2, 3, 4, 5 });
        source2.AddRange(new[] { 2, 3 });
        source3.AddRange(new[] { 4 });

        // Look only at final changeset (Combiner does full recomputation)
        var allItems = CollectItems(results);

        Assert.Equal(2, allItems.Count);
        Assert.Contains(1, allItems);
        Assert.Contains(5, allItems);
    }

    [Fact]
    public void And_EmptyWhenNoCommonItems()
    {
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source1.Connect().And(source2.Connect()).Subscribe(results.Add);

        source1.AddRange(new[] { 1, 2, 3 });
        source2.AddRange(new[] { 4, 5, 6 });

        var allItems = CollectItems(results);

        Assert.Empty(allItems);
    }
}
