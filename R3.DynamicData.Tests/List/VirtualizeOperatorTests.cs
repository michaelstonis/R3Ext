using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public class VirtualizeOperatorTests
{
    [Fact]
    public void Virtualize_InitialWindow_EmitsCorrectSubset()
    {
        var source = new SourceList<int>();
        var virtualRequests = new Subject<VirtualRequest>();
        var results = new List<IChangeSet<int>>();
        var currentState = new List<int>();

        using var sub = source.Connect()
            .Virtualize(virtualRequests)
            .Subscribe(changes =>
            {
                results.Add(changes);
                ApplyChanges(currentState, changes);
            });

        // Add 10 items
        source.AddRange(Enumerable.Range(0, 10));

        // Request first 5 items
        virtualRequests.OnNext(new VirtualRequest(0, 5));

        Assert.Equal(2, results.Count); // Initial add + window request
        Assert.Equal(5, currentState.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, currentState);
    }

    [Fact]
    public void Virtualize_WindowSliding_EmitsCorrectChanges()
    {
        var source = new SourceList<int>();
        var virtualRequests = new Subject<VirtualRequest>();
        var results = new List<IChangeSet<int>>();
        var currentState = new List<int>();

        using var sub = source.Connect()
            .Virtualize(virtualRequests)
            .Subscribe(changes =>
            {
                results.Add(changes);
                ApplyChanges(currentState, changes);
            });

        // Add 20 items
        source.AddRange(Enumerable.Range(0, 20));

        // Window 1: items 0-4
        virtualRequests.OnNext(new VirtualRequest(0, 5));
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, currentState);

        // Window 2: items 5-9
        virtualRequests.OnNext(new VirtualRequest(5, 5));
        Assert.Equal(new[] { 5, 6, 7, 8, 9 }, currentState);

        // Window 3: items 10-14
        virtualRequests.OnNext(new VirtualRequest(10, 5));
        Assert.Equal(new[] { 10, 11, 12, 13, 14 }, currentState);
    }

    [Fact]
    public void Virtualize_OutOfBounds_HandlesGracefully()
    {
        var source = new SourceList<int>();
        var virtualRequests = new Subject<VirtualRequest>();
        var results = new List<IChangeSet<int>>();
        var currentState = new List<int>();

        using var sub = source.Connect()
            .Virtualize(virtualRequests)
            .Subscribe(changes =>
            {
                results.Add(changes);
                ApplyChanges(currentState, changes);
            });

        // Add 10 items
        source.AddRange(Enumerable.Range(0, 10));

        // Request beyond bounds
        virtualRequests.OnNext(new VirtualRequest(15, 5));

        Assert.Empty(currentState); // No items in window
    }

    [Fact]
    public void Virtualize_PartialWindow_ReturnsAvailableItems()
    {
        var source = new SourceList<int>();
        var virtualRequests = new Subject<VirtualRequest>();
        var results = new List<IChangeSet<int>>();
        var currentState = new List<int>();

        using var sub = source.Connect()
            .Virtualize(virtualRequests)
            .Subscribe(changes =>
            {
                results.Add(changes);
                ApplyChanges(currentState, changes);
            });

        // Add 10 items
        source.AddRange(Enumerable.Range(0, 10));

        // Request 5 items starting at index 8 (only 2 available)
        virtualRequests.OnNext(new VirtualRequest(8, 5));

        Assert.Equal(2, currentState.Count);
        Assert.Equal(new[] { 8, 9 }, currentState);
    }

    [Fact]
    public void Virtualize_DynamicDataChanges_UpdatesWindow()
    {
        var source = new SourceList<int>();
        var virtualRequests = new Subject<VirtualRequest>();
        var results = new List<IChangeSet<int>>();
        var currentState = new List<int>();

        using var sub = source.Connect()
            .Virtualize(virtualRequests)
            .Subscribe(changes =>
            {
                results.Add(changes);
                ApplyChanges(currentState, changes);
            });

        // Add initial data
        source.AddRange(Enumerable.Range(0, 10));
        virtualRequests.OnNext(new VirtualRequest(0, 5));

        results.Clear();

        // Add more items (should update window if it affects visible range)
        source.Insert(2, 100);

        Assert.True(results.Count > 0);
        Assert.Contains(100, currentState);
    }

    [Fact]
    public void Virtualize_WithSort_MaintainsSortedWindow()
    {
        var source = new SourceCache<Person, int>(x => x.Id);
        var virtualRequests = new Subject<VirtualRequest>();
        var results = new List<IChangeSet<Person>>();
        var currentState = new List<Person>();

        using var sub = source.Connect()
            .Sort(Comparer<Person>.Create((x, y) => x.Name.CompareTo(y.Name)))
            .Virtualize(virtualRequests)
            .Subscribe(changes =>
            {
                results.Add(changes);
                ApplyChanges(currentState, changes);
            });

        // Add people in random order
        source.AddOrUpdate(new Person { Id = 1, Name = "Charlie" });
        source.AddOrUpdate(new Person { Id = 2, Name = "Alice" });
        source.AddOrUpdate(new Person { Id = 3, Name = "Bob" });
        source.AddOrUpdate(new Person { Id = 4, Name = "Diana" });
        source.AddOrUpdate(new Person { Id = 5, Name = "Eve" });

        // Request first 3 (should be Alice, Bob, Charlie)
        virtualRequests.OnNext(new VirtualRequest(0, 3));

        Assert.Equal(3, currentState.Count);
        Assert.Equal("Alice", currentState[0].Name);
        Assert.Equal("Bob", currentState[1].Name);
        Assert.Equal("Charlie", currentState[2].Name);

        // Request next 2 (should be Diana, Eve)
        virtualRequests.OnNext(new VirtualRequest(3, 2));

        Assert.Equal(2, currentState.Count);
        Assert.Equal("Diana", currentState[0].Name);
        Assert.Equal("Eve", currentState[1].Name);
    }

    [Fact]
    public void Page_SequentialPages_EmitsCorrectWindows()
    {
        var source = new SourceList<int>();
        var pageRequests = new Subject<int>();
        var results = new List<IChangeSet<int>>();
        var currentState = new List<int>();

        using var sub = source.Connect()
            .Page(pageRequests, pageSize: 5)
            .Subscribe(changes =>
            {
                results.Add(changes);
                ApplyChanges(currentState, changes);
            });

        // Add 15 items
        source.AddRange(Enumerable.Range(0, 15));

        // Page 0 (items 0-4)
        pageRequests.OnNext(0);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, currentState);

        // Page 1 (items 5-9)
        pageRequests.OnNext(1);
        Assert.Equal(new[] { 5, 6, 7, 8, 9 }, currentState);

        // Page 2 (items 10-14)
        pageRequests.OnNext(2);
        Assert.Equal(new[] { 10, 11, 12, 13, 14 }, currentState);
    }

    [Fact]
    public void Page_CustomPageSize_WorksCorrectly()
    {
        var source = new SourceList<int>();
        var pageRequests = new Subject<int>();
        var results = new List<IChangeSet<int>>();
        var currentState = new List<int>();

        using var sub = source.Connect()
            .Page(pageRequests, pageSize: 3)
            .Subscribe(changes =>
            {
                results.Add(changes);
                ApplyChanges(currentState, changes);
            });

        // Add 10 items
        source.AddRange(Enumerable.Range(0, 10));

        // Page 0 (items 0-2)
        pageRequests.OnNext(0);
        Assert.Equal(new[] { 0, 1, 2 }, currentState);

        // Page 2 (items 6-8)
        pageRequests.OnNext(2);
        Assert.Equal(new[] { 6, 7, 8 }, currentState);
    }

    [Fact]
    public void Virtualize_EmptySource_HandlesGracefully()
    {
        var source = new SourceList<int>();
        var virtualRequests = new Subject<VirtualRequest>();
        var results = new List<IChangeSet<int>>();
        var currentState = new List<int>();

        using var sub = source.Connect()
            .Virtualize(virtualRequests)
            .Subscribe(changes =>
            {
                results.Add(changes);
                ApplyChanges(currentState, changes);
            });

        // Request window on empty source
        virtualRequests.OnNext(new VirtualRequest(0, 5));

        Assert.Empty(currentState);
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

                case ListChangeReason.Replace:
                    if (change.CurrentIndex >= 0 && change.CurrentIndex < state.Count)
                    {
                        state[change.CurrentIndex] = change.Item;
                    }

                    break;

                case ListChangeReason.Clear:
                    state.Clear();
                    break;

                case ListChangeReason.AddRange:
                    if (change.Range.Count > 0)
                    {
                        state.AddRange(change.Range);
                    }

                    break;
            }
        }
    }

    private class Person
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
