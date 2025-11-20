// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public class AggregateOperatorsTests
{
    [Fact]
    public void Count_TracksItems()
    {
        var list = new SourceList<int>();
        var counts = new List<int>();
        list.Connect().Count().Subscribe(counts.Add);

        list.Add(1);
        list.AddRange(new[] { 2, 3 });
        list.Remove(2);
        list.Clear();

        Assert.Equal(new[] { 1, 3, 2, 0 }, counts);
    }

    [Fact]
    public void Sum_TracksSum()
    {
        var list = new SourceList<int>();
        var sums = new List<int>();
        list.Connect().Sum().Subscribe(sums.Add);

        list.Add(1);
        list.AddRange(new[] { 2, 3 });
        list.Remove(2);
        list.Replace(3, 5);
        list.Clear();

        Assert.Equal(new[] { 1, 6, 4, 6, 0 }, sums);
    }
}
