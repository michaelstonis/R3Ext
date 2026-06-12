#pragma warning disable SA1107, SA1124, SA1501, SA1503, SA1515, SA1025, SA1520, SA1513, SA1508, SA1516
using System;
using System.Collections.Generic;
using R3;
using R3.Collections;
using Xunit;

#pragma warning disable SA1503, SA1513, SA1515, SA1107, SA1502, SA1508, SA1516

namespace R3Ext.Tests;

public class CombinationExtensionsTests
{
    #region ForkJoin

    [Fact]
    public void ForkJoin_EmitsLastValuesWhenAllComplete()
    {
        Subject<int> s1 = new(), s2 = new(), s3 = new();
        LiveList<(int, int, int)> result = CombinationExtensions.ForkJoin(s1, s2, s3).ToLiveList();

        s1.OnNext(1); s1.OnNext(2); s1.OnCompleted();
        s2.OnNext(10); s2.OnCompleted();
        Assert.Empty(result.ToArray());

        s3.OnNext(100); s3.OnCompleted();
        Assert.Single(result.ToArray());
        Assert.Equal((2, 10, 100), result.ToArray()[0]);
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void ForkJoin_EmptySourcesReturnsEmptyArray()
    {
        LiveList<int[]> result = CombinationExtensions.ForkJoin<int>().ToLiveList();
        Assert.Single(result.ToArray());
        Assert.Empty(result.ToArray()[0]);
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void ForkJoin_SingleSourceEmitsLastValue()
    {
        Subject<int> s = new();
        LiveList<int[]> result = CombinationExtensions.ForkJoin(s).ToLiveList();

        s.OnNext(5);
        s.OnNext(99);
        s.OnCompleted();

        Assert.Single(result.ToArray());
        Assert.Equal(new[] { 99 }, result.ToArray()[0]);
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void ForkJoin_SourceWithNoValueCompletesWithFailure()
    {
        Subject<int> s1 = new(), s2 = new();
        bool completed = false;

        CombinationExtensions.ForkJoin(s1, s2).Subscribe(
            _ => { },
            _ => { },
            r => { completed = true; Assert.True(r.IsFailure); });

        s1.OnNext(1);
        s1.OnCompleted();
        s2.OnCompleted(); // s2 never emitted

        Assert.True(completed);
    }

    [Fact]
    public void ForkJoin_Typed_TwoSources()
    {
        Subject<int> s1 = new();
        Subject<string> s2 = new();
        LiveList<(int, string)> result = CombinationExtensions.ForkJoin(s1, s2).ToLiveList();

        s1.OnNext(42); s1.OnCompleted();
        s2.OnNext("hello"); s2.OnCompleted();

        Assert.Single(result.ToArray());
        Assert.Equal((42, "hello"), result.ToArray()[0]);
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void ForkJoin_Typed_ThreeSources()
    {
        Subject<int> s1 = new();
        Subject<string> s2 = new();
        Subject<bool> s3 = new();
        LiveList<(int, string, bool)> result = CombinationExtensions.ForkJoin(s1, s2, s3).ToLiveList();

        s1.OnNext(1); s1.OnCompleted();
        s2.OnNext("x"); s2.OnCompleted();
        s3.OnNext(true); s3.OnCompleted();

        Assert.Single(result.ToArray());
        Assert.Equal((1, "x", true), result.ToArray()[0]);
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void ForkJoin_NullSourcesThrows()
    {
        Assert.Throws<ArgumentNullException>(() => CombinationExtensions.ForkJoin<int>((IEnumerable<Observable<int>>)null!));
    }

    #endregion

    #region Generate

    [Fact]
    public void Generate_ProducesSequence()
    {
        LiveList<int> result = CombinationExtensions
            .Generate(0, x => x < 5, x => x + 1, x => x * 2)
            .ToLiveList();

        Assert.Equal(new[] { 0, 2, 4, 6, 8 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void Generate_EmptyWhenConditionFalseFromStart()
    {
        LiveList<int> result = CombinationExtensions
            .Generate(10, x => x < 5, x => x + 1, x => x)
            .ToLiveList();

        Assert.Empty(result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void Generate_SingleItem()
    {
        LiveList<int> result = CombinationExtensions
            .Generate(0, x => x < 1, x => x + 1, x => x + 100)
            .ToLiveList();

        Assert.Equal(new[] { 100 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void Generate_StateOverload_ProducesSequence()
    {
        LiveList<int> result = CombinationExtensions
            .Generate(1, x => x <= 5, x => x + 1)
            .ToLiveList();

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void Generate_NullConditionThrows()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CombinationExtensions.Generate(0, null!, x => x + 1, x => x));
    }

    #endregion

    #region Iif / Condition

    [Fact]
    public void Iif_SelectsBasedOnCondition()
    {
        bool flag = true;
        LiveList<int> result = CombinationExtensions
            .Iif(() => flag, Observable.Return(1), Observable.Return(2))
            .ToLiveList();
        Assert.Equal(new[] { 1 }, result.ToArray());

        flag = false;
        LiveList<int> result2 = CombinationExtensions
            .Iif(() => flag, Observable.Return(1), Observable.Return(2))
            .ToLiveList();
        Assert.Equal(new[] { 2 }, result2.ToArray());
    }

    [Fact]
    public void Iif_EvaluatesConditionAtSubscribeTime()
    {
        bool flag = true;
        Observable<int> obs = CombinationExtensions.Iif(() => flag, Observable.Return(10), Observable.Return(20));

        flag = false;
        LiveList<int> result = obs.ToLiveList();

        // Condition evaluated at subscribe time (when ToLiveList subscribes), flag is false
        Assert.Equal(new[] { 20 }, result.ToArray());
    }

    [Fact]
    public void Iif_NullConditionThrows()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CombinationExtensions.Iif<int>(null!, Observable.Return(1), Observable.Return(2)));
    }

    [Fact]
    public void Condition_IsAliasForIif()
    {
        bool flag = true;
        LiveList<int> result = CombinationExtensions
            .Condition(() => flag, Observable.Return(42), Observable.Return(0))
            .ToLiveList();
        Assert.Equal(new[] { 42 }, result.ToArray());
    }

    #endregion

    #region SequenceEqual

    [Fact]
    public void SequenceEqual_TrueForEqualSequences()
    {
        Subject<int> s1 = new(), s2 = new();
        LiveList<bool> result = s1.SequenceEqual(s2).ToLiveList();

        s1.OnNext(1); s2.OnNext(1);
        s1.OnNext(2); s2.OnNext(2);
        s1.OnCompleted(); s2.OnCompleted();

        Assert.Equal(new[] { true }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void SequenceEqual_FalseOnMismatch()
    {
        Subject<int> s1 = new(), s2 = new();
        LiveList<bool> result = s1.SequenceEqual(s2).ToLiveList();

        s1.OnNext(1); s2.OnNext(99);

        Assert.Equal(new[] { false }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void SequenceEqual_FalseOnDifferentLengths()
    {
        Subject<int> s1 = new(), s2 = new();
        LiveList<bool> result = s1.SequenceEqual(s2).ToLiveList();

        s1.OnNext(1);
        s1.OnCompleted();
        s2.OnNext(1);
        s2.OnNext(2);
        s2.OnCompleted();

        Assert.Equal(new[] { false }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void SequenceEqual_TrueForBothEmpty()
    {
        Subject<int> s1 = new(), s2 = new();
        LiveList<bool> result = s1.SequenceEqual(s2).ToLiveList();

        s1.OnCompleted();
        s2.OnCompleted();

        Assert.Equal(new[] { true }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void SequenceEqual_UsesCustomComparer()
    {
        Subject<string> s1 = new(), s2 = new();
        LiveList<bool> result = s1.SequenceEqual(s2, StringComparer.OrdinalIgnoreCase).ToLiveList();

        s1.OnNext("Hello"); s2.OnNext("hello");
        s1.OnCompleted(); s2.OnCompleted();

        Assert.Equal(new[] { true }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void SequenceEqual_NullSourceThrows()
    {
        Observable<int> nullSource = null!;
        Assert.Throws<ArgumentNullException>(() => nullSource.SequenceEqual(Observable.Return(1)));
    }

    #endregion

    #region OnErrorResumeNext

    [Fact]
    public void OnErrorResumeNext_ContinuesAfterError()
    {
        Subject<int> s1 = new(), s2 = new();
        LiveList<int> result = s1.OnErrorResumeNext(s2).ToLiveList();

        s1.OnNext(1);
        s1.OnErrorResume(new Exception());
        s2.OnNext(2);
        s2.OnCompleted();

        Assert.Equal(new[] { 1, 2 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void OnErrorResumeNext_ContinuesAfterSuccess()
    {
        Subject<int> s1 = new(), s2 = new();
        LiveList<int> result = s1.OnErrorResumeNext(s2).ToLiveList();

        s1.OnNext(10);
        s1.OnCompleted();
        s2.OnNext(20);
        s2.OnCompleted();

        Assert.Equal(new[] { 10, 20 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void OnErrorResumeNext_MultipleSources()
    {
        Subject<int> s1 = new(), s2 = new(), s3 = new();
        LiveList<int> result = CombinationExtensions.OnErrorResumeNext(s1, s2, s3).ToLiveList();

        s1.OnNext(1); s1.OnCompleted();
        s2.OnNext(2); s2.OnErrorResume(new Exception());
        s3.OnNext(3); s3.OnCompleted();

        Assert.Equal(new[] { 1, 2, 3 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void OnErrorResumeNext_EmptySourcesCompletes()
    {
        LiveList<int> result = CombinationExtensions.OnErrorResumeNext<int>().ToLiveList();
        Assert.Empty(result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void OnErrorResumeNext_NullSourceThrows()
    {
        Observable<int> nullSource = null!;
        Assert.Throws<ArgumentNullException>(() => nullSource.OnErrorResumeNext(Observable.Return(1)));
    }

    #endregion

    #region RepeatWhen

    [Fact]
    public void RepeatWhen_RepeatsWhenNotifierEmits()
    {
        int subscribeCount = 0;
        Subject<Unit> trigger = new();

        Observable<int> source = Observable.Create<int>(observer =>
        {
            subscribeCount++;
            observer.OnNext(subscribeCount);
            observer.OnCompleted();
            return Disposable.Empty;
        });

        LiveList<int> result = source.RepeatWhen(_ => trigger).ToLiveList();

        Assert.Equal(new[] { 1 }, result.ToArray());
        trigger.OnNext(Unit.Default);
        Assert.Equal(new[] { 1, 2 }, result.ToArray());
        trigger.OnNext(Unit.Default);
        Assert.Equal(new[] { 1, 2, 3 }, result.ToArray());
    }

    [Fact]
    public void RepeatWhen_StopsWhenHandlerCompletes()
    {
        Subject<Unit> trigger = new();
        int subscribeCount = 0;

        Observable<int> source = Observable.Create<int>(observer =>
        {
            subscribeCount++;
            observer.OnNext(subscribeCount);
            observer.OnCompleted();
            return Disposable.Empty;
        });

        LiveList<int> result = source.RepeatWhen(_ => trigger).ToLiveList();

        trigger.OnNext(Unit.Default);
        trigger.OnCompleted();

        Assert.True(result.IsCompleted);
        Assert.Equal(new[] { 1, 2 }, result.ToArray());
    }

    [Fact]
    public void RepeatWhen_NullSourceThrows()
    {
        Observable<int> nullSource = null!;
        Assert.Throws<ArgumentNullException>(() => nullSource.RepeatWhen(_ => Observable.Return(Unit.Default)));
    }

    [Fact]
    public void RepeatWhen_NullHandlerThrows()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Observable.Return(1).RepeatWhen(null!));
    }

    #endregion
}
