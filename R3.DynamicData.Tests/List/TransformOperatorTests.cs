// Port of DynamicData to R3.

using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public class TransformOperatorTests
{
    [Fact]
    public void Transform_InitialItems_AreProjected()
    {
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 2, 3 });

        var results = new List<IChangeSet<string>>();
        list.Connect()
            .Transform(i => $"#{i}")
            .Subscribe(results.Add);

        Assert.Single(results);
        var changes = results[0];
        var items = changes.Select(c => c.Item).ToList();
        Assert.Equal(new[] { "#1", "#2", "#3" }, items);
    }

    [Fact]
    public void Transform_AddRemoveReplace_Move()
    {
        var list = new SourceList<int>();
        list.AddRange(new[] { 1, 2, 3 });

        var results = new List<IChangeSet<string>>();
        list.Connect()
            .Transform(i => (i * 10).ToString())
            .Subscribe(results.Add);

        list.Add(4); // add at end
        list.RemoveAt(1); // remove "20"
        list.ReplaceAt(1, 5); // replace 3 -> 5
        list.Move(0, 2); // move first to third

        // Verify last change set reflects move
        var last = results.Last();
        Assert.Contains(last, c => c.Reason == ListChangeReason.Moved);
    }
}
