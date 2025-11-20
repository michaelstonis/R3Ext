// Tests for newly added cache operators: Transform, DistinctValues, TransformMany, DynamicFilter.

using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.List;
using R3.DynamicData.Operators; // for Transform & Filter

namespace R3.DynamicData.Tests.Cache;

#pragma warning disable SA1515 // allow inline comments without preceding blank line
#pragma warning disable SA1516 // allow compact property declarations

public class CacheOperatorsPhase2Tests
{
    private sealed class Person
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public List<string> Hobbies { get; set; } = new();
        public string City { get; set; } = string.Empty;
    }

    [Fact]
    public void Transform_Projects_Items()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var names = new List<string>();
        using var sub = cache.Connect().Transform<Person, int, string>(p => p.Name).Select(cs => cs.Select(c => c.Current)).Subscribe(list =>
        {
            names = list.ToList();
        });
        cache.AddOrUpdate(new Person { Id = 1, Name = "Alice" });
        cache.AddOrUpdate(new Person { Id = 2, Name = "Bob" });
        Assert.Contains("Alice", names);
        Assert.Contains("Bob", names);
    }

    [Fact]
    public void DistinctValues_Tracks_Unique_Cities()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var distinct = new List<string>();
        using var sub = cache.Connect().DistinctValues<Person, int, string>(p => p.City).Bind(distinct);
        cache.AddOrUpdate(new Person { Id = 1, Name = "A", City = "NY" });
        cache.AddOrUpdate(new Person { Id = 2, Name = "B", City = "LA" });
        cache.AddOrUpdate(new Person { Id = 3, Name = "C", City = "NY" }); // duplicate city
        Assert.Equal(2, distinct.Count);
        Assert.Contains("NY", distinct);
        Assert.Contains("LA", distinct);
        // Update city -> should remove LA if count hits zero
        cache.AddOrUpdate(new Person { Id = 2, Name = "B", City = "Chicago" });
        Assert.Contains("Chicago", distinct);
        Assert.DoesNotContain("LA", distinct);
    }

    [Fact]
    public void TransformMany_Flattens_Hobbies()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var hobbies = new List<string>();
        using var sub = cache.Connect().TransformMany<Person, int, string>(p => p.Hobbies).Bind(hobbies);
        cache.AddOrUpdate(new Person { Id = 1, Name = "A", Hobbies = new() { "golf", "chess" } });
        cache.AddOrUpdate(new Person { Id = 2, Name = "B", Hobbies = new() { "chess", "swim" } });
        Assert.Equal(4, hobbies.Count); // duplicates allowed
        Assert.Equal(2, hobbies.Count(h => h == "chess"));
        // Update - remove one hobby
        cache.AddOrUpdate(new Person { Id = 1, Name = "A", Hobbies = new() { "golf" } });
        Assert.Equal(3, hobbies.Count);
    }

    [Fact]
    public void DynamicFilter_Responds_To_Predicate_Changes()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var predicate = new BehaviorSubject<Func<Person, bool>>(_ => true);
        var filtered = new List<Person>();
        using var sub = cache.Connect().Filter(predicate).Select(cs => cs.Select(c => c.Current)).Subscribe(list =>
        {
            filtered = list.ToList();
        });
        cache.AddOrUpdate(new Person { Id = 1, Name = "A", Age = 25 });
        cache.AddOrUpdate(new Person { Id = 2, Name = "B", Age = 40 });
        Assert.Equal(2, filtered.Count);
        predicate.OnNext(p => p.Age >= 30);
        Assert.Single(filtered);
        Assert.Equal(40, filtered[0].Age);
        predicate.OnNext(p => p.Age < 30);
        Assert.Single(filtered);
        Assert.Equal(25, filtered[0].Age);
        // Update item while predicate is Age < 30 (should drop all items)
        cache.AddOrUpdate(new Person { Id = 1, Name = "A", Age = 31 });
        Assert.Empty(filtered);
        // Switch predicate to Age >= 30 includes both
        predicate.OnNext(p => p.Age >= 30);
        Assert.Equal(2, filtered.Count);
    }
}
