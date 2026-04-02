#pragma warning disable SA1107, SA1124, SA1501, SA1503, SA1515, SA1025, SA1520, SA1513, SA1508, SA1516
using Microsoft.Extensions.Time.Testing;
using R3;
using R3.Collections;

namespace R3Ext.Tests;

public class TimingAdvancedTests
{
    // ── TimeInterval ─────────────────────────────────────────────────────────

    [Fact]
    public void TimeInterval_NullSource_ThrowsArgumentNullException()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.TimeInterval());
    }

    [Fact]
    public void TimeInterval_MeasuresElapsedBetweenEmissions()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<TimeInterval<int>> result = subject.TimeInterval(tp).ToLiveList();

        subject.OnNext(1); // interval = 0
        tp.Advance(TimeSpan.FromSeconds(2));
        subject.OnNext(2); // interval ≈ 2s

        Assert.Equal(2, result.Count);
        Assert.Equal(TimeSpan.Zero, result[0].Interval);
        Assert.True(result[1].Interval >= TimeSpan.FromSeconds(1));
        Assert.Equal(1, result[0].Value);
        Assert.Equal(2, result[1].Value);
    }

    [Fact]
    public void TimeInterval_FirstItem_HasZeroInterval()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<TimeInterval<int>> result = subject.TimeInterval(tp).ToLiveList();

        subject.OnNext(42);

        Assert.Single(result);
        Assert.Equal(TimeSpan.Zero, result[0].Interval);
        Assert.Equal(42, result[0].Value);
    }

    [Fact]
    public void TimeInterval_MultipleItems_MeasuresEachGap()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<TimeInterval<int>> result = subject.TimeInterval(tp).ToLiveList();

        subject.OnNext(1);
        tp.Advance(TimeSpan.FromSeconds(1));
        subject.OnNext(2);
        tp.Advance(TimeSpan.FromSeconds(3));
        subject.OnNext(3);

        Assert.Equal(3, result.Count);
        Assert.Equal(TimeSpan.Zero, result[0].Interval);
        Assert.Equal(TimeSpan.FromSeconds(1), result[1].Interval);
        Assert.Equal(TimeSpan.FromSeconds(3), result[2].Interval);
    }

    [Fact]
    public void TimeInterval_Deconstruct_WorksCorrectly()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<TimeInterval<int>> result = subject.TimeInterval(tp).ToLiveList();

        subject.OnNext(99);

        var (value, interval) = result[0];
        Assert.Equal(99, value);
        Assert.Equal(TimeSpan.Zero, interval);
    }

    [Fact]
    public void TimeInterval_Completion_PropagatesDownstream()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<TimeInterval<int>> result = subject.TimeInterval(tp).ToLiveList();

        subject.OnNext(1);
        subject.OnCompleted();

        Assert.True(result.IsCompleted);
        Assert.Single(result);
    }

    // ── DelayWhen ─────────────────────────────────────────────────────────────

    [Fact]
    public void DelayWhen_NullSource_ThrowsArgumentNullException()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.DelayWhen(_ => Observable.Return(Unit.Default)));
    }

    [Fact]
    public void DelayWhen_NullSelector_ThrowsArgumentNullException()
    {
        var source = Observable.Return(1);
        Assert.Throws<ArgumentNullException>(() => source.DelayWhen(null!));
    }

    [Fact]
    public void DelayWhen_EmitsAfterDurationObservable()
    {
        Subject<int> subject = new();
        Subject<Unit> trigger = new();
        LiveList<int> result = subject.DelayWhen(_ => trigger).ToLiveList();

        subject.OnNext(42);
        Assert.Empty(result.ToArray()); // not emitted yet

        trigger.OnNext(Unit.Default);
        Assert.Equal(new[] { 42 }, result.ToArray()); // now emitted
    }

    [Fact]
    public void DelayWhen_MultipleItems_EachDelayedIndependently()
    {
        Subject<int> subject = new();
        Subject<Unit> trigger1 = new();
        Subject<Unit> trigger2 = new();
        int callCount = 0;
        Subject<Unit>[] triggers = [trigger1, trigger2];

        LiveList<int> result = subject.DelayWhen(_ => triggers[callCount++]).ToLiveList();

        subject.OnNext(1);
        subject.OnNext(2);

        Assert.Empty(result.ToArray());

        trigger2.OnNext(Unit.Default); // item 2 fires first
        Assert.Equal(new[] { 2 }, result.ToArray());

        trigger1.OnNext(Unit.Default); // item 1 fires second
        Assert.Equal(new[] { 2, 1 }, result.ToArray());
    }

    [Fact]
    public void DelayWhen_DurationCompletesWithoutEmitting_SkipsItem()
    {
        Subject<int> subject = new();
        Subject<Unit> trigger = new();
        LiveList<int> result = subject.DelayWhen(_ => trigger).ToLiveList();

        subject.OnNext(42);
        trigger.OnCompleted(); // completes without emitting

        Assert.Empty(result.ToArray()); // item was not emitted
    }

    [Fact]
    public void DelayWhen_SourceCompletion_WaitsForInFlightItems()
    {
        Subject<int> subject = new();
        Subject<Unit> trigger = new();
        LiveList<int> result = subject.DelayWhen(_ => trigger).ToLiveList();

        subject.OnNext(1);
        subject.OnCompleted();

        Assert.False(result.IsCompleted); // still waiting for trigger

        trigger.OnNext(Unit.Default);
        Assert.Equal(new[] { 1 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void DelayWhen_WithSubscriptionDelay_NullDelay_Throws()
    {
        var source = Observable.Return(1);
        Assert.Throws<ArgumentNullException>(() =>
            source.DelayWhen(_ => Observable.Return(Unit.Default), null!));
    }

    [Fact]
    public void DelayWhen_WithSubscriptionDelay_DelaysSubscription()
    {
        Subject<int> source = new();
        Subject<Unit> subscriptionTrigger = new();
        Subject<Unit> itemTrigger = new();
        LiveList<int> result = source.DelayWhen(_ => itemTrigger, subscriptionTrigger).ToLiveList();

        // Items emitted before the subscription delay fires are lost.
        source.OnNext(1);
        Assert.Empty(result.ToArray());

        subscriptionTrigger.OnNext(Unit.Default); // subscription starts now
        source.OnNext(2);                         // this one is captured
        itemTrigger.OnNext(Unit.Default);

        Assert.Equal(new[] { 2 }, result.ToArray());
    }

    // ── RateLimit ─────────────────────────────────────────────────────────────

    [Fact]
    public void RateLimit_NullSource_ThrowsArgumentNullException()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.RateLimit(2, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void RateLimit_ZeroCount_ThrowsArgumentOutOfRangeException()
    {
        var source = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.RateLimit(0, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void RateLimit_NegativePeriod_ThrowsArgumentOutOfRangeException()
    {
        var source = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.RateLimit(2, TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void RateLimit_AllowsUpToCountPerPeriod()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<int> result = subject.RateLimit(2, TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1); subject.OnNext(2); subject.OnNext(3); // 3rd queued
        Assert.Equal(new[] { 1, 2 }, result.ToArray());

        tp.Advance(TimeSpan.FromSeconds(1)); // next window
        Assert.Equal(new[] { 1, 2, 3 }, result.ToArray());
    }

    [Fact]
    public void RateLimit_UnderLimit_EmitsAllImmediately()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<int> result = subject.RateLimit(5, TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1); subject.OnNext(2); subject.OnNext(3);

        Assert.Equal(new[] { 1, 2, 3 }, result.ToArray());
    }

    [Fact]
    public void RateLimit_MultipleWindows_DrainsQueueAcrossWindows()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<int> result = subject.RateLimit(2, TimeSpan.FromSeconds(1), tp).ToLiveList();

        // Flood 6 items; 2 per window
        for (int i = 1; i <= 6; i++)
        {
            subject.OnNext(i);
        }

        Assert.Equal(new[] { 1, 2 }, result.ToArray());

        tp.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(new[] { 1, 2, 3, 4 }, result.ToArray());

        tp.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, result.ToArray());
    }

    [Fact]
    public void RateLimit_SourceCompletion_FlushesRemainingQueue()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<int> result = subject.RateLimit(1, TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1); subject.OnNext(2); subject.OnNext(3);
        subject.OnCompleted();

        // All items flushed on completion regardless of window
        Assert.Equal(new[] { 1, 2, 3 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    // ── BufferWithOverflow ───────────────────────────────────────────────────

    [Fact]
    public void BufferWithOverflow_NullSource_ThrowsArgumentNullException()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.BufferWithOverflow(2));
    }

    [Fact]
    public void BufferWithOverflow_ZeroCapacity_ThrowsArgumentOutOfRangeException()
    {
        var source = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.BufferWithOverflow(0));
    }

    [Fact]
    public void BufferWithOverflow_DropOldest_RemovesOldItem()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.BufferWithOverflow(2, OverflowStrategy.DropOldest).ToLiveList();

        subject.OnNext(1); subject.OnNext(2); subject.OnNext(3);

        Assert.Contains(2, result.ToArray());
        Assert.Contains(3, result.ToArray());
    }

    [Fact]
    public void BufferWithOverflow_DropLatest_IgnoresIncomingItemWhenFull()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.BufferWithOverflow(2, OverflowStrategy.DropLatest).ToLiveList();

        subject.OnNext(1); subject.OnNext(2); subject.OnNext(3); // 3 is dropped

        Assert.Equal(new[] { 1, 2 }, result.ToArray());
        Assert.DoesNotContain(3, result.ToArray());
    }

    [Fact]
    public void BufferWithOverflow_Error_SignalsErrorOnOverflow()
    {
        Subject<int> subject = new();
        List<Exception> errors = new();
        LiveList<int> result = subject
            .BufferWithOverflow(2, OverflowStrategy.Error)
            .Do(onErrorResume: ex => errors.Add(ex))
            .ToLiveList();

        subject.OnNext(1); subject.OnNext(2); subject.OnNext(3); // 3 triggers error

        Assert.Single(errors);
        Assert.IsType<InvalidOperationException>(errors[0]);
        Assert.Equal(new[] { 1, 2 }, result.ToArray());
    }

    [Fact]
    public void BufferWithOverflow_UnderCapacity_PassesAllItemsThrough()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.BufferWithOverflow(10, OverflowStrategy.DropOldest).ToLiveList();

        subject.OnNext(1); subject.OnNext(2); subject.OnNext(3);

        Assert.Equal(new[] { 1, 2, 3 }, result.ToArray());
    }

    [Fact]
    public void BufferWithOverflow_Completion_PropagatesDownstream()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.BufferWithOverflow(5).ToLiveList();

        subject.OnNext(1);
        subject.OnCompleted();

        Assert.True(result.IsCompleted);
        Assert.Single(result);
    }

    // ── Chunked ───────────────────────────────────────────────────────────────

    [Fact]
    public void Chunked_NullSource_ThrowsArgumentNullException()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.Chunked(3));
    }

    [Fact]
    public void Chunked_ZeroSize_ThrowsArgumentOutOfRangeException()
    {
        var source = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Chunked(0));
    }

    [Fact]
    public void Chunked_NonOverlapping_EmitsCorrectChunks()
    {
        Subject<int> subject = new();
        LiveList<int[]> result = subject.Chunked(3).ToLiveList();

        subject.OnNext(1); subject.OnNext(2); subject.OnNext(3);
        subject.OnNext(4); subject.OnNext(5); subject.OnNext(6);
        subject.OnCompleted();

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 1, 2, 3 }, result[0]);
        Assert.Equal(new[] { 4, 5, 6 }, result[1]);
    }

    [Fact]
    public void Chunked_PartialChunk_NotEmittedOnCompletion()
    {
        Subject<int> subject = new();
        LiveList<int[]> result = subject.Chunked(3).ToLiveList();

        subject.OnNext(1); subject.OnNext(2); // only 2, never fills the chunk
        subject.OnCompleted();

        Assert.Empty(result);
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void Chunked_OverlappingStep_EmitsCorrectWindows()
    {
        Subject<int> subject = new();
        LiveList<int[]> result = subject.Chunked(size: 3, step: 1).ToLiveList();

        subject.OnNext(1); subject.OnNext(2); subject.OnNext(3);
        subject.OnNext(4);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 1, 2, 3 }, result[0]);
        Assert.Equal(new[] { 2, 3, 4 }, result[1]);
    }

    [Fact]
    public void Chunked_StepLargerThanSize_LeavesGapsBetweenChunks()
    {
        Subject<int> subject = new();
        LiveList<int[]> result = subject.Chunked(size: 2, step: 3).ToLiveList();

        // items:  1  2  3  4  5  6
        // chunk1: ^  ^           (starts at count=0, size=2)
        // chunk2:          ^  ^  (starts at count=3, size=2)
        subject.OnNext(1); subject.OnNext(2);
        subject.OnNext(3);
        subject.OnNext(4); subject.OnNext(5);
        subject.OnNext(6);
        subject.OnCompleted();

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 1, 2 }, result[0]);
        Assert.Equal(new[] { 4, 5 }, result[1]);
    }

    [Fact]
    public void Chunked_SingleItemChunks_EmitsEachItem()
    {
        Subject<int> subject = new();
        LiveList<int[]> result = subject.Chunked(size: 1).ToLiveList();

        subject.OnNext(10); subject.OnNext(20); subject.OnNext(30);

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { 10 }, result[0]);
        Assert.Equal(new[] { 20 }, result[1]);
        Assert.Equal(new[] { 30 }, result[2]);
    }
}
