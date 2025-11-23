// Tests for WatchValue cache operator.
#pragma warning disable SA1516, SA1515, SA1503, SA1502, SA1107
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;

namespace R3.DynamicData.Tests.Cache;

public class WatchValueCacheTests
{
    private sealed record Person(int Id, int Age);

    [Fact]
    public void WatchValue_EmitsOnlyForSpecifiedKey()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var changes = new List<Change<Person, int>>();
        using var sub = cache.Connect().WatchValue<Person, int>(1).Subscribe(changes.Add);

        cache.AddOrUpdate(new Person(1, 10)); // Add
        cache.AddOrUpdate(new Person(2, 20)); // Different key ignored
        cache.AddOrUpdate(new Person(1, 11)); // Update
        cache.Remove(2); // Remove for other key ignored
        cache.Remove(1); // Remove

        Assert.Equal(3, changes.Count); // Add, Update, Remove for key 1
        Assert.Equal(ChangeReason.Add, changes[0].Reason);
        Assert.Equal(ChangeReason.Update, changes[1].Reason);
        Assert.True(changes[1].Previous.HasValue);
        Assert.Equal(10, changes[1].Previous.Value.Age);
        Assert.Equal(ChangeReason.Remove, changes[2].Reason);
    }
}
