using R3; // for Subscribe overloads
using R3.DynamicData.List;
using Xunit;

namespace R3Ext.Tests;

public class ReverseOperatorTests
{
    [Fact]
    public void Reverse_ProducesInitialReversedAddRange()
    {
        var source = new SourceList<int>();
        var changesets = new List<IChangeSet<int>>();
        using var sub = source.Connect().Reverse().Subscribe(cs => changesets.Add(cs));

        source.Add(1);
        source.Add(2);
        source.Add(3);

        Assert.NotEmpty(changesets);

        // Expect Clear + AddRange for reversed [3,2,1] after each mutation
        var lastChangeSet = changesets[^1];
        var changes = lastChangeSet.ToList();

        // Should have Clear and AddRange
        Assert.Contains(changes, c => c.Reason == ListChangeReason.Clear);
        Assert.Contains(changes, c => c.Reason == ListChangeReason.AddRange && c.Range.SequenceEqual(new[] { 3, 2, 1 }));

        source.Dispose();
    }

    [Fact]
    public void Reverse_EmitsClearAndAddRangeOnChange()
    {
        var source = new SourceList<int>();
        var changesets = new List<IChangeSet<int>>();
        using var sub = source.Connect().Reverse().Subscribe(cs => changesets.Add(cs));

        source.AddRange(new[] { 10, 20 }); // reversed [20,10]
        source.Add(30); // now reversed [30,20,10]

        // Expect at least one Clear and one AddRange after second mutation
        var lastChangeSet = changesets[^1];
        var changes = lastChangeSet.ToList();

        Assert.Contains(changes, c => c.Reason == ListChangeReason.Clear);
        Assert.Contains(changes, c => c.Reason == ListChangeReason.AddRange && c.Range.SequenceEqual(new[] { 30, 20, 10 }));

        source.Dispose();
    }
}
