
// Port of DynamicData to R3.

using R3.DynamicData.List;

namespace R3.DynamicData.Tests;

public class SourceListTests
{
    [Fact]
    public void Connect_EmitsInitialSnapshot()
    {
        // Arrange
        var list = new SourceList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);

        var changesList = new List<IChangeSet<int>>();

        // Act
        list.Connect().Subscribe(changesList.Add);

        // Assert
        Assert.Single(changesList);
        var snapshot = changesList[0];
        Assert.Equal(3, snapshot.Count);
        Assert.Contains(snapshot, c => c.Item == 1 && c.CurrentIndex == 0 && c.Reason == ListChangeReason.Add);
        Assert.Contains(snapshot, c => c.Item == 2 && c.CurrentIndex == 1 && c.Reason == ListChangeReason.Add);
        Assert.Contains(snapshot, c => c.Item == 3 && c.CurrentIndex == 2 && c.Reason == ListChangeReason.Add);
    }

    [Fact]
    public void Add_EmitsChange()
    {
        // Arrange
        var list = new SourceList<int>();
        var changesList = new List<IChangeSet<int>>();
        list.Connect().Subscribe(changesList.Add);

        // Act
        list.Add(42);

        // Assert
        Assert.Single(changesList);
        var changes = changesList[0];
        Assert.Single(changes);
        var change = changes.First();
        Assert.Equal(42, change.Item);
        Assert.Equal(0, change.CurrentIndex);
        Assert.Equal(ListChangeReason.Add, change.Reason);
    }

    [Fact]
    public void AddRange_EmitsChanges()
    {
        // Arrange
        var list = new SourceList<int>();
        var changesList = new List<IChangeSet<int>>();
        list.Connect().Subscribe(changesList.Add);

        // Act
        list.AddRange(new[] { 1, 2, 3 });

        // Assert
        Assert.Single(changesList);
        var changes = changesList[0];
        Assert.Single(changes);
        var change = changes.First();
        Assert.Equal(ListChangeReason.AddRange, change.Reason);
        Assert.Equal(0, change.CurrentIndex);
        Assert.Equal(new[] { 1, 2, 3 }, change.Range);
    }

    [Fact]
    public void Insert_EmitsChangeAtIndex()
    {
        // Arrange
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 3 });
        var changesList = new List<IChangeSet<int>>();
        list.Connect().Subscribe(changesList.Add);

        // Act
        list.Insert(1, 2);

        // Assert
        Assert.Equal(2, changesList.Count); // snapshot + insert
        var changes = changesList[1];
        Assert.Single(changes);
        var change = changes.First();
        Assert.Equal(2, change.Item);
        Assert.Equal(1, change.CurrentIndex);
        Assert.Equal(ListChangeReason.Add, change.Reason);
        Assert.Equal(new[] { 1, 2, 3 }, list.Items);
    }

    [Fact]
    public void InsertRange_EmitsChangesAtIndex()
    {
        // Arrange
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 5 });
        var changesList = new List<IChangeSet<int>>();
        list.Connect().Subscribe(changesList.Add);

        // Act
        list.InsertRange(1, new[] { 2, 3, 4 });

        // Assert
        Assert.Equal(2, changesList.Count); // snapshot + insert range
        var changes = changesList[1];
        Assert.Single(changes);
        var change = changes.First();
        Assert.Equal(ListChangeReason.AddRange, change.Reason);
        Assert.Equal(1, change.CurrentIndex);
        Assert.Equal(new[] { 2, 3, 4 }, change.Range);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, list.Items);
    }

    [Fact]
    public void Remove_EmitsChange()
    {
        // Arrange
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 2, 3 });
        var changesList = new List<IChangeSet<int>>();
        list.Connect().Subscribe(changesList.Add);

        // Act
        list.Remove(2);

        // Assert
        Assert.Equal(2, changesList.Count); // snapshot + remove
        var changes = changesList[1];
        Assert.Single(changes);
        var change = changes.First();
        Assert.Equal(2, change.Item);
        Assert.Equal(1, change.CurrentIndex);
        Assert.Equal(ListChangeReason.Remove, change.Reason);
        Assert.Equal(new[] { 1, 3 }, list.Items);
    }

    [Fact]
    public void RemoveAt_EmitsChange()
    {
        // Arrange
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 2, 3 });
        var changesList = new List<IChangeSet<int>>();
        list.Connect().Subscribe(changesList.Add);

        // Act
        list.RemoveAt(1);

        // Assert
        Assert.Equal(2, changesList.Count); // snapshot + remove
        var changes = changesList[1];
        Assert.Single(changes);
        var change = changes.First();
        Assert.Equal(2, change.Item);
        Assert.Equal(1, change.CurrentIndex);
        Assert.Equal(ListChangeReason.Remove, change.Reason);
        Assert.Equal(new[] { 1, 3 }, list.Items);
    }

    [Fact]
    public void RemoveRange_EmitsChanges()
    {
        // Arrange
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 2, 3, 4, 5 });
        var changesList = new List<IChangeSet<int>>();
        list.Connect().Subscribe(changesList.Add);

        // Act
        list.RemoveRange(1, 3);

        // Assert
        Assert.Equal(2, changesList.Count); // snapshot + remove range
        var changes = changesList[1];
        Assert.Single(changes);
        var change = changes.First();
        Assert.Equal(ListChangeReason.RemoveRange, change.Reason);
        Assert.Equal(1, change.CurrentIndex);
        Assert.Equal(new[] { 2, 3, 4 }, change.Range);
        Assert.Equal(3, changes.Removes);
        Assert.Equal(new[] { 1, 5 }, list.Items);
    }

    [Fact]
    public void RemoveMany_EmitsChangesForEachItem()
    {
        // Arrange
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 2, 3, 4, 5 });
        var changesList = new List<IChangeSet<int>>();
        list.Connect().Subscribe(changesList.Add);

        // Act
        list.RemoveMany(new[] { 2, 4 });

        // Assert
        Assert.Equal(2, changesList.Count); // snapshot + removes
        var changes = changesList[1];
        Assert.Equal(2, changes.Count);
        Assert.Equal(2, changes.Removes);
        Assert.Equal(new[] { 1, 3, 5 }, list.Items);
    }

    [Fact]
    public void Replace_EmitsChange()
    {
        // Arrange
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 2, 3 });
        var changesList = new List<IChangeSet<int>>();
        list.Connect().Subscribe(changesList.Add);

        // Act
        list.Replace(2, 42);

        // Assert
        Assert.Equal(2, changesList.Count); // snapshot + replace
        var changes = changesList[1];
        Assert.Single(changes);
        var change = changes.First();
        Assert.Equal(42, change.Item);
        Assert.Equal(1, change.CurrentIndex);
        Assert.Equal(ListChangeReason.Replace, change.Reason);
        Assert.Equal(new[] { 1, 42, 3 }, list.Items);
    }

    [Fact]
    public void ReplaceAt_EmitsChange()
    {
        // Arrange
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 2, 3 });
        var changesList = new List<IChangeSet<int>>();
        list.Connect().Subscribe(changesList.Add);

        // Act
        list.ReplaceAt(1, 42);

        // Assert
        Assert.Equal(2, changesList.Count); // snapshot + replace
        var changes = changesList[1];
        Assert.Single(changes);
        var change = changes.First();
        Assert.Equal(42, change.Item);
        Assert.Equal(1, change.CurrentIndex);
        Assert.Equal(ListChangeReason.Replace, change.Reason);
        Assert.Equal(new[] { 1, 42, 3 }, list.Items);
    }

    [Fact]
    public void Move_EmitsChangeWithBothIndices()
    {
        // Arrange
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 2, 3, 4 });
        var changesList = new List<IChangeSet<int>>();
        list.Connect().Subscribe(changesList.Add);

        // Act
        list.Move(0, 2);

        // Assert
        Assert.Equal(2, changesList.Count); // snapshot + move
        var changes = changesList[1];
        Assert.Single(changes);
        var change = changes.First();
        Assert.Equal(1, change.Item);
        Assert.Equal(2, change.CurrentIndex);
        Assert.Equal(0, change.PreviousIndex);
        Assert.Equal(ListChangeReason.Moved, change.Reason);
        Assert.Equal(1, changes.Moves);
        Assert.Equal(new[] { 2, 3, 1, 4 }, list.Items);
    }

    [Fact]
    public void Clear_EmitsChange()
    {
        // Arrange
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 2, 3 });
        var changesList = new List<IChangeSet<int>>();
        list.Connect().Subscribe(changesList.Add);

        // Act
        list.Clear();

        // Assert
        Assert.Equal(2, changesList.Count); // snapshot + clear
        var changes = changesList[1];
        Assert.Single(changes);
        var change = changes.First();
        Assert.Equal(ListChangeReason.Clear, change.Reason);
        Assert.Equal(-1, change.CurrentIndex);
        Assert.Empty(list.Items);
    }

    [Fact]
    public void Edit_BatchesMultipleOperations()
    {
        // Arrange
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 2, 3 });
        var changesList = new List<IChangeSet<int>>();
        list.Connect().Subscribe(changesList.Add);

        // Act
        list.Edit(updater =>
        {
            updater.Add(4);
            updater.Remove(2);
            updater.Insert(0, 0);
        });

        // Assert
        Assert.Equal(2, changesList.Count); // snapshot + batch
        var changes = changesList[1];
        Assert.Equal(3, changes.Count);
        Assert.Equal(2, changes.Adds); // Add + Insert
        Assert.Equal(1, changes.Removes);
        Assert.Equal(new[] { 0, 1, 3, 4 }, list.Items);
    }

    [Fact]
    public void CountChanged_EmitsAfterEachOperation()
    {
        // Arrange
        var list = new SourceList<int>();
        var counts = new List<int>();
        list.CountChanged.Subscribe(counts.Add);

        // Act
        list.Add(1);
        list.AddRange(new[] { 2, 3 });
        list.Remove(2);
        list.Clear();

        // Assert
        Assert.Equal(new[] { 1, 3, 2, 0 }, counts);
    }

    [Fact]
    public void Count_ReturnsCurrentCount()
    {
        // Arrange
        var list = new SourceList<int>();

        // Act & Assert
        Assert.Equal(0, list.Count);
        list.Add(1);
        Assert.Equal(1, list.Count);
        list.AddRange(new[] { 2, 3 });
        Assert.Equal(3, list.Count);
        list.Remove(2);
        Assert.Equal(2, list.Count);
        list.Clear();
        Assert.Equal(0, list.Count);
    }

    [Fact]
    public void Items_ReturnsSnapshot()
    {
        // Arrange
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 2, 3 });

        // Act
        var items = list.Items;
        list.Add(4); // Modify after getting snapshot

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, items);
        Assert.Equal(new[] { 1, 2, 3, 4 }, list.Items);
    }

    [Fact]
    public void Edit_EmptyOperations_DoesNotEmit()
    {
        // Arrange
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 2, 3 });
        var changesList = new List<IChangeSet<int>>();
        list.Connect().Subscribe(changesList.Add);

        // Act
        list.Edit(updater => { }); // No operations

        // Assert
        Assert.Single(changesList); // Only snapshot, no empty changeset
    }

    [Fact]
    public void AddRange_EmptyCollection_DoesNotEmit()
    {
        // Arrange
        var list = new SourceList<int>();
        var changesList = new List<IChangeSet<int>>();
        list.Connect().Subscribe(changesList.Add);

        // Act
        list.AddRange(Array.Empty<int>());

        // Assert
        Assert.Empty(changesList); // No empty changeset emitted
    }

    [Fact]
    public void Clear_EmptyList_DoesNotEmit()
    {
        // Arrange
        var list = new SourceList<int>();
        var changesList = new List<IChangeSet<int>>();
        list.Connect().Subscribe(changesList.Add);

        // Act
        list.Clear();

        // Assert
        Assert.Empty(changesList); // No changeset for empty clear
    }
}
