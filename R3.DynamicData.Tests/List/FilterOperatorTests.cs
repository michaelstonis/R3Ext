// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public class FilterOperatorTests
{
    [Fact]
    public void Filter_AddsOnlyPassingItems()
    {
        var source = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source.Connect().Filter(x => x % 2 == 0).Subscribe(results.Add);

        source.Add(1); // odd ignored
        source.Add(2); // even added
        source.Add(4); // even added

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Adds); // 2
        Assert.Equal(1, results[1].Adds); // 4
        Assert.Equal(new[] { 2 }, results[0].Select(c => c.Item));
        Assert.Equal(new[] { 4 }, results[1].Select(c => c.Item));
    }

    [Fact]
    public void Filter_RemoveRemovesPassingItemsOnly()
    {
        var source = new SourceList<int>();
        using var sub = source.Connect().Filter(x => x > 5).Subscribe(_ => { });
        source.AddRange(new[] { 3, 6, 9 });
        // Current filtered list: 6,9
        var captured = new List<IChangeSet<int>>();
        sub.Dispose();
        using var sub2 = source.Connect().Filter(x => x > 5).Subscribe(captured.Add);
        captured.Clear();
        source.RemoveAt(1); // remove 6 (passing)
        source.RemoveAt(0); // remove 3 (not passing) -> should not emit

        Assert.Single(captured);
        Assert.Equal(1, captured[0].Removes);
        Assert.Equal(6, captured[0].First().Item);
    }

    [Fact]
    public void Filter_ReplaceChangesMembership()
    {
        var source = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source.Connect().Filter(x => x >= 10).Subscribe(results.Add);
        source.Add(5); // ignored
        source.Add(12); // added
        results.Clear();

        // Replace 12 with 8 (removal)
        source.Replace(12, 8);
        // Replace 5 with 11 (addition)
        source.Replace(5, 11);

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Removes);
        Assert.Equal(12, results[0].First().Item);
        Assert.Equal(1, results[1].Adds);
        Assert.Equal(11, results[1].First().Item);
    }

    [Fact]
    public void Filter_ClearRemovesAll()
    {
        var source = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source.Connect().Filter(x => x < 5).Subscribe(results.Add);
        source.AddRange(new[] { 1, 2, 10, 3 }); // adds 1,2,3
        results.Clear();
        source.Clear();

        Assert.Single(results);
        Assert.Equal(3, results[0].Removes);
        var removed = results[0].Select(c => c.Item).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, removed);
    }
}
