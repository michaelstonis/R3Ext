using R3;
using R3Ext;
using Xunit;

namespace R3Ext.Tests;

/// <summary>
/// Tests for CollectionExtensions covering ForEach and Shuffle operations.
/// </summary>
[Collection("FrameProvider")]
public class CollectionExtensionsTests(FrameProviderFixture fp)
{
    [Fact]
    public void ForEach_Generic_ThrowsOnNullSource()
    {
        Observable<IEnumerable<int>> source = null!;
        Assert.Throws<ArgumentNullException>(() => source.ForEach<int, IEnumerable<int>>());
    }

    [Fact]
    public void ForEach_Generic_ExpandsSequence()
    {
        var source = new Subject<IEnumerable<int>>();
        var results = new List<int>();

        source.ForEach<int, IEnumerable<int>>().Subscribe(results.Add);

        source.OnNext(new[] { 1, 2, 3 });
        source.OnNext(new[] { 4, 5 });

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, results);
    }

    [Fact]
    public void ForEach_Generic_HandlesNullSequence()
    {
        var source = new Subject<IEnumerable<int>>();
        var results = new List<int>();

        source.ForEach<int, IEnumerable<int>>().Subscribe(results.Add);

        source.OnNext(null!);
        source.OnNext(new[] { 1, 2 });

        Assert.Equal(new[] { 1, 2 }, results); // null sequence is skipped
    }

    [Fact]
    public void ForEach_Generic_UsesArrayFastPath()
    {
        var source = new Subject<IEnumerable<int>>();
        var results = new List<int>();

        source.ForEach<int, IEnumerable<int>>().Subscribe(results.Add);

        int[] array = { 10, 20, 30 };
        source.OnNext(array);

        Assert.Equal(new[] { 10, 20, 30 }, results);
    }

    [Fact]
    public void ForEach_Generic_UsesIListFastPath()
    {
        var source = new Subject<IEnumerable<int>>();
        var results = new List<int>();

        source.ForEach<int, IEnumerable<int>>().Subscribe(results.Add);

        IList<int> list = new List<int> { 100, 200, 300 };
        source.OnNext(list);

        Assert.Equal(new[] { 100, 200, 300 }, results);
    }

    [Fact]
    public void ForEach_Generic_HandlesEmptySequence()
    {
        var source = new Subject<IEnumerable<int>>();
        var results = new List<int>();

        source.ForEach<int, IEnumerable<int>>().Subscribe(results.Add);

        source.OnNext(Array.Empty<int>());

        Assert.Empty(results);
    }

    [Fact]
    public void ForEach_Array_ThrowsOnNullSource()
    {
        Observable<int[]> source = null!;
        Assert.Throws<ArgumentNullException>(() => source.ForEach());
    }

    [Fact]
    public void ForEach_Array_ExpandsArray()
    {
        var source = new Subject<int[]>();
        var results = new List<int>();

        source.ForEach().Subscribe(results.Add);

        source.OnNext(new[] { 1, 2, 3 });
        source.OnNext(new[] { 4, 5 });

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, results);
    }

    [Fact]
    public void ForEach_Array_HandlesNullArray()
    {
        var source = new Subject<int[]>();
        var results = new List<int>();

        source.ForEach().Subscribe(results.Add);

        source.OnNext(null!);
        source.OnNext(new[] { 1, 2 });

        Assert.Equal(new[] { 1, 2 }, results); // null array is skipped
    }

    [Fact]
    public void ForEach_Array_HandlesEmptyArray()
    {
        var source = new Subject<int[]>();
        var results = new List<int>();

        source.ForEach().Subscribe(results.Add);

        source.OnNext(Array.Empty<int>());

        Assert.Empty(results);
    }

    [Fact]
    public void ForEach_IList_ThrowsOnNullSource()
    {
        Observable<IList<int>> source = null!;
        Assert.Throws<ArgumentNullException>(() => source.ForEach());
    }

    [Fact]
    public void ForEach_IList_ExpandsList()
    {
        var source = new Subject<IList<int>>();
        var results = new List<int>();

        source.ForEach().Subscribe(results.Add);

        source.OnNext(new List<int> { 1, 2, 3 });
        source.OnNext(new List<int> { 4, 5 });

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, results);
    }

    [Fact]
    public void ForEach_IList_HandlesNullList()
    {
        var source = new Subject<IList<int>>();
        var results = new List<int>();

        source.ForEach().Subscribe(results.Add);

        source.OnNext(null!);
        source.OnNext(new List<int> { 1, 2 });

        Assert.Equal(new[] { 1, 2 }, results); // null list is skipped
    }

    [Fact]
    public void ForEach_IList_HandlesEmptyList()
    {
        var source = new Subject<IList<int>>();
        var results = new List<int>();

        source.ForEach().Subscribe(results.Add);

        source.OnNext(new List<int>());

        Assert.Empty(results);
    }

    [Fact]
    public void ForEach_List_ThrowsOnNullSource()
    {
        Observable<List<int>> source = null!;
        Assert.Throws<ArgumentNullException>(() => source.ForEach());
    }

    [Fact]
    public void ForEach_List_ExpandsList()
    {
        var source = new Subject<List<int>>();
        var results = new List<int>();

        source.ForEach().Subscribe(results.Add);

        source.OnNext(new List<int> { 1, 2, 3 });
        source.OnNext(new List<int> { 4, 5 });

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, results);
    }

    [Fact]
    public void ForEach_List_HandlesNullList()
    {
        var source = new Subject<List<int>>();
        var results = new List<int>();

        source.ForEach().Subscribe(results.Add);

        source.OnNext(null!);
        source.OnNext(new List<int> { 1, 2 });

        Assert.Equal(new[] { 1, 2 }, results); // null list is skipped
    }

    [Fact]
    public void ForEach_List_HandlesEmptyList()
    {
        var source = new Subject<List<int>>();
        var results = new List<int>();

        source.ForEach().Subscribe(results.Add);

        source.OnNext(new List<int>());

        Assert.Empty(results);
    }

    [Fact]
    public void Shuffle_IList_ThrowsOnNullList()
    {
        IList<int> list = null!;
        Assert.Throws<ArgumentNullException>(() => list.Shuffle());
    }

    [Fact]
    public void Shuffle_IList_ShufflesElements()
    {
        var original = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var list = new List<int>(original);

        list.Shuffle(new Random(42)); // Use seed for reproducibility

        // After shuffle, list should have same elements but likely different order
        Assert.Equal(original.OrderBy(x => x), list.OrderBy(x => x)); // Same elements

        // With 10 elements, the probability of being in original order is 1/10! which is negligible
    }

    [Fact]
    public void Shuffle_IList_HandlesSingleElement()
    {
        var list = new List<int> { 42 };

        list.Shuffle();

        Assert.Single(list);
        Assert.Equal(42, list[0]);
    }

    [Fact]
    public void Shuffle_IList_HandlesEmptyList()
    {
        var list = new List<int>();

        list.Shuffle();

        Assert.Empty(list);
    }

    [Fact]
    public void Shuffle_IList_UsesCustomRandom()
    {
        var list1 = new List<int> { 1, 2, 3, 4, 5 };
        var list2 = new List<int> { 1, 2, 3, 4, 5 };

        list1.Shuffle(new Random(123));
        list2.Shuffle(new Random(123)); // Same seed should produce same shuffle

        Assert.Equal(list1, list2);
    }

    [Fact]
    public void Shuffle_Array_ThrowsOnNullArray()
    {
        int[] array = null!;
        Assert.Throws<ArgumentNullException>(() => array.Shuffle());
    }

    [Fact]
    public void Shuffle_Array_ShufflesElements()
    {
        var original = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var array = (int[])original.Clone();

        array.Shuffle(new Random(42)); // Use seed for reproducibility

        // After shuffle, array should have same elements but likely different order
        Assert.Equal(original.OrderBy(x => x), array.OrderBy(x => x)); // Same elements
    }

    [Fact]
    public void Shuffle_Array_HandlesSingleElement()
    {
        var array = new[] { 42 };

        array.Shuffle();

        Assert.Single(array);
        Assert.Equal(42, array[0]);
    }

    [Fact]
    public void Shuffle_Array_HandlesEmptyArray()
    {
        var array = Array.Empty<int>();

        array.Shuffle();

        Assert.Empty(array);
    }

    [Fact]
    public void Shuffle_Array_UsesCustomRandom()
    {
        var array1 = new[] { 1, 2, 3, 4, 5 };
        var array2 = new[] { 1, 2, 3, 4, 5 };

        array1.Shuffle(new Random(123));
        array2.Shuffle(new Random(123)); // Same seed should produce same shuffle

        Assert.Equal(array1, array2);
    }

    [Fact]
    public void ForEach_Generic_WithNonOptimizedEnumerable()
    {
        var source = new Subject<IEnumerable<int>>();
        var results = new List<int>();

        source.ForEach<int, IEnumerable<int>>().Subscribe(results.Add);

        // HashSet doesn't implement IList, so it uses the foreach path
        source.OnNext(new HashSet<int> { 10, 20, 30 });

        Assert.Equal(3, results.Count);
        Assert.Contains(10, results);
        Assert.Contains(20, results);
        Assert.Contains(30, results);
    }

    [Fact]
    public void ForEach_MultipleSubscribers()
    {
        var source = new Subject<int[]>();
        var results1 = new List<int>();
        var results2 = new List<int>();

        source.ForEach().Subscribe(results1.Add);
        source.ForEach().Subscribe(results2.Add);

        source.OnNext(new[] { 1, 2, 3 });

        Assert.Equal(new[] { 1, 2, 3 }, results1);
        Assert.Equal(new[] { 1, 2, 3 }, results2);
    }

    [Fact]
    public void ForEach_DisposalStopsEmissions()
    {
        var source = new Subject<int[]>();
        var results = new List<int>();

        var subscription = source.ForEach().Subscribe(results.Add);

        source.OnNext(new[] { 1, 2 });
        subscription.Dispose();
        source.OnNext(new[] { 3, 4 }); // Should not be emitted

        Assert.Equal(new[] { 1, 2 }, results);
    }
}
