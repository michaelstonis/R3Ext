// Port of DynamicData to R3.

using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public class DistinctValuesOperatorTests
{
    [Fact]
    public void DistinctValues_AddsFirstOccurrenceOnly()
    {
        var source = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source.Connect().DistinctValues(x => x).Subscribe(results.Add);

        source.Add(1); // add distinct
        source.Add(2); // add distinct
        source.Add(1); // duplicate

        Assert.Equal(2, results.Count); // first 1, then 2
        Assert.Equal(1, results[0].Adds);
        Assert.Equal(1, results[1].Adds);
        Assert.Equal(new[] { 1 }, results[0].Select(c => c.Item));
        Assert.Equal(new[] { 2 }, results[1].Select(c => c.Item));
    }

    [Fact]
    public void DistinctValues_RemovesWhenLastOccurrenceRemoved()
    {
        var source = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source.Connect().DistinctValues(x => x).Subscribe(results.Add);

        source.AddRange(new[] { 1, 1, 2 });
        results.Clear();

        source.RemoveAt(0); // remove one 1 (still present)
        source.RemoveAt(0); // remove last 1

        Assert.Single(results); // Only final removal of distinct 1 produces change
        Assert.Equal(1, results[0].Removes);
        Assert.Equal(1, results[0].First().Item);
    }

    [Fact]
    public void DistinctValues_ReplaceChangesDistinctSet()
    {
        var source = new SourceList<string>();
        var results = new List<IChangeSet<string>>();
        using var sub = source.Connect().DistinctValues(s => s).Subscribe(results.Add);

        source.Add("A");
        source.Add("B");
        results.Clear();

        // Replace B with C
        source.Replace("B", "C");

        Assert.Single(results); // remove + add emitted together
        Assert.Equal(1, results[0].Removes);
        Assert.Equal(1, results[0].Adds);
        var items = results[0].Select(c => c.Item).ToList();
        Assert.Contains("B", items);
        Assert.Contains("C", items);
    }

    [Fact]
    public void DistinctValues_ClearRemovesAllDistinctValues()
    {
        var source = new SourceList<int>();
        var results = new List<IChangeSet<int>>();
        using var sub = source.Connect().DistinctValues(x => x).Subscribe(results.Add);

        source.AddRange(new[] { 1, 2, 2, 3 });
        results.Clear();
        source.Clear();

        Assert.Single(results);
        Assert.Equal(3, results[0].Removes);
        var removed = results[0].Select(c => c.Item).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, removed);
    }
}
