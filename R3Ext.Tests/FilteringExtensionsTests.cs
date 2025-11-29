using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using R3;
using Xunit;

namespace R3Ext.Tests;

/// <summary>
/// Tests for FilteringExtensions operators.
/// </summary>
[Collection("FrameProvider")]
public class FilteringExtensionsTests(FrameProviderFixture fp)
{
    private readonly FrameProviderFixture _frameProvider = fp;

    [Fact]
    public void Not_InvertsBooleanValues()
    {
        // Test: NOT operator inverts boolean stream
        List<bool> results = new();
        Subject<bool> source = new();

        using var sub = source.Not().Subscribe(results.Add);

        source.OnNext(true);
        source.OnNext(false);
        source.OnNext(true);

        Assert.Equal(3, results.Count);
        Assert.False(results[0]);
        Assert.True(results[1]);
        Assert.False(results[2]);
    }

    [Fact]
    public void Not_ThrowsOnNullSource()
    {
        // Test: Null source throws ArgumentNullException
        Observable<bool>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.Not());
    }

    [Fact]
    public void WhereTrue_FiltersOnlyTrueValues()
    {
        // Test: WhereTrue emits only true values
        List<bool> results = new();
        Subject<bool> source = new();

        using var sub = source.WhereTrue().Subscribe(results.Add);

        source.OnNext(true);
        source.OnNext(false);
        source.OnNext(true);
        source.OnNext(false);
        source.OnNext(true);

        Assert.Equal(3, results.Count);
        Assert.All(results, b => Assert.True(b));
    }

    [Fact]
    public void WhereTrue_ThrowsOnNullSource()
    {
        // Test: Null source throws ArgumentNullException
        Observable<bool>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.WhereTrue());
    }

    [Fact]
    public void WhereFalse_FiltersOnlyFalseValues()
    {
        // Test: WhereFalse emits only false values
        List<bool> results = new();
        Subject<bool> source = new();

        using var sub = source.WhereFalse().Subscribe(results.Add);

        source.OnNext(true);
        source.OnNext(false);
        source.OnNext(true);
        source.OnNext(false);
        source.OnNext(true);

        Assert.Equal(2, results.Count);
        Assert.All(results, b => Assert.False(b));
    }

    [Fact]
    public void WhereFalse_ThrowsOnNullSource()
    {
        // Test: Null source throws ArgumentNullException
        Observable<bool>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.WhereFalse());
    }

    [Fact]
    public void WhereIsNotNull_ReferenceType_FiltersNulls()
    {
        // Test: WhereIsNotNull filters null reference types
        List<string> results = new();
        Subject<string?> source = new();

        using var sub = source.WhereIsNotNull().Subscribe(results.Add);

        source.OnNext("Hello");
        source.OnNext(null);
        source.OnNext("World");
        source.OnNext(null);
        source.OnNext("!");

        Assert.Equal(3, results.Count);
        Assert.Equal("Hello", results[0]);
        Assert.Equal("World", results[1]);
        Assert.Equal("!", results[2]);
    }

    [Fact]
    public void WhereIsNotNull_ReferenceType_ThrowsOnNullSource()
    {
        // Test: Null source throws ArgumentNullException
        Observable<string?>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.WhereIsNotNull());
    }

    [Fact]
    public void WhereIsNotNull_ValueType_FiltersNulls()
    {
        // Test: WhereIsNotNull filters null value types
        List<int> results = new();
        Subject<int?> source = new();

        using var sub = source.WhereIsNotNull().Subscribe(results.Add);

        source.OnNext(1);
        source.OnNext(null);
        source.OnNext(2);
        source.OnNext(null);
        source.OnNext(3);

        Assert.Equal(3, results.Count);
        Assert.Equal(1, results[0]);
        Assert.Equal(2, results[1]);
        Assert.Equal(3, results[2]);
    }

    [Fact]
    public void WhereIsNotNull_ValueType_ThrowsOnNullSource()
    {
        // Test: Null source throws ArgumentNullException
        Observable<int?>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.WhereIsNotNull());
    }

    [Fact]
    public void WaitUntil_EmitsFirstMatchingValue()
    {
        // Test: WaitUntil emits first matching value then completes
        List<int> results = new();
        Subject<int> source = new();

        using var sub = source.WaitUntil(x => x > 5).Subscribe(results.Add);

        source.OnNext(1);
        source.OnNext(3);
        source.OnNext(7); // Matches, should complete
        source.OnNext(9); // Should not emit (completed already)

        Assert.Single(results);
        Assert.Equal(7, results[0]);
    }

    [Fact]
    public void WaitUntil_ThrowsOnNullSource()
    {
        // Test: Null source throws ArgumentNullException
        Observable<int>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.WaitUntil(x => x > 5));
    }

    [Fact]
    public void WaitUntil_ThrowsOnNullPredicate()
    {
        // Test: Null predicate throws ArgumentNullException
        Subject<int> source = new();
        Assert.Throws<ArgumentNullException>(() => source.WaitUntil(null!));
    }

    [Fact]
    public void TakeUntil_EmitsUntilPredicateMatches()
    {
        // Test: TakeUntil emits values until predicate matches, including matching value
        List<int> results = new();
        Subject<int> source = new();

        using var sub = source.TakeUntil(x => x >= 5).Subscribe(results.Add);

        source.OnNext(1);
        source.OnNext(2);
        source.OnNext(5); // Matches, emit then complete
        source.OnNext(6); // Should not emit (completed already)

        Assert.Equal(3, results.Count);
        Assert.Equal(new[] { 1, 2, 5 }, results);
    }

    [Fact]
    public void TakeUntil_ThrowsOnNullSource()
    {
        // Test: Null source throws ArgumentNullException
        Observable<int>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.TakeUntil(x => x > 5));
    }

    [Fact]
    public void TakeUntil_ThrowsOnNullPredicate()
    {
        // Test: Null predicate throws ArgumentNullException
        Subject<int> source = new();
        Assert.Throws<ArgumentNullException>(() => source.TakeUntil(null!));
    }

    [Fact]
    public void Filter_MatchesRegexPattern()
    {
        // Test: Filter emits strings matching regex pattern
        List<string> results = new();
        Subject<string> source = new();

        using var sub = source.Filter(@"^\d+$").Subscribe(results.Add); // Only digits

        source.OnNext("123");
        source.OnNext("abc");
        source.OnNext("456");
        source.OnNext("12a");
        source.OnNext("789");

        Assert.Equal(3, results.Count);
        Assert.Equal("123", results[0]);
        Assert.Equal("456", results[1]);
        Assert.Equal("789", results[2]);
    }

    [Fact]
    public void Filter_IgnoreCaseOption()
    {
        // Test: Filter respects RegexOptions parameter
        List<string> results = new();
        Subject<string> source = new();

        using var sub = source.Filter("hello", RegexOptions.IgnoreCase).Subscribe(results.Add);

        source.OnNext("hello");
        source.OnNext("HELLO");
        source.OnNext("Hello");
        source.OnNext("world");

        Assert.Equal(3, results.Count);
        Assert.Contains("hello", results);
        Assert.Contains("HELLO", results);
        Assert.Contains("Hello", results);
    }

    [Fact]
    public void Filter_FiltersNullStrings()
    {
        // Test: Filter handles null strings gracefully
        List<string> results = new();
        Subject<string> source = new();

        using var sub = source.Filter(@"test").Subscribe(results.Add);

        source.OnNext("test");
        source.OnNext(null!);
        source.OnNext("testing");

        Assert.Equal(2, results.Count);
        Assert.Equal("test", results[0]);
        Assert.Equal("testing", results[1]);
    }

    [Fact]
    public void Filter_ThrowsOnNullSource()
    {
        // Test: Null source throws ArgumentNullException
        Observable<string>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.Filter("test"));
    }

    [Fact]
    public void Filter_ThrowsOnNullPattern()
    {
        // Test: Null pattern throws ArgumentNullException
        Subject<string> source = new();
        Assert.Throws<ArgumentNullException>(() => source.Filter(null!));
    }

    [Fact]
    public void While_RepeatsWhileConditionTrue()
    {
        // Test: While repeats sequence while condition is true
        int count = 0;
        List<int> results = new();

        var source = Observable.Return(1).While(() => count++ < 3);

        using var sub = source.Subscribe(results.Add);

        // Should emit: 1, 1, 1 (three times)
        Assert.Equal(3, results.Count);
        Assert.All(results, x => Assert.Equal(1, x));
    }

    [Fact]
    public void While_StopsWhenConditionFalse()
    {
        // Test: While stops when condition evaluates to false
        bool condition = true;
        List<int> results = new();

        var source = Observable.Return(42).While(() =>
        {
            bool current = condition;
            condition = false; // Set to false after first check
            return current;
        });

        using var sub = source.Subscribe(results.Add);

        Assert.Single(results);
        Assert.Equal(42, results[0]);
    }

    [Fact]
    public void While_EmptyWhenConditionInitiallyFalse()
    {
        // Test: While emits nothing if condition is initially false
        List<int> results = new();
        var source = Observable.Return(1).While(() => false);

        using var sub = source.Subscribe(results.Add);

        Assert.Empty(results);
    }

    [Fact]
    public void While_ThrowsOnNullSource()
    {
        // Test: Null source throws ArgumentNullException
        Observable<int>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.While(() => true));
    }

    [Fact]
    public void While_ThrowsOnNullCondition()
    {
        // Test: Null condition throws ArgumentNullException
        var source = Observable.Return(1);
        Assert.Throws<ArgumentNullException>(() => source.While(null!));
    }

    [Fact]
    public void WhereIsNotNull_CompletesCorrectly()
    {
        // Test: WhereIsNotNull propagates completion
        List<string> results = new();
        Subject<string?> source = new();

        using var sub = source.WhereIsNotNull().Subscribe(results.Add);

        source.OnNext("test");
        source.OnNext(null);
        source.OnNext("value");

        Assert.Equal(2, results.Count);
        Assert.Equal("test", results[0]);
        Assert.Equal("value", results[1]);
    }

    [Fact]
    public void TakeUntil_HandlesImmediateMatch()
    {
        // Test: TakeUntil handles immediate predicate match
        List<int> results = new();
        Subject<int> source = new();

        using var sub = source.TakeUntil(x => x == 1).Subscribe(results.Add);

        source.OnNext(1); // Immediate match
        source.OnNext(2); // Should not emit (completed already)

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }
}
