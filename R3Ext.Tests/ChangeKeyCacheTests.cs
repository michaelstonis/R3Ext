using System.Collections.Generic;
using System.Linq;
using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;
#pragma warning disable SA1516, SA1503, SA1513, SA1107, SA1502, SA1515

namespace R3Ext.Tests;

public class ChangeKeyCacheTests
{
    private sealed record Person(int Id, int AltId, string Name);

    [Fact]
    public void ChangeKey_KeyChange_EmitsRemoveAddPair()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var captured = new List<IChangeSet<Person, int>>();
        var sub = cache.Connect().ChangeKey<Person, int, int>(p => p.AltId).Subscribe(captured.Add);

        var p = new Person(1, 10, "A");
        cache.AddOrUpdate(p); // add projected key 10

        // Update with new AltId (key change from 10 -> 20)
        p = p with { AltId = 20 };
        cache.AddOrUpdate(p);

        // Expect last emitted ChangeSet contains Remove(10) + Add(20)
        var last = captured.Last();
        Assert.Contains(last, c => c.Reason == ChangeReason.Remove && c.Key == 10);
        Assert.Contains(last, c => c.Reason == ChangeReason.Add && c.Key == 20);
        Assert.Equal(2, last.Count);

        // Remove upstream key -> projected remove for 20
        cache.Remove(1);
        var final = captured.Last();
        Assert.Single(final);
        Assert.Contains(final, c => c.Reason == ChangeReason.Remove && c.Key == 20);

        sub.Dispose();
    }
}
