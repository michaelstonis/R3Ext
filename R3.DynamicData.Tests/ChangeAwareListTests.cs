using R3.DynamicData.List;

namespace R3.DynamicData.Tests;

public class ChangeAwareListTests
{
    [Fact]
    public void Add_SingleItem_CapturesChange()
    {
        var list = new ChangeAwareList<int>();
        list.Add(1);

        var changes = list.CaptureChanges();

        Assert.Equal(1, changes.Count);
        Assert.Equal(1, changes.Adds);
        var change = changes.First();
        Assert.Equal(ListChangeReason.Add, change.Reason);
        Assert.Equal(1, change.Item);
        Assert.Equal(0, change.CurrentIndex);
    }

    [Fact]
    public void AddRange_MultipleItems_CapturesChange()
    {
        var list = new ChangeAwareList<int>();
        list.AddRange(new[] { 1, 2, 3 });

        var changes = list.CaptureChanges();

        Assert.Equal(1, changes.Count);
        Assert.Equal(3, changes.Adds);
        var change = changes.First();
        Assert.Equal(ListChangeReason.AddRange, change.Reason);
        Assert.Equal(3, change.Range.Count);
        Assert.Equal(new[] { 1, 2, 3 }, change.Range);
    }

    [Fact]
    public void RemoveAt_RemovesItem_CapturesChange()
    {
        var list = new ChangeAwareList<int>(new[] { 1, 2, 3 });
        list.CaptureChanges(); // Clear initial add

        list.RemoveAt(1);

        var changes = list.CaptureChanges();

        Assert.Equal(1, changes.Count);
        Assert.Equal(1, changes.Removes);
        var change = changes.First();
        Assert.Equal(ListChangeReason.Remove, change.Reason);
        Assert.Equal(2, change.Item);
        Assert.Equal(1, change.CurrentIndex);
        Assert.Equal(new[] { 1, 3 }, list.ToList());
    }

    [Fact]
    public void RemoveRange_RemovesMultipleItems_CapturesChange()
    {
        var list = new ChangeAwareList<int>(new[] { 1, 2, 3, 4, 5 });
        list.CaptureChanges(); // Clear initial add

        list.RemoveRange(1, 3);

        var changes = list.CaptureChanges();

        Assert.Equal(1, changes.Count);
        Assert.Equal(3, changes.Removes);
        var change = changes.First();
        Assert.Equal(ListChangeReason.RemoveRange, change.Reason);
        Assert.Equal(new[] { 2, 3, 4 }, change.Range);
        Assert.Equal(new[] { 1, 5 }, list.ToList());
    }

    [Fact]
    public void Move_MovesItem_CapturesChange()
    {
        var list = new ChangeAwareList<int>(new[] { 1, 2, 3, 4, 5 });
        list.CaptureChanges(); // Clear initial add

        list.Move(1, 3); // Move 2 from index 1 to index 3

        var changes = list.CaptureChanges();

        Assert.Equal(1, changes.Count);
        Assert.Equal(1, changes.Moves);
        var change = changes.First();
        Assert.Equal(ListChangeReason.Moved, change.Reason);
        Assert.Equal(2, change.Item);
        Assert.Equal(3, change.CurrentIndex);
        Assert.Equal(1, change.PreviousIndex);
        Assert.Equal(new[] { 1, 3, 4, 2, 5 }, list.ToList());
    }

    [Fact]
    public void Clear_ClearsAll_CapturesChange()
    {
        var list = new ChangeAwareList<int>(new[] { 1, 2, 3 });
        list.CaptureChanges(); // Clear initial add

        list.Clear();

        var changes = list.CaptureChanges();

        Assert.Equal(1, changes.Count);
        var change = changes.First();
        Assert.Equal(ListChangeReason.Clear, change.Reason);
        Assert.Equal(new[] { 1, 2, 3 }, change.Range);
        Assert.Empty(list);
    }

    [Fact]
    public void Indexer_Set_CapturesReplaceChange()
    {
        var list = new ChangeAwareList<int>(new[] { 1, 2, 3 });
        list.CaptureChanges(); // Clear initial add

        list[1] = 20;

        var changes = list.CaptureChanges();

        Assert.Equal(1, changes.Count);
        var change = changes.First();
        Assert.Equal(ListChangeReason.Replace, change.Reason);
        Assert.Equal(20, change.Item);
        Assert.Equal(2, change.PreviousItem);
        Assert.Equal(1, change.CurrentIndex);
        Assert.Equal(new[] { 1, 20, 3 }, list.ToList());
    }

    [Fact]
    public void InsertRange_InsertsAtIndex_CapturesChange()
    {
        var list = new ChangeAwareList<int>(new[] { 1, 5 });
        list.CaptureChanges(); // Clear initial add

        list.InsertRange(new[] { 2, 3, 4 }, 1);

        var changes = list.CaptureChanges();

        Assert.Equal(1, changes.Count);
        Assert.Equal(3, changes.Adds);
        var change = changes.First();
        Assert.Equal(ListChangeReason.AddRange, change.Reason);
        Assert.Equal(new[] { 2, 3, 4 }, change.Range);
        Assert.Equal(1, change.CurrentIndex);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, list.ToList());
    }

    [Fact]
    public void CaptureChanges_WhenNoChanges_ReturnsEmpty()
    {
        var list = new ChangeAwareList<int>();

        var changes = list.CaptureChanges();

        Assert.Equal(0, changes.Count);
        Assert.Same(ChangeSet<int>.Empty, changes);
    }

    [Fact]
    public void MultipleOperations_CapturesAllChanges()
    {
        var list = new ChangeAwareList<int>();

        list.Add(1);
        list.Add(2);
        list.AddRange(new[] { 3, 4 });
        list.RemoveAt(0);

        var changes = list.CaptureChanges();

        Assert.Equal(4, changes.Count);
        Assert.Equal(4, changes.Adds);
        Assert.Equal(1, changes.Removes);
        Assert.Equal(new[] { 2, 3, 4 }, list.ToList());
    }
}
