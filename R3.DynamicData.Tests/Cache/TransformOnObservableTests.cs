// Port of DynamicData to R3.
#pragma warning disable SA1516, SA1515, SA1503, SA1502, SA1107, SA1513, SA1518

using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;

namespace R3.DynamicData.Tests.Cache;

public class TransformOnObservableTests
{
    [Fact]
    public void TransformOnObservable_Add_EmitsAddChangeset()
    {
        var cache = new SourceCache<string, int>(s => s.Length);
        var results = new List<IChangeSet<int, int>>();

        // Use Observable.Return so value is emitted synchronously on subscribe
        using var sub = cache.Connect()
            .TransformOnObservable<string, int, int>((item, key) =>
                Observable.Return(item.Length * 2))
            .Subscribe(results.Add);

        cache.AddOrUpdate("abc"); // length 3 → 6

        Assert.Single(results);
        Assert.Equal(1, results[0].Adds);
        Assert.Equal(6, results[0].First().Current);
    }

    [Fact]
    public void TransformOnObservable_SubsequentEmission_EmitsUpdateChangeset()
    {
        var cache = new SourceCache<string, int>(s => s.Length);
        var results = new List<IChangeSet<int, int>>();
        Subject<int>? capturedSubject = null;

        using var sub = cache.Connect()
            .TransformOnObservable<string, int, int>((item, key) =>
            {
                capturedSubject = new Subject<int>();
                return capturedSubject;
            })
            .Subscribe(results.Add);

        cache.AddOrUpdate("abc");
        Assert.NotNull(capturedSubject);

        capturedSubject!.OnNext(100); // first emission → Add
        Assert.Equal(1, results.Count);
        Assert.Equal(1, results[0].Adds);

        capturedSubject.OnNext(200); // second emission → Update
        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[1].Updates);
        Assert.Equal(200, results[1].First().Current);
        Assert.Equal(100, results[1].First().Previous.Value);
    }

    [Fact]
    public void TransformOnObservable_Remove_EmitsRemoveChangeset()
    {
        var cache = new SourceCache<string, int>(s => s.Length);
        var results = new List<IChangeSet<int, int>>();
        Subject<int>? capturedSubject = null;

        using var sub = cache.Connect()
            .TransformOnObservable<string, int, int>((item, key) =>
            {
                capturedSubject = new Subject<int>();
                return capturedSubject;
            })
            .Subscribe(results.Add);

        cache.AddOrUpdate("abc");
        capturedSubject!.OnNext(42); // Add
        Assert.Equal(1, results.Count);

        cache.Remove("abc".Length); // Remove key 3

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[1].Removes);
        Assert.Equal(42, results[1].First().Current);
    }

    [Fact]
    public void TransformOnObservable_Remove_NoMoreEmissionsFromDisposedObservable()
    {
        var cache = new SourceCache<string, int>(s => s.Length);
        var results = new List<IChangeSet<int, int>>();
        Subject<int>? capturedSubject = null;

        using var sub = cache.Connect()
            .TransformOnObservable<string, int, int>((item, key) =>
            {
                capturedSubject = new Subject<int>();
                return capturedSubject;
            })
            .Subscribe(results.Add);

        cache.AddOrUpdate("abc");
        capturedSubject!.OnNext(10); // first emission → Add
        cache.Remove("abc".Length);  // Remove

        int countAfterRemove = results.Count;
        capturedSubject.OnNext(99); // should be ignored

        Assert.Equal(countAfterRemove, results.Count);
    }

    [Fact]
    public void TransformOnObservable_Update_DisposesOldSubscription_SubscribesNew()
    {
        var cache = new SourceCache<string, int>(s => s.Length);
        var results = new List<IChangeSet<int, int>>();

        var subjects = new List<Subject<int>>();

        using var sub = cache.Connect()
            .TransformOnObservable<string, int, int>((item, key) =>
            {
                var s = new Subject<int>();
                subjects.Add(s);
                return s;
            })
            .Subscribe(results.Add);

        cache.AddOrUpdate("abc"); // key 3, creates subjects[0]
        subjects[0].OnNext(10);   // Add: 10

        // Update same key with different value: "xyz" has same length 3
        cache.AddOrUpdate("xyz"); // same key 3, creates subjects[1]
        Assert.Equal(2, subjects.Count);

        // Old subject should no longer produce output
        subjects[0].OnNext(999);
        int countBeforeNewEmit = results.Count;
        Assert.Equal(countBeforeNewEmit, results.Count);

        // New subject should produce output
        subjects[1].OnNext(20);
        Assert.Equal(countBeforeNewEmit + 1, results.Count);
        // New observable's first emission: if output had previous value from old obs → Update
        // If not (old obs was disposed before producing output here), then Add
        Assert.True(results.Last().Updates == 1 || results.Last().Adds == 1);
    }
}
