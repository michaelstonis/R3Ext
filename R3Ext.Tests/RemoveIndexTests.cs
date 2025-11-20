using R3; // For Observable extensions
using R3.DynamicData.List;
using Xunit;

namespace R3Ext.Tests;

public class RemoveIndexTests
{
    [Fact]
    public void RemoveIndex_RemovesIndicesFromAllChanges()
    {
        var source = new SourceList<int>();
        var captured = new List<IChangeSet<int>>();
        var subscription = source.Connect().RemoveIndex().Subscribe(c => captured.Add(c));

        source.Add(1);
        source.Add(2);
        source.AddRange(new[] { 3, 4 });
        source.Move(0, 2); // Move operation
        source.Replace(2, 5); // Replace operation (value 2 at index 1 -> replaced later?)
        source.Remove(3); // Remove a value from range added
        source.Clear();

        subscription.Dispose();

        Assert.NotEmpty(captured);
        foreach (var changeSet in captured)
        {
            foreach (var change in changeSet)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Moved:
                        Assert.Equal(-1, change.CurrentIndex);
                        Assert.Equal(-1, change.PreviousIndex);
                        break;
                    case ListChangeReason.Refresh:
                        Assert.Equal(-1, change.CurrentIndex);
                        break;
                    default:
                        Assert.Equal(-1, change.CurrentIndex);
                        break;
                }
            }
        }
    }
}
