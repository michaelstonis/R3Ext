using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;
using R3.DynamicData.List;
using R3.DynamicData.Operators;
using Xunit;

namespace R3Ext.Tests;

public class CacheOperatorParityPhase2Tests
{
    private sealed class Item
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public int Value { get; set; }
    }

    [Fact]
    public async Task Filter_StaticPredicate_AddRemove()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var filtered = cache.Connect().Filter(i => i.Value % 2 == 0);

        var results = new List<IReadOnlyCollection<Item>>();
        using var sub = filtered.ToCollection().Subscribe(x => results.Add(x));

        await Task.Delay(10);
        Assert.Empty(results[0]);

        cache.AddOrUpdate(new Item { Id = 1, Value = 2 });
        await Task.Delay(10);
        Assert.Single(results.Last());

        cache.AddOrUpdate(new Item { Id = 2, Value = 3 });
        await Task.Delay(10);
        Assert.Single(results.Last());

        cache.Remove(1);
        await Task.Delay(10);
        Assert.Empty(results.Last());
    }

    [Fact]
    public async Task DynamicFilter_Reevaluates()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var predicateSubject = new Subject<Func<Item, bool>>();
        var observable = cache.Connect().Filter(predicateSubject.AsObservable());

        var counts = new List<int>();
        using var sub = observable.QueryWhenChanged(q => q.Count).Subscribe(x => counts.Add(x));

        predicateSubject.OnNext(i => i.Value > 5);
        await Task.Delay(10);

        cache.AddOrUpdate(new Item { Id = 1, Value = 1 });
        await Task.Delay(10);

        cache.AddOrUpdate(new Item { Id = 2, Value = 10 });
        await Task.Delay(10);

        predicateSubject.OnNext(i => i.Value >= 1);
        await Task.Delay(10);

        Assert.True(counts.Last() >= 2);
    }

    [Fact]
    public async Task AddKey_ProducesKeyedChanges()
    {
        var list = new SourceList<Item>();
        var results = new List<IQuery<Item, int>>();

        Observable<R3.DynamicData.Cache.IChangeSet<Item, int>> keyed = list.Connect().AddKey<Item, int>(i => i.Id);
        using var sub = keyed.QueryWhenChanged().Subscribe(x => results.Add(x));

        list.Add(new Item { Id = 10, Value = 5 });
        await Task.Delay(10);

        Assert.Equal(10, results.Last().Items.First().Id);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Cast_KeyedChanges()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        cache.AddOrUpdate(new Item { Id = 7, Value = 11 });

        var casted = cache.Connect().Cast<Item, int, string>(i => i.Value.ToString());
        var results = new List<IQuery<string, int>>();
        using var sub = casted.QueryWhenChanged().Subscribe(x => results.Add(x));

        await Task.Delay(10);
        Assert.Equal("11", results.Last().Items.First());
    }

    [Fact]
    public async Task ToObservableOptional_Emits()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var optional = cache.Connect().ToObservableOptional(5);

        var results = new List<Optional<Item>>();
        using var sub = optional.Subscribe(x => results.Add(x));

        cache.AddOrUpdate(new Item { Id = 5, Value = 3 });
        await Task.Delay(10);
        Assert.True(results.Last().HasValue);

        cache.Remove(5);
        await Task.Delay(10);
        Assert.False(results.Last().HasValue);
    }

    [Fact]
    public async Task Combine_Or_Simple()
    {
        var c1 = new SourceCache<Item, int>(i => i.Id);
        var c2 = new SourceCache<Item, int>(i => i.Id);

        c1.AddOrUpdate(new Item { Id = 1, Value = 1 });
        c2.AddOrUpdate(new Item { Id = 2, Value = 2 });

        var union = c1.Connect().Or(c2.Connect());
        var results = new List<IQuery<Item, int>>();
        using var sub = union.QueryWhenChanged().Subscribe(x => results.Add(x));

        await Task.Delay(10);
        Assert.Equal(2, results.Last().Count);
    }

    [Fact]
    public async Task TrueForAny_Works()
    {
        var cache = new SourceCache<Item, int>(i => i.Id);
        var boolStream = cache.Connect().TrueForAny<Item, int, int>(
            i => Observable.Return(i.Value),
            (item, val) => val > 10);

        var results = new List<bool>();
        using var sub = boolStream.Subscribe(x => results.Add(x));

        cache.AddOrUpdate(new Item { Id = 1, Value = 5 });
        await Task.Delay(10);
        Assert.False(results.Last());

        cache.AddOrUpdate(new Item { Id = 2, Value = 15 });
        await Task.Delay(10);
        Assert.True(results.Last());
    }
}
