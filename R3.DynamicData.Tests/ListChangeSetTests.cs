using R3.DynamicData.List;

namespace R3.DynamicData.Tests;

public class ListChangeSetTests
{
    [Fact]
    public void Construct_Empty_CheckCounts()
    {
        var changeSet = new ChangeSet<int>();

        Assert.Equal(0, changeSet.Count);
        Assert.Equal(0, changeSet.Adds);
        Assert.Equal(0, changeSet.Moves);
        Assert.Equal(0, changeSet.Refreshes);
    }

    [Fact]
    public void Construct_Capacity_CheckCounts()
    {
        var changeSet = new ChangeSet<int>(7);

        Assert.Equal(0, changeSet.Count);
        Assert.Equal(0, changeSet.Adds);
        Assert.Equal(0, changeSet.Moves);
        Assert.Equal(0, changeSet.Refreshes);
    }

    [Fact]
    public void Construct_Enumerable_CheckCounts()
    {
        Change<int>[] changes = [
            new(ListChangeReason.Add, 0, 0),
            new(ListChangeReason.Moved, 0, 0),
            new(ListChangeReason.Moved, 0, 0),
            new(ListChangeReason.Refresh),
            new(ListChangeReason.Refresh),
            new(ListChangeReason.Refresh),
        ];
        var changeSet = new ChangeSet<int>(changes);

        Assert.Equal(changes.Length, changeSet.Count);
        Assert.Equal(changes.Count(x => x.Reason == ListChangeReason.Add), changeSet.Adds);
        Assert.Equal(changes.Count(x => x.Reason == ListChangeReason.Moved), changeSet.Moves);
        Assert.Equal(changes.Count(x => x.Reason == ListChangeReason.Refresh), changeSet.Refreshes);
    }
}
