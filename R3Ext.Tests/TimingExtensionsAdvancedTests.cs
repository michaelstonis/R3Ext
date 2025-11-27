using Microsoft.Extensions.Time.Testing;
using R3;
using R3.Collections;

namespace R3Ext.Tests;

public class TimingExtensionsAdvancedTests
{
    // Conflate Tests
    [Fact]
    public void Conflate_NullSource_ThrowsArgumentNullException()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.Conflate(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Conflate_ZeroPeriod_ThrowsArgumentOutOfRangeException()
    {
        var source = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Conflate(TimeSpan.Zero));
    }

    [Fact]
    public void Conflate_NegativePeriod_ThrowsArgumentOutOfRangeException()
    {
        var source = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Conflate(TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void Conflate_FirstValueEmittedImmediately()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<int> result = subject.Conflate(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1);

        Assert.Single(result);
        Assert.Equal(1, result[0]);
    }

    [Fact]
    public void Conflate_SubsequentValuesWithinWindow_EmitLatestOnly()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<int> result = subject.Conflate(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1); // immediate
        subject.OnNext(2); // within window
        subject.OnNext(3); // within window
        subject.OnNext(4); // within window

        Assert.Single(result);
        Assert.Equal(1, result[0]);

        // Advance past window
        tp.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(2, result.Count);
        Assert.Equal(4, result[1]); // Latest value (4) emitted
    }

    [Fact]
    public void Conflate_ValuesAfterWindow_StartNewWindow()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<int> result = subject.Conflate(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1); // immediate
        subject.OnNext(2); // buffered
        tp.Advance(TimeSpan.FromSeconds(1));

