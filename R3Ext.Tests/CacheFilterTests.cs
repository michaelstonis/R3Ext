using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;
using R3.DynamicData.Operators;
#pragma warning disable SA1208
#pragma warning disable SA1516
#pragma warning disable SA1501
#pragma warning disable SA1107
#pragma warning disable SA1503
#pragma warning disable SA1502
#pragma warning disable SA1513
#pragma warning disable SA1515

namespace R3Ext.Tests;

public class CacheFilterTests
{
    private sealed record Person(int Id, int Age);

    [Fact]
    public void Filter_AddUpdateRemove_EmitsCorrectly()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var captured = new List<IChangeSet<Person, int>>();

        var sub = cache.Connect()
            .Filter<Person, int>(p => p.Age >= 18)
            .Subscribe(captured.Add);

        cache.AddOrUpdate(new Person(1, 16)); // ignored
        cache.AddOrUpdate(new Person(2, 21)); // add
        cache.AddOrUpdate(new Person(1, 18)); // add (became adult)
        cache.AddOrUpdate(new Person(2, 25)); // update
        cache.AddOrUpdate(new Person(1, 17)); // remove (fell below)
        cache.Remove(2); // remove

        // Validate last changes correspond to removal of key 2
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Remove && c.Key == 2);

        sub.Dispose();
    }

    [Fact]
    public void Filter_Refresh_TogglesInclusion()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var captured = new List<IChangeSet<Person, int>>();
        var sub = cache.Connect()
            .Filter<Person, int>(p => p.Age >= 18)
            .Subscribe(captured.Add);

        var p = new Person(1, 17);
        cache.AddOrUpdate(p);

        // Become adult via refresh
        p = p with { Age = 18 };
        cache.AddOrUpdate(p); // update -> add

        // Refresh maintaining inclusion
        cache.Edit(u => u.Refresh(1));
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Refresh && c.Key == 1);

        // Drop below via update -> remove
        p = p with { Age = 16 };
        cache.AddOrUpdate(p);
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Remove && c.Key == 1);

        sub.Dispose();
    }
}
