using BenchmarkDotNet.Attributes;
using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;
using R3.DynamicData.List;

namespace R3Ext.Benchmarks;

/// <summary>
/// Benchmarks for Phase 2 cache operators (AddKey, Combine, TrueForAny, QueryWhenChanged).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class Phase2CacheOperatorBenchmarks
{
    private SourceList<TestItem> _sourceList = null!;
    private SourceCache<TestItem, int> _sourceCache1 = null!;
    private SourceCache<TestItem, int> _sourceCache2 = null!;
    private List<TestItem> _items = null!;

    [Params(100, 1000, 10000)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _items = Enumerable.Range(0, ItemCount)
            .Select(i => new TestItem { Id = i, Value = i * 2, Name = $"Item{i}" })
            .ToList();

        _sourceList = new SourceList<TestItem>();
        _sourceCache1 = new SourceCache<TestItem, int>(x => x.Id);
        _sourceCache2 = new SourceCache<TestItem, int>(x => x.Id);

        // Pre-populate caches for Combine benchmarks
        _sourceCache1.AddOrUpdate(_items.Take(ItemCount / 2));
        _sourceCache2.AddOrUpdate(_items.Skip(ItemCount / 2));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _sourceList?.Dispose();
        _sourceCache1?.Dispose();
        _sourceCache2?.Dispose();
    }

    [Benchmark]
    public void AddKey_ListToCache()
    {
        var results = new List<IChangeSet<TestItem, int>>();
        using var sub = _sourceList.Connect()
            .AddKey<TestItem, int>(x => x.Id)
            .Subscribe(changes => results.Add(changes));

        _sourceList.AddRange(_items);
        _sourceList.Clear();
    }

    [Benchmark]
    public void Combine_Or_TwoCaches()
    {
        var results = new List<IChangeSet<TestItem, int>>();
        using var sub = _sourceCache1.Connect()
            .Or(_sourceCache2.Connect())
            .Subscribe(changes => results.Add(changes));

        // Trigger some changes
        _sourceCache1.AddOrUpdate(new TestItem { Id = ItemCount + 1, Value = 999 });
        _sourceCache2.AddOrUpdate(new TestItem { Id = ItemCount + 2, Value = 888 });
    }

    [Benchmark]
    public void Combine_And_TwoCaches()
    {
        var results = new List<IChangeSet<TestItem, int>>();
        using var sub = _sourceCache1.Connect()
            .And(_sourceCache2.Connect())
            .Subscribe(changes => results.Add(changes));

        // Add overlapping items to both caches
        var overlap = new TestItem { Id = 999999, Value = 777 };
        _sourceCache1.AddOrUpdate(overlap);
        _sourceCache2.AddOrUpdate(overlap);
    }

    [Benchmark]
    public void TrueForAny_BooleanAggregate()
    {
        var cache = new SourceCache<TestItem, int>(x => x.Id);
        cache.AddOrUpdate(_items.Take(100));

        var results = new List<bool>();
        using var sub = cache.Connect()
            .TrueForAny<TestItem, int, int>(
                item => Observable.Return(item.Value),
                (item, val) => val > ItemCount / 2)
            .Subscribe(b => results.Add(b));

        // Trigger recomputation
        cache.AddOrUpdate(new TestItem { Id = ItemCount + 1, Value = ItemCount * 10 });
        cache.Dispose();
    }

    [Benchmark]
    public void TrueForAll_BooleanAggregate()
    {
        var cache = new SourceCache<TestItem, int>(x => x.Id);
        cache.AddOrUpdate(_items.Take(100));

        var results = new List<bool>();
        using var sub = cache.Connect()
            .TrueForAll<TestItem, int, int>(
                item => Observable.Return(item.Value),
                (item, val) => val >= 0)
            .Subscribe(b => results.Add(b));

        // Trigger recomputation
        cache.AddOrUpdate(new TestItem { Id = ItemCount + 1, Value = -1 });
        cache.Dispose();
    }

    [Benchmark]
    public void QueryWhenChanged_Snapshots()
    {
        var cache = new SourceCache<TestItem, int>(x => x.Id);
        var results = new List<IQuery<TestItem, int>>();

        using var sub = cache.Connect()
            .QueryWhenChanged()
            .Subscribe(query => results.Add(query));

        // Perform multiple operations
        cache.AddOrUpdate(_items.Take(ItemCount / 2));
        cache.AddOrUpdate(_items.Skip(ItemCount / 2));
        cache.Remove(_items.First().Id);
        cache.Dispose();
    }

    [Benchmark]
    public void ToCollection_MaterializeList()
    {
        var cache = new SourceCache<TestItem, int>(x => x.Id);
        var results = new List<IReadOnlyList<TestItem>>();

        using var sub = cache.Connect()
            .ToCollection()
            .Subscribe(collection => results.Add(collection));

        // Perform batch operations
        cache.AddOrUpdate(_items);
        cache.Remove(_items.Take(ItemCount / 4).Select(x => x.Id));
        cache.Dispose();
    }

    [Benchmark]
    public void Cast_TypeConversion()
    {
        var cache = new SourceCache<TestItem, int>(x => x.Id);
        cache.AddOrUpdate(_items);

        var results = new List<IChangeSet<string, int>>();
        using var sub = cache.Connect()
            .Cast<TestItem, int, string>(item => item.Name)
            .Subscribe(changes => results.Add(changes));

        cache.AddOrUpdate(new TestItem { Id = ItemCount + 1, Value = 123, Name = "NewItem" });
        cache.Dispose();
    }

    [Benchmark]
    public void ToObservableOptional_SingleKeyTracking()
    {
        var cache = new SourceCache<TestItem, int>(x => x.Id);
        cache.AddOrUpdate(_items);

        var results = new List<Optional<TestItem>>();
        using var sub = cache.Connect()
            .ToObservableOptional(ItemCount / 2)
            .Subscribe(opt => results.Add(opt));

        cache.Remove(ItemCount / 2);
        cache.AddOrUpdate(new TestItem { Id = ItemCount / 2, Value = 999 });
        cache.Dispose();
    }

    private sealed class TestItem
    {
        public int Id { get; set; }
        public int Value { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
