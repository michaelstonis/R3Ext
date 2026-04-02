#pragma warning disable SA1107, SA1124, SA1501, SA1503, SA1515, SA1025, SA1520, SA1513, SA1508, SA1516, SA1028
using Microsoft.Extensions.Time.Testing;
using R3;
using R3.Collections;

namespace R3Ext.Tests;

public class WindowingOperatorsTests
{
    // -----------------------------------------------------------------------
    //  argument validationWindowCount 
    // -----------------------------------------------------------------------

    [Fact]
    public void WindowCount_NullSource_Throws()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.WindowCount(3));
    }

    [Fact]
    public void WindowCount_ZeroCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Observable.Return(1).WindowCount(0));
    }

    [Fact]
    public void WindowCount_NegativeSkip_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Observable.Return(1).WindowCount(3, -1));
    }

    // -----------------------------------------------------------------------
    //  non-overlappingWindowCount 
    // -----------------------------------------------------------------------

    [Fact]
    public void WindowCount_NonOverlapping_EmitsCorrectWindows()
    {
        Subject<int> subject = new();
        List<int[]> windows = new();
        subject.WindowCount<int>(3).Subscribe(window =>
        {
            List<int> items = new();

            window.Subscribe(items.Add, _ => { }, _ => windows.Add(items.ToArray()));
        });

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        subject.OnNext(4);
        subject.OnNext(5);
        subject.OnNext(6);
        subject.OnCompleted();

        Assert.Equal(2, windows.Count);
        Assert.Equal(new[] { 1, 2, 3 }, windows[0]);
        Assert.Equal(new[] { 4, 5, 6 }, windows[1]);
    }

    [Fact]
    public void WindowCount_NonOverlapping_IncompleteLastWindow_EmittedOnSourceComplete()
    {
        Subject<int> subject = new();
        List<int[]> windows = new();
        subject.WindowCount<int>(3).Subscribe(window =>
        {
            List<int> items = new();

            window.Subscribe(items.Add, _ => { }, _ => windows.Add(items.ToArray()));
        });

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        subject.OnNext(4);
        subject.OnNext(5);
        subject.OnCompleted();

        Assert.Equal(2, windows.Count);
        Assert.Equal(new[] { 1, 2, 3 }, windows[0]);
        Assert.Equal(new[] { 4, 5 }, windows[1]);
    }

    [Fact]
    public void WindowCount_NonOverlapping_SingleElement_EachWindowHasOneItem()
    {
        Subject<int> subject = new();
        List<int[]> windows = new();
        subject.WindowCount<int>(1).Subscribe(window =>
        {
            List<int> items = new();

            window.Subscribe(items.Add, _ => { }, _ => windows.Add(items.ToArray()));
        });

        subject.OnNext(10);
        subject.OnNext(20);
        subject.OnNext(30);
        subject.OnCompleted();

        Assert.Equal(3, windows.Count);
        Assert.Equal(new[] { 10 }, windows[0]);
        Assert.Equal(new[] { 20 }, windows[1]);
        Assert.Equal(new[] { 30 }, windows[2]);
    }

    // -----------------------------------------------------------------------
    //  overlappingWindowCount 
    // -----------------------------------------------------------------------

    [Fact]
    public void WindowCount_Overlapping_WindowsShareElements()
    {
        Subject<int> subject = new();
        List<int[]> windows = new();

        // skip=2, count=3: W0={0,1,2}, W1={2,3,4}
        subject.WindowCount<int>(count: 3, skip: 2).Subscribe(window =>
        {
            List<int> items = new();

            window.Subscribe(items.Add, _ => { }, _ => windows.Add(items.ToArray()));
        });

        subject.OnNext(0);
        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        subject.OnNext(4);
        subject.OnCompleted();

        Assert.True(windows.Count >= 2);
        Assert.Equal(new[] { 0, 1, 2 }, windows[0]);
        Assert.Equal(new[] { 2, 3, 4 }, windows[1]);
    }

    [Fact]
    public void WindowCount_Overlapping_ExplicitSkipEqualsCount_NonOverlapping()
    {
        Subject<int> subject = new();
        List<int[]> windows = new();
        subject.WindowCount<int>(count: 2, skip: 2).Subscribe(window =>
        {
            List<int> items = new();

            window.Subscribe(items.Add, _ => { }, _ => windows.Add(items.ToArray()));
        });

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        subject.OnNext(4);
        subject.OnCompleted();

        Assert.Equal(2, windows.Count);
        Assert.Equal(new[] { 1, 2 }, windows[0]);
        Assert.Equal(new[] { 3, 4 }, windows[1]);
    }

    // -----------------------------------------------------------------------
    //  argument validationWindowTime 
    // -----------------------------------------------------------------------

    [Fact]
    public void WindowTime_NullSource_Throws()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.WindowTime(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void WindowTime_ZeroTimeSpan_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Observable.Return(1).WindowTime(TimeSpan.Zero));
    }

    [Fact]
    public void WindowTime_NegativeTimeSpan_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Observable.Return(1).WindowTime(TimeSpan.FromMilliseconds(-1)));
    }

    // -----------------------------------------------------------------------
    //  behaviourWindowTime 
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WindowTime_CreatesTimeBasedWindows()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        List<int[]> windows = new();
        subject.WindowTime<int>(TimeSpan.FromSeconds(1), tp).Subscribe(window =>
        {
            List<int> items = new();

            window.Subscribe(items.Add, _ => { }, _ => windows.Add(items.ToArray()));
        });

        subject.OnNext(1);
        subject.OnNext(2);
        tp.Advance(TimeSpan.FromSeconds(1));

        subject.OnNext(3);
        tp.Advance(TimeSpan.FromSeconds(1));

        subject.OnCompleted();
        await Task.Yield();

        Assert.True(windows.Count >= 2);
        Assert.Equal(new[] { 1, 2 }, windows[0]);
        Assert.Equal(new[] { 3 }, windows[1]);
    }

    [Fact]
    public async Task WindowTime_EmptyWindowsAreEmitted()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        int windowCount = 0;
        subject.WindowTime<int>(TimeSpan.FromSeconds(1), tp).Subscribe(_ => windowCount++);

        tp.Advance(TimeSpan.FromSeconds(1));
        tp.Advance(TimeSpan.FromSeconds(1));

        subject.OnCompleted();
        await Task.Yield();

        Assert.True(windowCount >= 2, $"Expected at least 2 windows, got {windowCount}");
    }

    [Fact]
    public async Task WindowTime_SourceCompleteFlushesCurrentWindow()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        List<int[]> windows = new();
        subject.WindowTime<int>(TimeSpan.FromSeconds(10), tp).Subscribe(window =>
        {
            List<int> items = new();

            window.Subscribe(items.Add, _ => { }, _ => windows.Add(items.ToArray()));
        });

        subject.OnNext(42);
        subject.OnCompleted();
        await Task.Yield();

        Assert.Single(windows);
        Assert.Equal(new[] { 42 }, windows[0]);
    }

    // -----------------------------------------------------------------------
    // WindowTime with  argument validationmaxCount 
    // -----------------------------------------------------------------------

    [Fact]
    public void WindowTimeMaxCount_ZeroMaxCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Observable.Return(1).WindowTime(TimeSpan.FromSeconds(1), maxCount: 0));
    }

    // -----------------------------------------------------------------------
    // WindowTime with  behaviourmaxCount 
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WindowTimeMaxCount_ClosesOnCountBeforeTimer()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        List<int[]> windows = new();
        subject.WindowTime<int>(TimeSpan.FromSeconds(10), maxCount: 2, timeProvider: tp).Subscribe(window =>
        {
            List<int> items = new();

            window.Subscribe(items.Add, _ => { }, _ => windows.Add(items.ToArray()));
        });

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        subject.OnNext(4);
        subject.OnCompleted();
        await Task.Yield();

        Assert.True(windows.Count >= 2);
        Assert.Equal(new[] { 1, 2 }, windows[0]);
        Assert.Equal(new[] { 3, 4 }, windows[1]);
    }

    [Fact]
    public async Task WindowTimeMaxCount_ClosesOnTimerBeforeCount()
    {
        FakeTimeProvider tp = new();
        Subject<int> subject = new();
        List<int[]> windows = new();
        subject.WindowTime<int>(TimeSpan.FromSeconds(1), maxCount: 10, timeProvider: tp).Subscribe(window =>
        {
            List<int> items = new();

            window.Subscribe(items.Add, _ => { }, _ => windows.Add(items.ToArray()));
        });

        subject.OnNext(1);
        subject.OnNext(2);
        tp.Advance(TimeSpan.FromSeconds(1));

        subject.OnCompleted();
        await Task.Yield();

        Assert.True(windows.Count >= 1);
        Assert.Equal(new[] { 1, 2 }, windows[0]);
    }

    // -----------------------------------------------------------------------
    //  argument validationBufferToggle 
    // -----------------------------------------------------------------------

    [Fact]
    public void BufferToggle_NullSource_Throws()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() =>
            source!.BufferToggle<int, Unit, Unit>(Observable.Empty<Unit>(), _ => Observable.Empty<Unit>()));
    }

    [Fact]
    public void BufferToggle_NullOpenings_Throws()
    {
        Observable<Unit>? nullOpenings = null;
        Assert.Throws<ArgumentNullException>(() =>
            Observable.Return(1).BufferToggle<int, Unit, Unit>(nullOpenings!, _ => Observable.Empty<Unit>()));
    }

    [Fact]
    public void BufferToggle_NullClosingSelector_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Observable.Return(1).BufferToggle<int, Unit, Unit>(
                Observable.Empty<Unit>(),
                (Func<Unit, Observable<Unit>>)null!));
    }

    // -----------------------------------------------------------------------
    //  behaviourBufferToggle 
    // -----------------------------------------------------------------------

    [Fact]
    public void BufferToggle_CollectsItemsInOpenWindows()
    {
        Subject<int> source = new();
        Subject<Unit> opens = new();
        Subject<Unit> closes = new();
        LiveList<int[]> result = source.BufferToggle(opens, _ => (Observable<Unit>)closes).ToLiveList();

        opens.OnNext(Unit.Default);
        source.OnNext(1);
        source.OnNext(2);
        closes.OnNext(Unit.Default);

        Assert.Single(result);
        Assert.Equal(new[] { 1, 2 }, result[0]);
    }

    [Fact]
    public void BufferToggle_ItemsBeforeOpenAreNotCollected()
    {
        Subject<int> source = new();
        Subject<Unit> opens = new();
        Subject<Unit> closes = new();
        LiveList<int[]> result = source.BufferToggle(opens, _ => (Observable<Unit>)closes).ToLiveList();

        source.OnNext(99);
        opens.OnNext(Unit.Default);
        source.OnNext(1);
        closes.OnNext(Unit.Default);

        Assert.Single(result);
        Assert.Equal(new[] { 1 }, result[0]);
    }

    [Fact]
    public void BufferToggle_MultipleConcurrentBuffers()
    {
        Subject<int> source = new();
        Subject<Unit> opens = new();
        Subject<Unit> closes1 = new();
        Subject<Unit> closes2 = new();
        int callCount = 0;
        List<Subject<Unit>> closers = new() { closes1, closes2 };

        LiveList<int[]> result = source.BufferToggle(opens, _ =>
        {
            return (Observable<Unit>)closers[callCount++];
        }).ToLiveList();

        opens.OnNext(Unit.Default);
        source.OnNext(1);
        opens.OnNext(Unit.Default);
        source.OnNext(2);
        closes1.OnNext(Unit.Default);
        source.OnNext(3);
        closes2.OnNext(Unit.Default);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 1, 2 }, result[0]);
        Assert.Equal(new[] { 2, 3 }, result[1]);
    }

    [Fact]
    public void BufferToggle_SourceComplete_EmitsAllOpenBuffers()
    {
        Subject<int> source = new();
        Subject<Unit> opens = new();
        Subject<Unit> closes = new();
        LiveList<int[]> result = source.BufferToggle(opens, _ => (Observable<Unit>)closes).ToLiveList();

        opens.OnNext(Unit.Default);
        source.OnNext(5);
        source.OnNext(6);
        source.OnCompleted();

        Assert.Single(result);
        Assert.Equal(new[] { 5, 6 }, result[0]);
        Assert.True(result.IsCompleted);
    }

    // -----------------------------------------------------------------------
    //  argument validationBufferWhen 
    // -----------------------------------------------------------------------

    [Fact]
    public void BufferWhen_NullSource_Throws()
    {
        Observable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() =>
            source!.BufferWhen(() => Observable.Empty<Unit>()));
    }

    [Fact]
    public void BufferWhen_NullClosingSelector_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Observable.Return(1).BufferWhen((Func<Observable<Unit>>)null!));
    }

    // -----------------------------------------------------------------------
    //  behaviourBufferWhen 
    // -----------------------------------------------------------------------

    [Fact]
    public void BufferWhen_EmitsOnClose()
    {
        Subject<int> source = new();
        Subject<Unit> closer = new();
        LiveList<int[]> result = source.BufferWhen(() => (Observable<Unit>)closer).ToLiveList();

        source.OnNext(1);
        source.OnNext(2);
        closer.OnNext(Unit.Default);

        Assert.Single(result);
        Assert.Equal(new[] { 1, 2 }, result[0]);
    }

    [Fact]
    public void BufferWhen_MultipleCloses_EmitsSuccessiveBuffers()
    {
        Subject<int> source = new();
        Subject<Unit> closer = new();
        LiveList<int[]> result = source.BufferWhen(() => (Observable<Unit>)closer).ToLiveList();

        source.OnNext(1);
        source.OnNext(2);
        closer.OnNext(Unit.Default);

        Assert.Equal(new[] { 1, 2 }, result[0]);

        source.OnNext(3);
        closer.OnNext(Unit.Default);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 3 }, result[1]);
    }

    [Fact]
    public void BufferWhen_SourceComplete_FlushesRemainingBuffer()
    {
        Subject<int> source = new();
        Subject<Unit> closer = new();
        LiveList<int[]> result = source.BufferWhen(() => (Observable<Unit>)closer).ToLiveList();

        source.OnNext(7);
        source.OnNext(8);
        source.OnCompleted();

        Assert.Single(result);
        Assert.Equal(new[] { 7, 8 }, result[0]);
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void BufferWhen_CloseBeforeAnyItems_EmitsEmptyBuffer()
    {
        Subject<int> source = new();
        Subject<Unit> closer = new();
        LiveList<int[]> result = source.BufferWhen(() => (Observable<Unit>)closer).ToLiveList();

        closer.OnNext(Unit.Default);

        Assert.Single(result);
        Assert.Empty(result[0]);
    }
}
