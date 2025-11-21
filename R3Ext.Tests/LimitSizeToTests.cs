using R3;
using R3.DynamicData.List;
using Xunit;

namespace R3Ext.Tests;

public class LimitSizeToTests
{
    private static List<T> ApplyChanges<T>(IEnumerable<IChangeSet<T>> changeSets)
    {
        var items = new List<T>();
        foreach (var cs in changeSets)
        {
            foreach (var change in cs)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Add:
                        if (change.CurrentIndex >= 0 && change.CurrentIndex <= items.Count)
                        {
                            items.Insert(change.CurrentIndex, change.Item);
                        }
                        else
                        {
                            items.Add(change.Item);
                        }

                        break;

                    case ListChangeReason.Remove:
                        if (change.CurrentIndex >= 0 && change.CurrentIndex < items.Count && EqualityComparer<T>.Default.Equals(items[change.CurrentIndex], change.Item))
                        {
                            items.RemoveAt(change.CurrentIndex);
                        }
                        else
                        {
                            var idx = items.IndexOf(change.Item);
                            if (idx >= 0)
                            {
                                items.RemoveAt(idx);
                            }
                        }

                        break;

                    case ListChangeReason.Replace:
                        if (change.CurrentIndex >= 0 && change.CurrentIndex < items.Count)
                        {
                            items[change.CurrentIndex] = change.Item;
                        }

                        break;

                    case ListChangeReason.Moved:
                        if (change.PreviousIndex >= 0 && change.PreviousIndex < items.Count && change.CurrentIndex >= 0 && change.CurrentIndex <= items.Count)
                        {
                            var moved = items[change.PreviousIndex];
                            items.RemoveAt(change.PreviousIndex);
                            var target = Math.Min(change.CurrentIndex, items.Count);
                            items.Insert(target, moved);
                        }

                        break;

                    case ListChangeReason.Clear:
                        items.Clear();
                        break;
                }
            }
        }

        return items;
    }

    [Fact]
    public void LimitSizeTo_NoEvictionUnderLimit()
    {
        var source = new SourceList<int>();
        var emitted = new List<IChangeSet<int>>();
        using var sub = source.Connect().LimitSizeTo(10).Subscribe(cs => emitted.Add(cs));

        source.Add(1);
        source.Add(2);
        source.Add(3);

        Assert.NotEmpty(emitted);
        var final = ApplyChanges(emitted);
        Assert.Equal(new[] { 1, 2, 3 }, final);
        Assert.DoesNotContain(emitted.SelectMany(cs => cs), c => c.Reason == ListChangeReason.Remove);
    }

    [Fact]
    public void LimitSizeTo_FifoSingleEviction()
    {
        var source = new SourceList<int>();
        var emitted = new List<IChangeSet<int>>();
        using var sub = source.Connect().LimitSizeTo(3).Subscribe(cs => emitted.Add(cs));

        source.Add(1);
        source.Add(2);
        source.Add(3);
        source.Add(4); // should evict 1

        var allChanges = emitted.SelectMany(cs => cs).ToList();
        Assert.Contains(allChanges, c => c.Reason == ListChangeReason.Remove && c.Item == 1);
        var final = ApplyChanges(emitted);
        Assert.Equal(new[] { 2, 3, 4 }, final);
    }

    [Fact]
    public void LimitSizeTo_FifoMultiEviction_AddRange()
    {
        var source = new SourceList<int>();
        var emitted = new List<IChangeSet<int>>();
        using var sub = source.Connect().LimitSizeTo(3).Subscribe(cs => emitted.Add(cs));

        source.AddRange(new[] { 1, 2, 3, 4, 5 }); // should evict 1 and 2

        var all = emitted.SelectMany(cs => cs).ToList();
        Assert.Contains(all, c => c.Reason == ListChangeReason.Remove && c.Item == 1);
        Assert.Contains(all, c => c.Reason == ListChangeReason.Remove && c.Item == 2);
        var final = ApplyChanges(emitted);
        Assert.Equal(new[] { 3, 4, 5 }, final);
    }

    [Fact]
    public void LimitSizeTo_LifoEvictionOrder()
    {
        var source = new SourceList<int>();
        var emitted = new List<IChangeSet<int>>();
        using var sub = source.Connect().LimitSizeTo(3, LimitSizeToEviction.RemoveNewest).Subscribe(cs => emitted.Add(cs));

        source.AddRange(new[] { 1, 2, 3 });

        // overshoot by 2, evict 5 then 4
        source.AddRange(new[] { 4, 5 });

        var all = emitted.SelectMany(cs => cs).ToList();

        // Expect remove of 5 and 4
        Assert.Contains(all, c => c.Reason == ListChangeReason.Remove && c.Item == 5);
        Assert.Contains(all, c => c.Reason == ListChangeReason.Remove && c.Item == 4);
        var final = ApplyChanges(emitted);
        Assert.Equal(new[] { 1, 2, 3 }, final);
    }

    [Fact]
    public void LimitSizeTo_ReplaceDoesNotEvict()
    {
        var source = new SourceList<int>();
        var emitted = new List<IChangeSet<int>>();
        using var sub = source.Connect().LimitSizeTo(2).Subscribe(cs => emitted.Add(cs));

        source.Add(1);
        source.Add(2);
        source.Replace(1, 10); // replace value 1 with 10

        var all = emitted.SelectMany(cs => cs).ToList();
        Assert.DoesNotContain(all, c => c.Reason == ListChangeReason.Remove && (c.Item == 1 || c.Item == 2));
        var final = ApplyChanges(emitted);
        Assert.Equal(new[] { 10, 2 }, final);
    }

    [Fact]
    public void LimitSizeTo_ClearResets()
    {
        var source = new SourceList<int>();
        var emitted = new List<IChangeSet<int>>();
        using var sub = source.Connect().LimitSizeTo(3).Subscribe(cs => emitted.Add(cs));

        source.AddRange(new[] { 1, 2, 3, 4 }); // will evict 1
        source.Clear();

        var all = emitted.SelectMany(cs => cs).ToList();
        Assert.Contains(all, c => c.Reason == ListChangeReason.Clear);
        var final = ApplyChanges(emitted);
        Assert.Empty(final);
    }
}
