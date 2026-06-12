using Microsoft.Extensions.Time.Testing;
using R3;
using R3.Collections;

namespace R3Ext.Tests;

public class FilteringAdvancedTests
{
    // ─── IgnoreElements ───────────────────────────────────────────────────────

    [Fact]
    public void IgnoreElements_SuppressesOnNext()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.IgnoreElements().ToLiveList();
        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        Assert.Empty(result.ToArray());
        subject.OnCompleted();
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void IgnoreElements_ForwardsError()
    {
        Subject<int> subject = new();
        List<Exception> errors = new();
        using IDisposable _ = subject.IgnoreElements().Subscribe(_ => { }, errors.Add, _ => { });
        subject.OnErrorResume(new InvalidOperationException("boom"));
        Assert.Single(errors);
        Assert.IsType<InvalidOperationException>(errors[0]);
    }

    [Fact]
    public void IgnoreElements_ForwardsCompletion()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.IgnoreElements().ToLiveList();
        subject.OnNext(42);
        Assert.Empty(result.ToArray());
        subject.OnCompleted();
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void IgnoreElements_ThrowsOnNullSource()
    {
        Observable<int>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.IgnoreElements());
    }

    // ─── IsEmpty ─────────────────────────────────────────────────────────────

    [Fact]
    public void IsEmpty_TrueWhenNoElements()
    {
        Subject<int> subject = new();
        LiveList<bool> result = subject.IsEmpty().ToLiveList();
        subject.OnCompleted();
        Assert.Equal(new[] { true }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void IsEmpty_FalseOnFirstElement()
    {
        Subject<int> subject = new();
        LiveList<bool> result = subject.IsEmpty().ToLiveList();
        subject.OnNext(42);
        Assert.Equal(new[] { false }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void IsEmpty_CompletesAfterFirstElement_IgnoresSubsequent()
    {
        Subject<int> subject = new();
        LiveList<bool> result = subject.IsEmpty().ToLiveList();
        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        Assert.Equal(new[] { false }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void IsEmpty_ThrowsOnNullSource()
    {
        Observable<int>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.IsEmpty());
    }

    // ─── Every / All ─────────────────────────────────────────────────────────

    [Fact]
    public void Every_FalseOnFirstFail()
    {
        Subject<int> subject = new();
        LiveList<bool> result = subject.Every(x => x > 0).ToLiveList();
        subject.OnNext(1);
        subject.OnNext(-1);
        Assert.Equal(new[] { false }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void Every_TrueOnCompletion()
    {
        Subject<int> subject = new();
        LiveList<bool> result = subject.Every(x => x > 0).ToLiveList();
        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnCompleted();
        Assert.Equal(new[] { true }, result.ToArray());
    }

    [Fact]
    public void Every_TrueWhenEmpty()
    {
        Subject<int> subject = new();
        LiveList<bool> result = subject.Every(x => x > 0).ToLiveList();
        subject.OnCompleted();
        Assert.Equal(new[] { true }, result.ToArray());
    }

    [Fact]
    public void Every_FalseOnVeryFirstValue()
    {
        Subject<int> subject = new();
        LiveList<bool> result = subject.Every(x => x > 0).ToLiveList();
        subject.OnNext(-5);
        Assert.Equal(new[] { false }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void All_IsAliasForEvery()
    {
        Subject<int> subject = new();
        LiveList<bool> result = subject.All(x => x > 0).ToLiveList();
        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnCompleted();
        Assert.Equal(new[] { true }, result.ToArray());
    }

    [Fact]
    public void Every_ThrowsOnNullSource()
    {
        Observable<int>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.Every(x => x > 0));
    }

    [Fact]
    public void Every_ThrowsOnNullPredicate()
    {
        Subject<int> subject = new();
        Assert.Throws<ArgumentNullException>(() => subject.Every(null!));
    }

    // ─── Find ────────────────────────────────────────────────────────────────

    [Fact]
    public void Find_EmitsFirstMatchAndCompletes()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.Find(x => x > 5).ToLiveList();
        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(10);
        subject.OnNext(20);
        Assert.Equal(new[] { 10 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void Find_CompletesWithoutEmitWhenNoMatch()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.Find(x => x > 100).ToLiveList();
        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnCompleted();
        Assert.Empty(result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void Find_EmitsFirstMatchOnly()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.Find(x => x % 2 == 0).ToLiveList();
        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(4);
        Assert.Equal(new[] { 2 }, result.ToArray());
    }

    [Fact]
    public void Find_ThrowsOnNullSource()
    {
        Observable<int>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.Find(x => x > 0));
    }

    // ─── FindIndex ───────────────────────────────────────────────────────────

    [Fact]
    public void FindIndex_EmitsCorrectIndex()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.FindIndex(x => x > 5).ToLiveList();
        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(10);
        Assert.Equal(new[] { 2 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void FindIndex_EmitsZeroForFirstElement()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.FindIndex(x => x > 0).ToLiveList();
        subject.OnNext(99);
        Assert.Equal(new[] { 0 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void FindIndex_CompletesWithoutEmitWhenNoMatch()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.FindIndex(x => x > 100).ToLiveList();
        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnCompleted();
        Assert.Empty(result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void FindIndex_ThrowsOnNullSource()
    {
        Observable<int>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.FindIndex(x => x > 0));
    }

    // ─── DefaultIfEmpty ──────────────────────────────────────────────────────

    [Fact]
    public void DefaultIfEmpty_EmitsDefaultWhenEmpty()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.DefaultIfEmpty(99).ToLiveList();
        subject.OnCompleted();
        Assert.Equal(new[] { 99 }, result.ToArray());
    }

    [Fact]
    public void DefaultIfEmpty_DoesNotEmitDefaultWhenHasValues()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.DefaultIfEmpty(99).ToLiveList();
        subject.OnNext(1);
        subject.OnCompleted();
        Assert.Equal(new[] { 1 }, result.ToArray());
    }

    [Fact]
    public void DefaultIfEmpty_ForwardsAllValuesWhenPresent()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.DefaultIfEmpty(0).ToLiveList();
        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        subject.OnCompleted();
        Assert.Equal(new[] { 1, 2, 3 }, result.ToArray());
    }

    [Fact]
    public void DefaultIfEmpty_ThrowsOnNullSource()
    {
        Observable<int>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.DefaultIfEmpty(0));
    }

    // ─── ThrowIfEmpty ────────────────────────────────────────────────────────

    [Fact]
    public void ThrowIfEmpty_ThrowsDefaultExceptionWhenEmpty()
    {
        Subject<int> subject = new();
        List<Result> completions = new();
        using IDisposable _ = subject.ThrowIfEmpty().Subscribe(_ => { }, _ => { }, completions.Add);
        subject.OnCompleted();
        Assert.Single(completions);
        Assert.True(completions[0].IsFailure);
        Assert.IsType<InvalidOperationException>(completions[0].Exception);
    }

    [Fact]
    public void ThrowIfEmpty_UsesCustomExceptionFactory()
    {
        Subject<int> subject = new();
        List<Result> completions = new();
        using IDisposable _ = subject
            .ThrowIfEmpty(() => new ArgumentException("custom"))
            .Subscribe(_ => { }, _ => { }, completions.Add);
        subject.OnCompleted();
        Assert.Single(completions);
        Assert.True(completions[0].IsFailure);
        Assert.IsType<ArgumentException>(completions[0].Exception);
    }

    [Fact]
    public void ThrowIfEmpty_DoesNotThrowWhenHasValues()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.ThrowIfEmpty().ToLiveList();
        subject.OnNext(1);
        subject.OnCompleted();
        Assert.Equal(new[] { 1 }, result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void ThrowIfEmpty_ThrowsOnNullSource()
    {
        Observable<int>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.ThrowIfEmpty());
    }

    // ─── Audit ───────────────────────────────────────────────────────────────

    [Fact]
    public void Audit_EmitsLatestWhenDurationFires()
    {
        Subject<int> source = new();
        Subject<Unit> durationSubject = new();
        LiveList<int> result = source.Audit(_ => durationSubject).ToLiveList();

        source.OnNext(1);
        source.OnNext(2);
        durationSubject.OnNext(Unit.Default);

        Assert.Equal(new[] { 2 }, result.ToArray());
    }

    [Fact]
    public void Audit_EmitsNothingIfNoDurationFire()
    {
        Subject<int> source = new();
        Subject<Unit> duration = new();
        LiveList<int> result = source.Audit(_ => duration).ToLiveList();

        source.OnNext(1);
        source.OnNext(2);
        source.OnCompleted();

        Assert.Empty(result.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void Audit_ResetsAfterDurationFiresAndAcceptsNewValue()
    {
        Subject<int> source = new();
        Subject<Unit> duration1 = new();
        Subject<Unit> duration2 = new();
        int call = 0;
        Subject<Unit>[] durations = [duration1, duration2];

        LiveList<int> result = source.Audit(_ => durations[call++ % 2]).ToLiveList();

        source.OnNext(10);
        duration1.OnNext(Unit.Default);
        Assert.Equal(new[] { 10 }, result.ToArray());

        source.OnNext(20);
        duration2.OnNext(Unit.Default);
        Assert.Equal(new[] { 10, 20 }, result.ToArray());
    }

    [Fact]
    public void Audit_ThrowsOnNullSource()
    {
        Observable<int>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.Audit(_ => Observable.Never<Unit>()));
    }

    // ─── AuditTime ───────────────────────────────────────────────────────────

    [Fact]
    public void AuditTime_EmitsLatestAfterDuration()
    {
        FakeTimeProvider tp = new();
        Subject<int> source = new();
        LiveList<int> result = source.AuditTime(TimeSpan.FromSeconds(1), tp).ToLiveList();

        source.OnNext(1);
        source.OnNext(2);
        tp.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(new[] { 2 }, result.ToArray());
    }

    [Fact]
    public void AuditTime_DoesNotEmitBeforeDuration()
    {
        FakeTimeProvider tp = new();
        Subject<int> source = new();
        LiveList<int> result = source.AuditTime(TimeSpan.FromSeconds(5), tp).ToLiveList();

        source.OnNext(42);
        tp.Advance(TimeSpan.FromSeconds(4));

        Assert.Empty(result.ToArray());
    }

    [Fact]
    public void AuditTime_CompletesWhenSourceCompletes()
    {
        FakeTimeProvider tp = new();
        Subject<int> source = new();
        LiveList<int> result = source.AuditTime(TimeSpan.FromSeconds(1), tp).ToLiveList();

        source.OnCompleted();
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void AuditTime_ThrowsOnNullSource()
    {
        Observable<int>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.AuditTime(TimeSpan.FromSeconds(1)));
    }

    // ─── Sample ──────────────────────────────────────────────────────────────

    [Fact]
    public void Sample_EmitsLatestWhenSamplerFires()
    {
        Subject<int> source = new();
        Subject<Unit> sampler = new();
        LiveList<int> result = source.Sample(sampler).ToLiveList();

        source.OnNext(1);
        source.OnNext(2);
        sampler.OnNext(Unit.Default);

        Assert.Equal(new[] { 2 }, result.ToArray());
    }

    [Fact]
    public void Sample_EmitsNothingIfNoSourceValue()
    {
        Subject<int> source = new();
        Subject<Unit> sampler = new();
        LiveList<int> result = source.Sample(sampler).ToLiveList();

        sampler.OnNext(Unit.Default);
        sampler.OnNext(Unit.Default);

        Assert.Empty(result.ToArray());
    }

    [Fact]
    public void Sample_ResetsAfterEachSample()
    {
        Subject<int> source = new();
        Subject<Unit> sampler = new();
        LiveList<int> result = source.Sample(sampler).ToLiveList();

        source.OnNext(10);
        sampler.OnNext(Unit.Default);
        Assert.Equal(new[] { 10 }, result.ToArray());

        sampler.OnNext(Unit.Default); // no new value since last sample
        Assert.Equal(new[] { 10 }, result.ToArray()); // nothing new emitted

        source.OnNext(20);
        sampler.OnNext(Unit.Default);
        Assert.Equal(new[] { 10, 20 }, result.ToArray());
    }

    [Fact]
    public void Sample_WithTypedSampler_EmitsLatest()
    {
        Subject<int> source = new();
        Subject<string> sampler = new();
        LiveList<int> result = source.Sample<int, string>(sampler).ToLiveList();

        source.OnNext(5);
        sampler.OnNext("tick");

        Assert.Equal(new[] { 5 }, result.ToArray());
    }

    [Fact]
    public void Sample_CompletesWhenSourceCompletes()
    {
        Subject<int> source = new();
        Subject<Unit> sampler = new();
        LiveList<int> result = source.Sample(sampler).ToLiveList();

        source.OnCompleted();
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public void Sample_ThrowsOnNullSource()
    {
        Observable<int>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.Sample(new Subject<Unit>()));
    }
}
