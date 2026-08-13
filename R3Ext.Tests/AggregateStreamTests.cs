#pragma warning disable SA1107, SA1124, SA1501, SA1503, SA1515, SA1025, SA1520, SA1513, SA1508, SA1516
using R3;
using R3.Collections;

namespace R3Ext.Tests;

public class AggregateStreamTests
{
    [Fact]
    public void RunningCount_EmitsIncrementalCount()
    {
        Subject<string> subject = new();
        LiveList<int> result = subject.RunningCount().ToLiveList();
        subject.OnNext("a"); subject.OnNext("b"); subject.OnNext("c");
        subject.OnCompleted();
        Assert.Equal(new[] { 1, 2, 3 }, result.ToArray());
    }

    [Fact]
    public void RunningSum_EmitsRunningSumOfInts()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.RunningSum().ToLiveList();
        subject.OnNext(1); subject.OnNext(2); subject.OnNext(3);
        subject.OnCompleted();
        Assert.Equal(new[] { 1, 3, 6 }, result.ToArray());
    }

    [Fact]
    public void RunningMin_EmitsCurrentMinimum()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.RunningMin().ToLiveList();
        subject.OnNext(5); subject.OnNext(3); subject.OnNext(7); subject.OnNext(1);
        subject.OnCompleted();
        Assert.Equal(new[] { 5, 3, 3, 1 }, result.ToArray());
    }

    [Fact]
    public void RunningMax_EmitsCurrentMaximum()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.RunningMax().ToLiveList();
        subject.OnNext(1); subject.OnNext(5); subject.OnNext(3); subject.OnNext(7);
        subject.OnCompleted();
        Assert.Equal(new[] { 1, 5, 5, 7 }, result.ToArray());
    }

    [Fact]
    public void RunningAverage_EmitsRunningAverageOfDoubles()
    {
        Subject<double> subject = new();
        LiveList<double> result = subject.RunningAverage().ToLiveList();
        subject.OnNext(2.0); subject.OnNext(4.0); subject.OnNext(6.0);
        subject.OnCompleted();
        Assert.Equal(new[] { 2.0, 3.0, 4.0 }, result.ToArray());
    }

    [Fact]
    public void RunningAverage_EmitsRunningAverageOfDecimals()
    {
        Subject<decimal> subject = new();
        LiveList<decimal> result = subject.RunningAverage().ToLiveList();
        subject.OnNext(2m); subject.OnNext(4m); subject.OnNext(6m);
        subject.OnCompleted();
        Assert.Equal(new[] { 2m, 3m, 4m }, result.ToArray());
    }

    [Fact]
    public void RunningAverage_EmitsRunningAverageOfInts()
    {
        Subject<int> subject = new();
        LiveList<double> result = subject.RunningAverage().ToLiveList();
        subject.OnNext(1); subject.OnNext(3); subject.OnNext(5);
        subject.OnCompleted();
        Assert.Equal(new[] { 1.0, 2.0, 3.0 }, result.ToArray());
    }

    [Fact]
    public void RunningMin_WithCustomComparer_EmitsCurrentMinimum()
    {
        Subject<string> subject = new();
        LiveList<string> result = subject.RunningMin(StringComparer.Ordinal).ToLiveList();
        subject.OnNext("banana"); subject.OnNext("apple"); subject.OnNext("cherry");
        subject.OnCompleted();
        Assert.Equal(new[] { "banana", "apple", "apple" }, result.ToArray());
    }

    [Fact]
    public void RunningMax_WithCustomComparer_EmitsCurrentMaximum()
    {
        Subject<string> subject = new();
        LiveList<string> result = subject.RunningMax(StringComparer.Ordinal).ToLiveList();
        subject.OnNext("apple"); subject.OnNext("cherry"); subject.OnNext("banana");
        subject.OnCompleted();
        Assert.Equal(new[] { "apple", "cherry", "cherry" }, result.ToArray());
    }

    [Fact]
    public void RunningCount_EmptySequence_EmitsNothing()
    {
        Subject<int> subject = new();
        LiveList<int> result = subject.RunningCount().ToLiveList();
        subject.OnCompleted();
        Assert.Empty(result.ToArray());
    }
}
