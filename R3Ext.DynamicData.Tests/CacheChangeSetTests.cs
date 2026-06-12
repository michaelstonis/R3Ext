using R3Ext.DynamicData.Cache;
using R3Ext.DynamicData.Kernel;

namespace R3Ext.DynamicData.Tests;

public class CacheChangeSetTests
{
    [Fact]
    public void Construct_Empty_CheckCounts()
    {
        var changeSet = new ChangeSet<string, int>();

        Assert.Equal(0, changeSet.Count);
        Assert.Equal(0, changeSet.Adds);
        Assert.Equal(0, changeSet.Moves);
        Assert.Equal(0, changeSet.Refreshes);
        Assert.Equal(0, changeSet.Removes);
        Assert.Equal(0, changeSet.Updates);
    }

    [Fact]
    public void Construct_Capacity_CheckCounts()
    {
        var changeSet = new ChangeSet<string, int>(7);

        Assert.Equal(0, changeSet.Count);
        Assert.Equal(0, changeSet.Adds);
        Assert.Equal(0, changeSet.Moves);
        Assert.Equal(0, changeSet.Refreshes);
        Assert.Equal(0, changeSet.Removes);
        Assert.Equal(0, changeSet.Updates);
    }

    [Fact]
    public void Construct_Enumerable_CheckCounts()
    {
        Change<string, int>[] changes = [
            ..Enumerable.Range(0, 1).Select(x => new Change<string, int>(ChangeReason.Add, x, x.ToString())),
            ..Enumerable.Range(0, 2).Select(x => new Change<string, int>(ChangeReason.Moved, x, x.ToString())),
            ..Enumerable.Range(0, 3).Select(x => new Change<string, int>(ChangeReason.Refresh, x, x.ToString())),
            ..Enumerable.Range(0, 4).Select(x => new Change<string, int>(ChangeReason.Remove, x, x.ToString())),
            ..Enumerable.Range(0, 5).Select(x => new Change<string, int>(ChangeReason.Update, x, x.ToString()))
        ];
        var changeSet = new ChangeSet<string, int>(changes);

        Assert.Equal(changes.Length, changeSet.Count);
        Assert.Equal(changes.Count(x => x.Reason == ChangeReason.Add), changeSet.Adds);
        Assert.Equal(changes.Count(x => x.Reason == ChangeReason.Moved), changeSet.Moves);
        Assert.Equal(changes.Count(x => x.Reason == ChangeReason.Refresh), changeSet.Refreshes);
        Assert.Equal(changes.Count(x => x.Reason == ChangeReason.Remove), changeSet.Removes);
        Assert.Equal(changes.Count(x => x.Reason == ChangeReason.Update), changeSet.Updates);
    }
}
