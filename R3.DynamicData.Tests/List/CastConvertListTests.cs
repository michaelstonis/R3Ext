// Tests for Convert and existing Cast list operators.
// StyleCop suppressions for brevity.
#pragma warning disable SA1516, SA1502, SA1003, SA1001, SA1011, SA1012, SA1013
using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public class CastConvertListTests
{
    private class Base { public int V; }
    private sealed class Derived : Base { public string Tag = string.Empty; }

    [Fact]
    public void Convert_ProjectsValues()
    {
        var src = new SourceList<int>();
        var results = new List<IChangeSet<string>>();
        using var sub = src.Connect().Convert(i => $"#{i}").Subscribe(results.Add);

        src.AddRange(new[] { 1, 2, 3 });
        src.ReplaceAt(1, 5); // Replace second value
        src.RemoveAt(0); // Remove first

        Assert.True(results.Count >= 2); // initial adds then subsequent change(s)
        var allAdds = results[0];
        Assert.Equal(3, allAdds.Adds);
        bool sawRemoveOrReplace = results.Skip(1).Any(cs => cs.Any(ch => ch.Reason == ListChangeReason.Remove || ch.Reason == ListChangeReason.Replace));
        Assert.True(sawRemoveOrReplace);
    }

    [Fact]
    public void Cast_SimpleUsage()
    {
        var src = new SourceList<Derived>();
        var results = new List<IChangeSet<Derived>>();
        using var sub = src.Connect().Cast<Derived, Derived>().Subscribe(results.Add);

        src.AddRange(new[] { new Derived { V = 1, Tag = "a" }, new Derived { V = 2, Tag = "b" } });
        src.ReplaceAt(1, new Derived { V = 3, Tag = "c" });
        src.RemoveAt(0);

        Assert.True(results.Count >= 2);
        var first = results[0];
        Assert.Equal(2, first.Adds);
        bool sawReplaceOrRemove = results.Skip(1).Any(cs => cs.Any(ch => ch.Reason == ListChangeReason.Remove || ch.Reason == ListChangeReason.Replace));
        Assert.True(sawReplaceOrRemove);
    }
}