        subject.OnNext(3); // new window - starts gate, doesn't emit yet

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0]);
        Assert.Equal(2, result[1]);

        // Advance again to emit value 3
        tp.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(3, result.Count);
        Assert.Equal(3, result[2]);
    }

    [Fact]
    public void Conflate_NoValuesWithinWindow_NoExtraEmissions()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<int> result = subject.Conflate(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1); // immediate

        // Advance but no pending values
        tp.Advance(TimeSpan.FromSeconds(1));

        Assert.Single(result);
        Assert.Equal(1, result[0]);
    }

    [Fact]
    public void Conflate_MultipleWindows_PreservesLatestInEach()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<int> result = subject.Conflate(TimeSpan.FromMilliseconds(100), tp).ToLiveList();

        // Window 1
        subject.OnNext(1); // immediate
        subject.OnNext(2); // buffered
        subject.OnNext(3); // overwrites buffer
        tp.Advance(TimeSpan.FromMilliseconds(100));

        // Window 2 - first value starts gate but doesn't emit
        subject.OnNext(4); // starts new gate
        subject.OnNext(5); // buffered
        subject.OnNext(6); // overwrites buffer
        tp.Advance(TimeSpan.FromMilliseconds(100));

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { 1, 3, 6 }, result.ToArray());
    }

    // BufferUntilInactive Tests
    [Fact]
    public void BufferUntilInactive_NullSource_ThrowsArgumentNullException()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.BufferUntilInactive(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void BufferUntilInactive_ZeroQuietPeriod_ThrowsArgumentOutOfRangeException()
    {
        var source = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.BufferUntilInactive(TimeSpan.Zero));
    }

    [Fact]
    public void BufferUntilInactive_NegativeQuietPeriod_ThrowsArgumentOutOfRangeException()
    {
        var source = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.BufferUntilInactive(TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void BufferUntilInactive_SingleValue_EmitsAfterQuietPeriod()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<int[]> result = subject.BufferUntilInactive(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1);
        Assert.Empty(result);

        tp.Advance(TimeSpan.FromSeconds(1));

        Assert.Single(result);
        Assert.Equal(new[] { 1 }, result[0]);
    }

    [Fact]
    public void BufferUntilInactive_MultipleValuesQuickly_BufferedTogether()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<int[]> result = subject.BufferUntilInactive(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1);
        tp.Advance(TimeSpan.FromMilliseconds(100));
        subject.OnNext(2);
        tp.Advance(TimeSpan.FromMilliseconds(100));
        subject.OnNext(3);

        Assert.Empty(result);

        tp.Advance(TimeSpan.FromSeconds(1));

        Assert.Single(result);
        Assert.Equal(new[] { 1, 2, 3 }, result[0]);
    }

    [Fact]
    public void BufferUntilInactive_MultipleGroups_EmitsSeparately()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<int[]> result = subject.BufferUntilInactive(TimeSpan.FromSeconds(1), tp).ToLiveList();

        // Group 1
        subject.OnNext(1);
        subject.OnNext(2);
        tp.Advance(TimeSpan.FromSeconds(1));

        // Group 2
        subject.OnNext(3);
        subject.OnNext(4);
        subject.OnNext(5);
        tp.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 1, 2 }, result[0]);
        Assert.Equal(new[] { 3, 4, 5 }, result[1]);
    }

    [Fact]
    public void BufferUntilInactive_NewValueResetsTimer()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<int[]> result = subject.BufferUntilInactive(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1);
        tp.Advance(TimeSpan.FromMilliseconds(900));
        subject.OnNext(2); // resets timer
        tp.Advance(TimeSpan.FromMilliseconds(900));
        subject.OnNext(3); // resets timer

        Assert.Empty(result); // Still buffering

        tp.Advance(TimeSpan.FromSeconds(1));

        Assert.Single(result);
        Assert.Equal(new[] { 1, 2, 3 }, result[0]);
    }

    // DebounceImmediate Additional Tests (beyond existing)
    [Fact]
    public void DebounceImmediate_EmptyStream_CompletesImmediately()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<int> result = subject.DebounceImmediate(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnCompleted();

        Assert.Empty(result);
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void DebounceImmediate_SingleValue_EmittedImmediatelyThenCompletes()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<int> result = subject.DebounceImmediate(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1);
        subject.OnCompleted();

        Assert.Single(result);
        Assert.Equal(1, result[0]);
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void DebounceImmediate_RapidSuccession_FirstImmediateRestDebounced()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<int> result = subject.DebounceImmediate(TimeSpan.FromMilliseconds(100), tp).ToLiveList();

        subject.OnNext(1); // immediate
        subject.OnNext(2);
        subject.OnNext(3);
        subject.OnNext(4);
        subject.OnNext(5);
        tp.Advance(TimeSpan.FromMilliseconds(100));

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0]);
        Assert.Equal(5, result[1]); // Latest debounced value
    }

    // Heartbeat Additional Tests
    [Fact]
    public void Heartbeat_EmitsDataWrappedEvents()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<TimingExtensions.HeartbeatEvent<int>> result = subject.Heartbeat(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(42);

        Assert.Single(result);
        Assert.False(result[0].IsHeartbeat);
        Assert.Equal(42, result[0].Value);
    }

    [Fact]
    public void Heartbeat_EmitsHeartbeatDuringInactivity()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<TimingExtensions.HeartbeatEvent<int>> result = subject.Heartbeat(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1);
        Assert.Single(result);

        tp.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(2, result.Count);
        Assert.True(result[1].IsHeartbeat);
    }

    [Fact]
    public void Heartbeat_MultipleHeartbeats_ContinueDuringInactivity()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<TimingExtensions.HeartbeatEvent<int>> result = subject.Heartbeat(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1);

        tp.Advance(TimeSpan.FromSeconds(1));
        tp.Advance(TimeSpan.FromSeconds(1));
        tp.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(4, result.Count);
        Assert.False(result[0].IsHeartbeat); // data
        Assert.True(result[1].IsHeartbeat);
        Assert.True(result[2].IsHeartbeat);
        Assert.True(result[3].IsHeartbeat);
    }

    [Fact]
    public void Heartbeat_NewValue_StopsHeartbeats()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<TimingExtensions.HeartbeatEvent<int>> result = subject.Heartbeat(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1);

        tp.Advance(TimeSpan.FromSeconds(1));

        subject.OnNext(2); // stops heartbeats

        Assert.Equal(3, result.Count);
        Assert.False(result[0].IsHeartbeat);
        Assert.True(result[1].IsHeartbeat);
        Assert.False(result[2].IsHeartbeat);
        Assert.Equal(2, result[2].Value);
    }

    // DetectStale Tests
    [Fact]
    public void DetectStale_EmitsStaleEventAfterQuietPeriod()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<TimingExtensions.StaleEvent<int>> result = subject.DetectStale(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1);
        Assert.Single(result);
        Assert.False(result[0].IsStale); // Fresh value
        Assert.Equal(1, result[0].Value);

        tp.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(2, result.Count);
        Assert.True(result[1].IsStale); // Stale marker
    }

    [Fact]
    public void DetectStale_MultipleStalePeriods_EmitsOnlyOneStaleMarker()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        LiveList<TimingExtensions.StaleEvent<int>> result = subject.DetectStale(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1);
        Assert.Single(result);
        Assert.False(result[0].IsStale);

        tp.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, result.Count);
        Assert.True(result[1].IsStale);

        // Additional time advances don't emit more stale markers
        tp.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, result.Count); // Still only 2
    }
}
