using System;
using Microsoft.Extensions.Time.Testing;
using System.Threading.Tasks;
using R3;
using Xunit;

namespace R3Ext.Tests;

public class TimingOperatorsTests
{
    [Fact]
    public async Task DebounceImmediate_FirstImmediate_RestDebounced()
    {
        var tp = new FakeTimeProvider();
        var subject = new Subject<int>();
        var result = subject.DebounceImmediate(TimeSpan.FromSeconds(1), tp).ToLiveList();

        subject.OnNext(1); // immediate
        await Task.Yield();
        Assert.Equal(new[] { 1 }, result.ToArray());

        subject.OnNext(2);
        tp.Advance(TimeSpan.FromMilliseconds(500));
        subject.OnNext(3);
        tp.Advance(TimeSpan.FromMilliseconds(999)); // not yet
        Assert.Equal(new[] { 1 }, result.ToArray());

        tp.Advance(TimeSpan.FromMilliseconds(1)); // debounced emit of 3
        Assert.Equal(new[] { 1, 3 }, result.ToArray());

        subject.OnCompleted();
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void DebounceImmediate_Negative_Throws()
    {
        var src = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => src.DebounceImmediate(TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void Heartbeat_QuietPeriodMustBePositive()
    {
        var src = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => src.Heartbeat(TimeSpan.Zero));
    }

    [Fact]
    public void DetectStale_QuietPeriodMustBePositive()
    {
        var src = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => src.DetectStale(TimeSpan.Zero));
    }

    [Fact]
    public void BufferUntilInactive_QuietPeriodMustBePositive()
    {
        var src = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => src.BufferUntilInactive(TimeSpan.Zero));
    }

    [Fact]
    public void Conflate_PeriodMustBePositive()
    {
        var src = Observable.Return(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => src.Conflate(TimeSpan.Zero));
    }
}
