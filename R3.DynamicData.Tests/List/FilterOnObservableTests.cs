using R3;
using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public class FilterOnObservableTests
{
    [Fact]
    public void FilterOnObservable_InitiallyIncluded()
    {
        var source = new SourceList<TestItem>();
        var results = new List<IChangeSet<TestItem>>();

        var item = new TestItem { Id = 1, IsActive = new Subject<bool>() };
        source.Add(item);

        using var sub = source.Connect()
            .FilterOnObservable(x => x.IsActive)
            .Subscribe(results.Add);

        // Emit true - item should be included
        item.IsActive.OnNext(true);

        Assert.Single(results);
        Assert.Single(results[0]);
        Assert.Equal(ListChangeReason.Add, results[0].First().Reason);
        Assert.Equal(item, results[0].First().Item);
    }

    [Fact]
    public void FilterOnObservable_InitiallyExcluded()
    {
        var source = new SourceList<TestItem>();
        var results = new List<IChangeSet<TestItem>>();

        var item = new TestItem { Id = 1, IsActive = new Subject<bool>() };
        source.Add(item);

        using var sub = source.Connect()
            .FilterOnObservable(x => x.IsActive)
            .Subscribe(results.Add);

        // Emit false - item should not be included
        item.IsActive.OnNext(false);

        Assert.Empty(results);
    }

    [Fact]
    public void FilterOnObservable_DynamicInclusion()
    {
        var source = new SourceList<TestItem>();
        var results = new List<IChangeSet<TestItem>>();

        var item = new TestItem { Id = 1, IsActive = new Subject<bool>() };
        source.Add(item);

        using var sub = source.Connect()
            .FilterOnObservable(x => x.IsActive)
            .Subscribe(results.Add);

        // Initially exclude
        item.IsActive.OnNext(false);
        Assert.Empty(results);

        // Then include
        item.IsActive.OnNext(true);
        Assert.Single(results);
        Assert.Equal(ListChangeReason.Add, results[0].First().Reason);

        // Then exclude again
        item.IsActive.OnNext(false);
        Assert.Equal(2, results.Count);
        Assert.Equal(ListChangeReason.Remove, results[1].First().Reason);
    }

    [Fact]
    public void FilterOnObservable_MultipleItems()
    {
        var source = new SourceList<TestItem>();
        var currentState = new List<TestItem>();

        var item1 = new TestItem { Id = 1, IsActive = new Subject<bool>() };
        var item2 = new TestItem { Id = 2, IsActive = new Subject<bool>() };
        var item3 = new TestItem { Id = 3, IsActive = new Subject<bool>() };

        source.AddRange(new[] { item1, item2, item3 });

        using var sub = source.Connect()
            .FilterOnObservable(x => x.IsActive)
            .Subscribe(changes => ApplyChanges(currentState, changes));

        // Include item1 and item3
        item1.IsActive.OnNext(true);
        item2.IsActive.OnNext(false);
        item3.IsActive.OnNext(true);

        Assert.Equal(2, currentState.Count);
        Assert.Contains(item1, currentState);
        Assert.Contains(item3, currentState);
        Assert.DoesNotContain(item2, currentState);

        // Now include item2
        item2.IsActive.OnNext(true);

        Assert.Equal(3, currentState.Count);
        Assert.Contains(item1, currentState);
        Assert.Contains(item2, currentState);
        Assert.Contains(item3, currentState);
    }

    [Fact]
    public void FilterOnObservable_RemoveItem()
    {
        var source = new SourceList<TestItem>();
        var currentState = new List<TestItem>();

        var item = new TestItem { Id = 1, IsActive = new Subject<bool>() };
        source.Add(item);

        using var sub = source.Connect()
            .FilterOnObservable(x => x.IsActive)
            .Subscribe(changes => ApplyChanges(currentState, changes));

        // Include the item
        item.IsActive.OnNext(true);
        Assert.Single(currentState);

        // Remove the item
        source.Remove(item);
        Assert.Empty(currentState);

        // Further emissions should not affect the output
        item.IsActive.OnNext(false);
        item.IsActive.OnNext(true);
        Assert.Empty(currentState);
    }

    [Fact]
    public void FilterOnObservable_Clear()
    {
        var source = new SourceList<TestItem>();
        var currentState = new List<TestItem>();
        var clearEmitted = false;

        var item1 = new TestItem { Id = 1, IsActive = new Subject<bool>() };
        var item2 = new TestItem { Id = 2, IsActive = new Subject<bool>() };

        source.AddRange(new[] { item1, item2 });

        using var sub = source.Connect()
            .FilterOnObservable(x => x.IsActive)
            .Subscribe(changes =>
            {
                ApplyChanges(currentState, changes);
                if (changes.Any(c => c.Reason == ListChangeReason.Clear))
                {
                    clearEmitted = true;
                }
            });

        // Include both items
        item1.IsActive.OnNext(true);
        item2.IsActive.OnNext(true);
        Assert.Equal(2, currentState.Count);

        // Clear the source
        source.Clear();
        Assert.Empty(currentState);
        Assert.True(clearEmitted);
    }

    [Fact]
    public void FilterOnObservable_AddRange()
    {
        var source = new SourceList<TestItem>();
        var currentState = new List<TestItem>();

        using var sub = source.Connect()
            .FilterOnObservable(x => x.IsActive)
            .Subscribe(changes => ApplyChanges(currentState, changes));

        var items = new[]
        {
            new TestItem { Id = 1, IsActive = new Subject<bool>() },
            new TestItem { Id = 2, IsActive = new Subject<bool>() },
            new TestItem { Id = 3, IsActive = new Subject<bool>() },
        };

        source.AddRange(items);

        // Include all items
        items[0].IsActive.OnNext(true);
        items[1].IsActive.OnNext(true);
        items[2].IsActive.OnNext(true);

        Assert.Equal(3, currentState.Count);
    }

    [Fact]
    public void FilterOnObservable_RemoveRange()
    {
        var source = new SourceList<TestItem>();
        var currentState = new List<TestItem>();

        var items = new[]
        {
            new TestItem { Id = 1, IsActive = new Subject<bool>() },
            new TestItem { Id = 2, IsActive = new Subject<bool>() },
            new TestItem { Id = 3, IsActive = new Subject<bool>() },
        };

        source.AddRange(items);

        using var sub = source.Connect()
            .FilterOnObservable(x => x.IsActive)
            .Subscribe(changes => ApplyChanges(currentState, changes));

        // Include all items
        foreach (var item in items)
        {
            item.IsActive.OnNext(true);
        }

        Assert.Equal(3, currentState.Count);

        // Remove range
        source.RemoveRange(0, 2);

        Assert.Single(currentState);
        Assert.Equal(items[2], currentState[0]);
    }

    [Fact]
    public void FilterOnObservable_ToggleManyTimes()
    {
        var source = new SourceList<TestItem>();
        var currentState = new List<TestItem>();

        var item = new TestItem { Id = 1, IsActive = new Subject<bool>() };
        source.Add(item);

        using var sub = source.Connect()
            .FilterOnObservable(x => x.IsActive)
            .Subscribe(changes => ApplyChanges(currentState, changes));

        // Toggle multiple times
        for (int i = 0; i < 10; i++)
        {
            item.IsActive.OnNext(i % 2 == 0);
            if (i % 2 == 0)
            {
                Assert.Single(currentState);
            }
            else
            {
                Assert.Empty(currentState);
            }
        }
    }

    [Fact]
    public void FilterOnObservable_DisposalCleansUp()
    {
        var source = new SourceList<TestItem>();
        var currentState = new List<TestItem>();
        var emissionCount = 0;

        var item = new TestItem { Id = 1, IsActive = new Subject<bool>() };
        source.Add(item);

        var sub = source.Connect()
            .FilterOnObservable(x => x.IsActive)
            .Subscribe(changes =>
            {
                emissionCount++;
                ApplyChanges(currentState, changes);
            });

        item.IsActive.OnNext(true);
        Assert.Equal(1, emissionCount);

        // Dispose subscription
        sub.Dispose();

        // Further emissions should not be processed
        item.IsActive.OnNext(false);
        item.IsActive.OnNext(true);
        Assert.Equal(1, emissionCount);
    }

    private void ApplyChanges<T>(List<T> state, IChangeSet<T> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    if (change.CurrentIndex >= 0 && change.CurrentIndex <= state.Count)
                    {
                        state.Insert(change.CurrentIndex, change.Item);
                    }
                    else
                    {
                        state.Add(change.Item);
                    }

                    break;

                case ListChangeReason.Remove:
                    if (change.CurrentIndex >= 0 && change.CurrentIndex < state.Count)
                    {
                        state.RemoveAt(change.CurrentIndex);
                    }
                    else
                    {
                        state.Remove(change.Item);
                    }

                    break;

                case ListChangeReason.Clear:
                    state.Clear();
                    break;

                case ListChangeReason.AddRange:
                    state.AddRange(change.Range);
                    break;
            }
        }
    }

    private class TestItem
    {
        public int Id { get; set; }

        public Subject<bool> IsActive { get; set; } = new Subject<bool>();
    }
}
