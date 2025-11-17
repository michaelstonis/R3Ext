using System;
using Microsoft.Extensions.Time.Testing;
using System.Linq;
using System.Threading.Tasks;
using R3;
using Xunit;

namespace R3Ext.Tests;

public class LivenessBufferingTests
{
    [Fact]
    public void Heartbeat_EmitsMarkersDuringInactivity()
    {
        var tp = new FakeTimeProvider();
        var subject = new Subject<int>();
        var list = subject.Heartbeat(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(10);
        Assert.False(list.Last().IsHeartbeat);

        tp.Advance(TimeSpan.FromSeconds(1));
        Assert.True(list.Last().IsHeartbeat);

        subject.OnNext(20);
        Assert.False(list.Last().IsHeartbeat);
    }

    [Fact]
    public void DetectStale_EmitsOncePerQuietPeriod()
    {
        var tp = new FakeTimeProvider();
        var subject = new Subject<int>();
        var list = subject.DetectStale(TimeSpan.FromSeconds(1), tp).ToLiveList();

        // Initial inactivity
        tp.Advance(TimeSpan.FromSeconds(1));
        Assert.True(list.Last().IsStale);

        // After new value, stale resets
        subject.OnNext(1);
        Assert.False(list.Last().IsStale);

        tp.Advance(TimeSpan.FromSeconds(1));
        Assert.True(list.Last().IsStale);
    }

    [Fact]
    public void BufferUntilInactive_FlushesOnQuietGap()
    {
        var tp = new FakeTimeProvider();
        var subject = new Subject<int>();
        var list = subject.BufferUntilInactive(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1);
        subject.OnNext(2);
        tp.Advance(TimeSpan.FromMilliseconds(999));
        Assert.Empty(list);
        tp.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(new[] { new[] { 1, 2 } }, list.Select(a => a).ToArray());
    }

    [Fact]
    public void Conflate_EmitsFirstAndLatestWithinWindow()
    {
        var tp = new FakeTimeProvider();
        var subject = new Subject<int>();
        var list = subject.Conflate(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1); // immediate
        subject.OnNext(2);
        subject.OnNext(3);
        tp.Advance(TimeSpan.FromSeconds(1)); // emit latest(3)
        Assert.Equal(new[] { 1, 3 }, list.ToArray());

        // Next value arrives after emission; starts a new window but does not emit until next period
        subject.OnNext(4);
        Assert.Equal(new[] { 1, 3 }, list.ToArray());
        tp.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(new[] { 1, 3, 4 }, list.ToArray());
    }
}
