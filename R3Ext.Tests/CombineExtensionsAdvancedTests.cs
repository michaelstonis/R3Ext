using R3;
using Xunit;

namespace R3Ext.Tests;

public class CombineExtensionsAdvancedTests
{
    // CombineLatestValuesAreAllTrue Tests

    [Fact]
    public async Task CombineLatestValuesAreAllTrue_AllTrueInitially_EmitsTrue()
    {
        var s1 = new Subject<bool>();
        var s2 = new Subject<bool>();
        var s3 = new Subject<bool>();

        Observable<bool> result = new[] { s1, s2, s3 }.CombineLatestValuesAreAllTrue();

        var values = new List<bool>();
        result.Subscribe(x => values.Add(x));

        s1.OnNext(true);
        s2.OnNext(true);
        s3.OnNext(true);

        Assert.Single(values);
        Assert.True(values[0]);
    }

    [Fact]
    public async Task CombineLatestValuesAreAllTrue_OneFalse_EmitsFalse()
    {
        var s1 = new Subject<bool>();
        var s2 = new Subject<bool>();
        var s3 = new Subject<bool>();

        Observable<bool> result = new[] { s1, s2, s3 }.CombineLatestValuesAreAllTrue();

        var values = new List<bool>();
        result.Subscribe(x => values.Add(x));

        s1.OnNext(true);
        s2.OnNext(false);
        s3.OnNext(true);

        Assert.Single(values);
        Assert.False(values[0]);
    }

    [Fact]
    public async Task CombineLatestValuesAreAllTrue_TransitionsFromFalseToTrue()
    {
        var s1 = new Subject<bool>();
        var s2 = new Subject<bool>();

        Observable<bool> result = new[] { s1, s2 }.CombineLatestValuesAreAllTrue();

        var values = new List<bool>();
        result.Subscribe(x => values.Add(x));

        s1.OnNext(true);
        s2.OnNext(false); // false
        s2.OnNext(true);  // true

        Assert.Equal(2, values.Count);
        Assert.False(values[0]);
        Assert.True(values[1]);
    }

    [Fact]
    public async Task CombineLatestValuesAreAllTrue_SingleSource_ReflectsSourceValue()
    {
        var source = new Subject<bool>();

        Observable<bool> result = new[] { source }.CombineLatestValuesAreAllTrue();

        var values = new List<bool>();
        result.Subscribe(x => values.Add(x));

        source.OnNext(true);
        source.OnNext(false);
        source.OnNext(true);

        Assert.Equal(3, values.Count);
        Assert.True(values[0]);
        Assert.False(values[1]);
        Assert.True(values[2]);
    }

    [Fact]
    public void CombineLatestValuesAreAllTrue_NullSources_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IEnumerable<Observable<bool>>)null!).CombineLatestValuesAreAllTrue());
    }

    [Fact]
    public async Task CombineLatestValuesAreAllTrue_LargeNumberOfSources()
    {
        var sources = Enumerable.Range(0, 100)
            .Select(_ => new Subject<bool>())
            .ToArray();

        Observable<bool> result = sources.CombineLatestValuesAreAllTrue();

        var values = new List<bool>();
        result.Subscribe(x => values.Add(x));

        // Set all to true
        foreach (var source in sources)
        {
            source.OnNext(true);
        }

        Assert.Single(values);
        Assert.True(values[0]);

        // Set one to false
        sources[50].OnNext(false);

        Assert.Equal(2, values.Count);
        Assert.False(values[1]);
    }

    // CombineLatestValuesAreAllFalse Tests

    [Fact]
    public async Task CombineLatestValuesAreAllFalse_AllFalseInitially_EmitsTrue()
    {
        var s1 = new Subject<bool>();
        var s2 = new Subject<bool>();
        var s3 = new Subject<bool>();

        Observable<bool> result = new[] { s1, s2, s3 }.CombineLatestValuesAreAllFalse();

        var values = new List<bool>();
        result.Subscribe(x => values.Add(x));

        s1.OnNext(false);
        s2.OnNext(false);
        s3.OnNext(false);

        Assert.Single(values);
        Assert.True(values[0]);
    }

    [Fact]
    public async Task CombineLatestValuesAreAllFalse_OneTrue_EmitsFalse()
    {
        var s1 = new Subject<bool>();
        var s2 = new Subject<bool>();
        var s3 = new Subject<bool>();

        Observable<bool> result = new[] { s1, s2, s3 }.CombineLatestValuesAreAllFalse();

        var values = new List<bool>();
        result.Subscribe(x => values.Add(x));

        s1.OnNext(false);
        s2.OnNext(true);
        s3.OnNext(false);

        Assert.Single(values);
        Assert.False(values[0]);
    }

    [Fact]
    public async Task CombineLatestValuesAreAllFalse_TransitionsFromTrueToFalse()
    {
        var s1 = new Subject<bool>();
        var s2 = new Subject<bool>();

        Observable<bool> result = new[] { s1, s2 }.CombineLatestValuesAreAllFalse();

        var values = new List<bool>();
        result.Subscribe(x => values.Add(x));

        s1.OnNext(false);
        s2.OnNext(true);  // false (not all false)
        s2.OnNext(false); // true (all false)

        Assert.Equal(2, values.Count);
        Assert.False(values[0]);
        Assert.True(values[1]);
    }

    [Fact]
    public async Task CombineLatestValuesAreAllFalse_SingleSource_ReflectsInverseSourceValue()
    {
        var source = new Subject<bool>();

        Observable<bool> result = new[] { source }.CombineLatestValuesAreAllFalse();

        var values = new List<bool>();
        result.Subscribe(x => values.Add(x));

        source.OnNext(false); // all false = true
        source.OnNext(true);  // not all false = false
        source.OnNext(false); // all false = true

        Assert.Equal(3, values.Count);
        Assert.True(values[0]);
        Assert.False(values[1]);
        Assert.True(values[2]);
    }

    [Fact]
    public void CombineLatestValuesAreAllFalse_NullSources_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IEnumerable<Observable<bool>>)null!).CombineLatestValuesAreAllFalse());
    }

    [Fact]
    public async Task CombineLatestValuesAreAllFalse_LargeNumberOfSources()
    {
        var sources = Enumerable.Range(0, 100)
            .Select(_ => new Subject<bool>())
            .ToArray();

        Observable<bool> result = sources.CombineLatestValuesAreAllFalse();

        var values = new List<bool>();
        result.Subscribe(x => values.Add(x));

        // Set all to false
        foreach (var source in sources)
        {
            source.OnNext(false);
        }

        Assert.Single(values);
        Assert.True(values[0]);

        // Set one to true
        sources[50].OnNext(true);

        Assert.Equal(2, values.Count);
        Assert.False(values[1]);
    }
}
