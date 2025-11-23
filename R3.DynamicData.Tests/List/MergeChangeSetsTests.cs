using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using R3.DynamicData.List;
using Xunit;

namespace R3.DynamicData.Tests.List;

public class MergeChangeSetsTests
{
    [Fact]
    public void MergeChangeSets_SingleSource_Passthrough()
    {
        var source = new SourceList<int>();
        var current = new List<int>();
        using var sub = ObservableListEx.MergeChangeSets(source.Connect())
            .Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            current.Insert(change.CurrentIndex, change.Item);
                            break;
                        case ListChangeReason.Remove:
                            current.RemoveAt(change.CurrentIndex);
                            break;
                    }
                }
            });

        source.AddRange(new[] { 1, 2, 3 });
        Assert.Equal(new[] { 1, 2, 3 }, current);
        source.Remove(2);
        Assert.Equal(new[] { 1, 3 }, current);
    }

    [Fact]
    public void MergeChangeSets_TwoSources_UnionBehavior()
    {
        var s1 = new SourceList<int>();
        var s2 = new SourceList<int>();
        var current = new List<int>();
        using var sub = ObservableListEx.MergeChangeSets(s1.Connect(), s2.Connect())
            .Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    if (change.Reason == ListChangeReason.Add)
                    {
                        current.Insert(change.CurrentIndex, change.Item);
                    }
                    else if (change.Reason == ListChangeReason.Remove)
                    {
                        current.RemoveAt(change.CurrentIndex);
                    }
                }
            });

        s1.AddRange(new[] { 1, 2, 3 });
        Assert.Equal(new[] { 1, 2, 3 }, current);

        s2.AddRange(new[] { 3, 4 });
        Assert.Equal(new[] { 1, 2, 3, 4 }, current);

        s1.Remove(2);
        Assert.Equal(new[] { 1, 3, 4 }, current);

        s1.Remove(3); // Still present in s2
        Assert.Equal(new[] { 1, 3, 4 }, current);

        s2.Remove(3); // Now removed everywhere
        Assert.Equal(new[] { 1, 4 }, current);
    }

    [Fact]
    public void MergeChangeSets_Replace_Item()
    {
        var s1 = new SourceList<int>();
        var s2 = new SourceList<int>();
        var current = new List<int>();
        using var sub = ObservableListEx.MergeChangeSets(s1.Connect(), s2.Connect())
            .Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    if (change.Reason == ListChangeReason.Add)
                    {
                        current.Insert(change.CurrentIndex, change.Item);
                    }
                    else if (change.Reason == ListChangeReason.Remove)
                    {
                        current.RemoveAt(change.CurrentIndex);
                    }
                }
            });

        s1.AddRange(new[] { 1, 2 });
        Assert.Equal(new[] { 1, 2 }, current);

        s1.Replace(2, 5);
        Assert.Equal(new[] { 1, 5 }, current);

        s2.Add(2); // Reintroduce 2
        Assert.Equal(new[] { 1, 5, 2 }, current);

        s2.Replace(2, 7); // Replace 2 with 7
        Assert.Equal(new[] { 1, 5, 7 }, current);
    }

    [Fact]
    public void MergeChangeSets_ClearOneSource_OthersPersist()
    {
        var s1 = new SourceList<int>();
        var s2 = new SourceList<int>();
        var current = new List<int>();
        using var sub = ObservableListEx.MergeChangeSets(s1.Connect(), s2.Connect())
            .Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    if (change.Reason == ListChangeReason.Add)
                    {
                        current.Insert(change.CurrentIndex, change.Item);
                    }
                    else if (change.Reason == ListChangeReason.Remove)
                    {
                        current.RemoveAt(change.CurrentIndex);
                    }
                }
            });

        s1.AddRange(new[] { 1, 2 });
        s2.AddRange(new[] { 3 });
        Assert.Equal(new[] { 1, 2, 3 }, current);

        s1.Clear(); // Should only remove 1,2, keep 3
        Assert.Equal(new[] { 3 }, current);

        s2.Clear(); // Everything gone
        Assert.Empty(current);
    }
}
