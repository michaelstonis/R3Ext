using System.Collections.Generic;
using System.Linq;
using R3;
using R3.DynamicData.List;
using Xunit;

namespace R3.DynamicData.Tests.List;

/// <summary>
/// Tests for logical operators using ToCollection() to verify UI binding behavior.
/// This matches how DDLogicalOperatorsPage uses the operators.
/// </summary>
public sealed class LogicalOperatorsWithToCollectionTests
{
    [Fact]
    public void And_ToCollection_ReturnsIntersection()
    {
        // Arrange
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        IReadOnlyList<int> result = null!;

        var subscription = source1.Connect()
            .And(source2.Connect())
            .ToCollection()
            .Subscribe(items => result = items);

        // Act
        source1.AddRange(new[] { 1, 2, 3, 4 });
        source2.AddRange(new[] { 3, 4, 5, 6 });

        // Assert - AND should return items in both lists
        Assert.Equal(2, result.Count);
        Assert.Contains(3, result);
        Assert.Contains(4, result);
        Assert.DoesNotContain(1, result);
        Assert.DoesNotContain(2, result);
        Assert.DoesNotContain(5, result);
        Assert.DoesNotContain(6, result);

        subscription.Dispose();
        source1.Dispose();
        source2.Dispose();
    }

    [Fact]
    public void Or_ToCollection_ReturnsUnion()
    {
        // Arrange
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        IReadOnlyList<int> result = null!;

        var subscription = source1.Connect()
            .Or(source2.Connect())
            .ToCollection()
            .Subscribe(items => result = items);

        // Act
        source1.AddRange(new[] { 1, 2, 3 });
        source2.AddRange(new[] { 3, 4, 5 });

        // Assert - OR should return all unique items from both lists
        Assert.Equal(5, result.Count);
        Assert.Contains(1, result);
        Assert.Contains(2, result);
        Assert.Contains(3, result);
        Assert.Contains(4, result);
        Assert.Contains(5, result);

        subscription.Dispose();
        source1.Dispose();
        source2.Dispose();
    }

    [Fact]
    public void Or_ToCollection_DoesNotDuplicate()
    {
        // Arrange
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        IReadOnlyList<int> result = null!;

        var subscription = source1.Connect()
            .Or(source2.Connect())
            .ToCollection()
            .Subscribe(items => result = items);

        // Act - Add same value to both lists
        source1.Add(5);
        source2.Add(5);

        // Assert - Should only appear once in result
        Assert.Single(result);
        Assert.Equal(5, result[0]);

        subscription.Dispose();
        source1.Dispose();
        source2.Dispose();
    }

    [Fact]
    public void Except_ToCollection_ReturnsItemsInFirstButNotSecond()
    {
        // Arrange
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        IReadOnlyList<int> result = null!;

        var subscription = source1.Connect()
            .Except(source2.Connect())
            .ToCollection()
            .Subscribe(items => result = items);

        // Act
        source1.AddRange(new[] { 1, 2, 3, 4, 5 });
        source2.AddRange(new[] { 3, 4, 5, 6, 7 });

        // Assert - EXCEPT should return items only in source1
        Assert.Equal(2, result.Count);
        Assert.Contains(1, result);
        Assert.Contains(2, result);
        Assert.DoesNotContain(3, result);
        Assert.DoesNotContain(4, result);
        Assert.DoesNotContain(5, result);

        subscription.Dispose();
        source1.Dispose();
        source2.Dispose();
    }

    [Fact]
    public void Xor_ToCollection_ReturnsItemsInEitherButNotBoth()
    {
        // Arrange
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        IReadOnlyList<int> result = null!;

        var subscription = source1.Connect()
            .Xor(source2.Connect())
            .ToCollection()
            .Subscribe(items => result = items);

        // Act
        source1.AddRange(new[] { 1, 2, 3, 4 });
        source2.AddRange(new[] { 3, 4, 5, 6 });

        // Assert - XOR should return items in either but not both
        Assert.Equal(4, result.Count);
        Assert.Contains(1, result);
        Assert.Contains(2, result);
        Assert.Contains(5, result);
        Assert.Contains(6, result);
        Assert.DoesNotContain(3, result); // In both, so excluded
        Assert.DoesNotContain(4, result); // In both, so excluded

        subscription.Dispose();
        source1.Dispose();
        source2.Dispose();
    }

