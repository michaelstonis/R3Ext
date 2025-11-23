// Tests for IncludeUpdateWhen cache operator.
#pragma warning disable SA1516, SA1515, SA1503, SA1502, SA1107
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;

namespace R3.DynamicData.Tests.Cache;

public class IncludeUpdateWhenCacheTests
{
    private sealed record Person(int Id, int Age);

    [Fact]
    public void IncludeUpdateWhen_SuppressesNonMatchingUpdates()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var results = new List<IChangeSet<Person, int>>();
        using var sub = cache.Connect()
            .IncludeUpdateWhen<Person, int>((current, previous) => previous is not null && (current.Age - previous.Age) >= 2)
            .Subscribe(results.Add);

        cache.AddOrUpdate(new Person(1, 10)); // Add emitted
        cache.AddOrUpdate(new Person(1, 11)); // Update suppressed (diff 1)
        cache.AddOrUpdate(new Person(1, 13)); // Update emitted (diff 2 from previous 11)

        Assert.Equal(2, results.Count); // Add + qualifying Update
        Assert.Equal(ChangeReason.Add, results[0].First().Reason);
        var update = results[1].First();
        Assert.Equal(ChangeReason.Update, update.Reason);
        Assert.Equal(13, update.Current.Age);
        Assert.True(update.Previous.HasValue);
        Assert.Equal(11, update.Previous.Value.Age);
    }

    [Fact]
    public void IncludeUpdateWhen_AllUpdatesSuppressed_OnlyAddEmitted()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var results = new List<IChangeSet<Person, int>>();
        using var sub = cache.Connect()
            .IncludeUpdateWhen<Person, int>((current, previous) => false)
            .Subscribe(results.Add);

        cache.AddOrUpdate(new Person(1, 5));
        cache.AddOrUpdate(new Person(1, 6));
        cache.AddOrUpdate(new Person(1, 7));

        Assert.Single(results);
        var add = results[0].Single();
        Assert.Equal(ChangeReason.Add, add.Reason);
        Assert.Equal(5, add.Current.Age); // Only initial Add emitted, updates suppressed
    }
}
