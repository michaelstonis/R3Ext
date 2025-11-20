// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public class DisposeManyOperatorTests
{
    private sealed class TrackDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    [Fact]
    public void DisposeMany_DisposesOnRemove()
    {
        var source = new SourceList<TrackDisposable>();
        var d1 = new TrackDisposable();
        var d2 = new TrackDisposable();
        using var sub = source.Connect().DisposeMany().Subscribe(_ => { });
        source.AddRange(new[] { d1, d2 });
        source.RemoveAt(0);
        Assert.True(d1.IsDisposed);
        Assert.False(d2.IsDisposed);
    }

    [Fact]
    public void DisposeMany_DisposesOnReplace()
    {
        var source = new SourceList<TrackDisposable>();
        var d1 = new TrackDisposable();
        var d2 = new TrackDisposable();
        var d3 = new TrackDisposable();
        using var sub = source.Connect().DisposeMany().Subscribe(_ => { });
        source.AddRange(new[] { d1, d2 });
        source.Replace(d2, d3);
        Assert.True(d2.IsDisposed);
        Assert.False(d3.IsDisposed);
    }

    [Fact]
    public void DisposeMany_DisposesOnClear()
    {
        var source = new SourceList<TrackDisposable>();
        var d1 = new TrackDisposable();
        var d2 = new TrackDisposable();
        using var sub = source.Connect().DisposeMany().Subscribe(_ => { });
        source.AddRange(new[] { d1, d2 });
        source.Clear();
        Assert.True(d1.IsDisposed);
        Assert.True(d2.IsDisposed);
    }
}
