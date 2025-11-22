using System.Collections.Generic;
#pragma warning disable SA1516, SA1515, SA1503, SA1502, SA1107
using System.Linq;
using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;

namespace R3Ext.Tests;

public class EnsureUniqueKeysCacheTests
{
    private sealed record Person(string Name, int Age);

    [Fact]
    public void UniqueForAdds_CollapsesToSingleAdd()
    {
        var cache = new SourceCache<Person, string>(p => p.Name);
        var messages = new List<IChangeSet<Person, string>>();
        var sub = cache.Connect().EnsureUniqueKeys().Subscribe(messages.Add);

        cache.Edit(inner =>
        {
            inner.AddOrUpdate(new Person("Me", 20));
            inner.AddOrUpdate(new Person("Me", 21));
            inner.AddOrUpdate(new Person("Me", 22));
        });

        Assert.Single(messages);
        var changeSet = messages[0];
        Assert.Equal(1, changeSet.Count);
        var change = changeSet.First();
        Assert.Equal(ChangeReason.Add, change.Reason);
        Assert.Equal(22, change.Current.Age);
        sub.Dispose();
    }

    [Fact]
    public void AddAndRemove_CancelsOut()
    {
        var cache = new SourceCache<Person, string>(p => p.Name);
        var messages = new List<IChangeSet<Person, string>>();
        var sub = cache.Connect().EnsureUniqueKeys().Subscribe(messages.Add);

        cache.Edit(inner =>
        {
            inner.AddOrUpdate(new Person("Me", 20));
            inner.AddOrUpdate(new Person("Me", 21));
            inner.Remove("Me");
        });

        // Expect no net emission (Add+Remove collapsed). SourceCache will emit nothing.
        Assert.Empty(messages);
        sub.Dispose();
    }

    [Fact]
    public void RefreshAfterAdd_SeparateBatch_EmitsRefresh()
    {
        var cache = new SourceCache<Person, string>(p => p.Name);
        var messages = new List<IChangeSet<Person, string>>();
        var sub = cache.Connect().EnsureUniqueKeys().Subscribe(messages.Add);
        cache.AddOrUpdate(new Person("Me", 20)); // Batch 1
        cache.Edit(inner => inner.Refresh("Me")); // Batch 2

        Assert.Equal(2, messages.Count); // Add then Refresh
        var refresh = messages.Last().First();
        Assert.Equal(ChangeReason.Refresh, refresh.Reason);
        sub.Dispose();
    }

    [Fact]
    public void CompoundRefreshSameBatch_AddThenRefresh_OnlyAdd()
    {
        var cache = new SourceCache<Person, string>(p => p.Name);
        var messages = new List<IChangeSet<Person, string>>();
        var sub = cache.Connect().EnsureUniqueKeys().Subscribe(messages.Add);

        cache.Edit(inner =>
        {
            inner.AddOrUpdate(new Person("Me", 20));
            inner.Refresh("Me");
        });

        Assert.Single(messages);
        var change = messages[0].First();
        Assert.Equal(ChangeReason.Add, change.Reason);
        sub.Dispose();
    }

    [Fact]
    public void CompoundRefreshMultiple_AddUpdateThenRefreshes_UsesAddSemantics()
    {
        var cache = new SourceCache<Person, string>(p => p.Name);
        var messages = new List<IChangeSet<Person, string>>();
        var sub = cache.Connect().EnsureUniqueKeys().Subscribe(messages.Add);

        cache.Edit(inner =>
        {
            inner.AddOrUpdate(new Person("Me", 20));
            inner.AddOrUpdate(new Person("Me", 21)); // update
            inner.Refresh("Me");
            inner.Refresh("Me");
        });

        Assert.Single(messages);
        var ch = messages[0].First();
        // DynamicData treats this as Add (due to batch initial add) after uniqueness enforcement.
        Assert.Equal(ChangeReason.Add, ch.Reason);
        Assert.Equal(21, ch.Current.Age);
        sub.Dispose();
    }

    [Fact]
    public void MultipleRefreshOnlyBatch_EmitsSingleRefresh()
    {
        var cache = new SourceCache<Person, string>(p => p.Name);
        var messages = new List<IChangeSet<Person, string>>();
        var sub = cache.Connect().EnsureUniqueKeys().Subscribe(messages.Add);
        cache.AddOrUpdate(new Person("Me", 20));

        cache.Edit(inner =>
        {
            inner.Refresh("Me");
            inner.Refresh("Me");
            inner.Refresh("Me");
        });

        Assert.Equal(2, messages.Count); // initial add + refresh-only batch
        var last = messages.Last().First();
        Assert.Equal(ChangeReason.Refresh, last.Reason);
        sub.Dispose();
    }
}
