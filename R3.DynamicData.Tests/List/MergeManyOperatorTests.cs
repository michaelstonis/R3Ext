
// Port of DynamicData to R3.

using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public class MergeManyOperatorTests
{
    private sealed class Item
    {
        public Item(int id)
        {
            Id = id;
            Subject = new Subject<int>();
        }

        public int Id { get; }

        public Subject<int> Subject { get; }

        public Observable<int> Stream => Subject.AsObservable();

        public void Emit(int value) => Subject.OnNext(value);

        public void Complete() => Subject.OnCompleted();
    }

    [Fact]
    public void MergeMany_SubscribesToChildStreams()
    {
        var source = new SourceList<Item>();
        var results = new List<int>();
        using var sub = source.Connect().MergeMany(i => i.Stream).Subscribe(results.Add);

        var item1 = new Item(1);
        var item2 = new Item(2);
        source.Add(item1);
        source.Add(item2);

        item1.Emit(10);
        item2.Emit(20);

        Assert.Equal(new[] { 10, 20 }, results);
    }

    [Fact]
    public void MergeMany_UnsubscribesOnRemove()
    {
        var source = new SourceList<Item>();
        var results = new List<int>();
        using var sub = source.Connect().MergeMany(i => i.Stream).Subscribe(results.Add);

        var item = new Item(1);
        source.Add(item);
        item.Emit(10);

        source.RemoveAt(0);
        item.Emit(20);

        Assert.Single(results);
        Assert.Equal(10, results[0]);
    }

    [Fact]
    public void MergeMany_HandlesReplace()
    {
        var source = new SourceList<Item>();
        var results = new List<int>();
        using var sub = source.Connect().MergeMany(i => i.Stream).Subscribe(results.Add);

        var item1 = new Item(1);
        source.Add(item1);
        item1.Emit(10);

        var item2 = new Item(2);
        source.Replace(item1, item2);

        item1.Emit(20);
        item2.Emit(30);

        Assert.Equal(new[] { 10, 30 }, results);
    }

    [Fact]
    public void MergeMany_HandlesClear()
    {
        var source = new SourceList<Item>();
        var results = new List<int>();
        using var sub = source.Connect().MergeMany(i => i.Stream).Subscribe(results.Add);

        var item1 = new Item(1);
        var item2 = new Item(2);
        source.AddRange(new[] { item1, item2 });
        item1.Emit(10);
        item2.Emit(20);

        source.Clear();
        item1.Emit(30);
        item2.Emit(40);

        Assert.Equal(new[] { 10, 20 }, results);
    }

    [Fact]
    public void MergeMany_CompletesWhenSourceAndChildrenComplete()
    {
        var source = new SourceList<Item>();
        var completed = false;
        var item = new Item(1);
        source.Add(item);

        using var sub = source.Connect().MergeMany(i => i.Stream).Subscribe(_ => { }, _ => completed = true);

        source.Dispose();

        Assert.False(completed);

        item.Complete();

        Assert.True(completed);
    }

    [Fact]
    public void MergeMany_DoesNotCompleteWhileChildActive()
    {
        var source = new SourceList<Item>();
        var completed = false;
        var item = new Item(1);
        source.Add(item);

        using var sub = source.Connect().MergeMany(i => i.Stream).Subscribe(_ => { }, _ => completed = true);

        source.Dispose();

        Assert.False(completed);
    }
}