    [Fact]
    public void And_ToCollection_UpdatesWhenSourceChanges()
    {
        // Arrange
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        IReadOnlyList<int> result = null!;

        var subscription = source1.Connect()
            .And(source2.Connect())
            .ToCollection()
            .Subscribe(items => result = items);

        source1.AddRange(new[] { 1, 2, 3 });
        source2.AddRange(new[] { 2, 3, 4 });

        // Initial state
        Assert.Equal(2, result.Count);
        Assert.Contains(2, result);
        Assert.Contains(3, result);

        // Act - Add item to both
        source1.Add(5);
        source2.Add(5);

        // Assert - Should now include 5
        Assert.Equal(3, result.Count);
        Assert.Contains(2, result);
        Assert.Contains(3, result);
        Assert.Contains(5, result);

        // Act - Remove item from one source
        source1.Remove(2);

        // Assert - Should no longer include 2
        Assert.Equal(2, result.Count);
        Assert.Contains(3, result);
        Assert.Contains(5, result);
        Assert.DoesNotContain(2, result);

        subscription.Dispose();
        source1.Dispose();
        source2.Dispose();
    }

    [Fact]
    public void Or_ToCollection_UpdatesWhenSourceChanges()
    {
        // Arrange
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        IReadOnlyList<int> result = null!;

        var subscription = source1.Connect()
            .Or(source2.Connect())
            .ToCollection()
            .Subscribe(items => result = items);

        source1.AddRange(new[] { 1, 2 });
        source2.AddRange(new[] { 3, 4 });

        // Initial state
        Assert.Equal(4, result.Count);

        // Act - Add item to source1
        source1.Add(5);

        // Assert - Should include 5
        Assert.Equal(5, result.Count);
        Assert.Contains(5, result);

        // Act - Remove item from source1 only
        source1.Remove(5);

        // Assert - Should no longer include 5
        Assert.Equal(4, result.Count);
        Assert.DoesNotContain(5, result);

        // Act - Add same item to both sources
        source1.Add(10);
        source2.Add(10);

        // Assert - Should appear once
        Assert.Equal(5, result.Count);
        Assert.Single(result, x => x == 10);

        subscription.Dispose();
        source1.Dispose();
        source2.Dispose();
    }

    [Fact]
    public void Except_ToCollection_UpdatesWhenSourceChanges()
    {
        // Arrange
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        IReadOnlyList<int> result = null!;

        var subscription = source1.Connect()
            .Except(source2.Connect())
            .ToCollection()
            .Subscribe(items => result = items);

        source1.AddRange(new[] { 1, 2, 3, 4 });
        source2.AddRange(new[] { 3, 4 });

        // Initial state - should have 1, 2
        Assert.Equal(2, result.Count);
        Assert.Contains(1, result);
        Assert.Contains(2, result);

        // Act - Remove excluded item from source2
        source2.Remove(3);

        // Assert - 3 should now appear (it's in source1 but no longer in source2)
        Assert.Equal(3, result.Count);
        Assert.Contains(1, result);
        Assert.Contains(2, result);
        Assert.Contains(3, result);

        // Act - Add new item to source2 that exists in source1
        source2.Add(1);

        // Assert - 1 should be excluded now
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(1, result);
        Assert.Contains(2, result);
        Assert.Contains(3, result);

        subscription.Dispose();
        source1.Dispose();
        source2.Dispose();
    }

    [Fact]
    public void Xor_ToCollection_UpdatesWhenSourceChanges()
    {
        // Arrange
        var source1 = new SourceList<int>();
        var source2 = new SourceList<int>();
        IReadOnlyList<int> result = null!;

        var subscription = source1.Connect()
            .Xor(source2.Connect())
            .ToCollection()
            .Subscribe(items => result = items);

        source1.AddRange(new[] { 1, 2, 3 });
        source2.AddRange(new[] { 3, 4, 5 });

        // Initial state - should have 1, 2, 4, 5 (not 3 which is in both)
        Assert.Equal(4, result.Count);
        Assert.Contains(1, result);
        Assert.Contains(2, result);
        Assert.Contains(4, result);
        Assert.Contains(5, result);
        Assert.DoesNotContain(3, result);

        // Act - Add item that exists in other source
        source1.Add(4);

        // Assert - 4 should disappear (now in both)
        Assert.Equal(3, result.Count);
        Assert.Contains(1, result);
        Assert.Contains(2, result);
        Assert.Contains(5, result);
        Assert.DoesNotContain(4, result);

        // Act - Remove item from one source
        source2.Remove(4);

        // Assert - 4 should reappear (now only in source1)
        Assert.Equal(4, result.Count);
        Assert.Contains(1, result);
        Assert.Contains(2, result);
        Assert.Contains(4, result);
        Assert.Contains(5, result);

        subscription.Dispose();
        source1.Dispose();
        source2.Dispose();
    }
}
