
// Port of DynamicData to R3.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using R3.DynamicData.Binding;
using R3.DynamicData.List;
using static R3.DynamicData.List.ObservableListEx;

namespace R3.DynamicData.Tests.List;

public class BindOperatorTests
{
    [Fact]
    public void Bind_TracksTargetList()
    {
        var source = new SourceList<int>();
        var target = new List<int>();

        using var sub = source.Connect().Bind(target);

        source.AddRange(new[] { 1, 2, 3 });
        source.Insert(1, 42);
        source.RemoveAt(0);
        source.ReplaceAt(1, 99);
        source.Move(0, 2);
        source.Clear();

        Assert.Empty(target);
    }

    [Fact]
    public void Bind_OutReadOnlyObservableCollection_Works()
    {
        var source = new SourceList<int>();
        using var sub = source.Connect().Bind(out ReadOnlyObservableCollection<int> readOnlyCollection);
        source.AddRange(new[] { 1, 2, 3 });
        Assert.Equal(new[] { 1, 2, 3 }, readOnlyCollection);
        source.Clear();
        Assert.Empty(readOnlyCollection);
    }

    [Fact]
    public void Bind_ObservableCollectionExtended_Works()
    {
        var source = new SourceList<int>();
        var target = new ObservableCollectionExtended<int>();
        using var sub = source.Connect().Bind(target);
        source.AddRange(new[] { 1, 2, 3 });
        Assert.Equal(new[] { 1, 2, 3 }, target);
        source.Clear();
        Assert.Empty(target);
    }
}
