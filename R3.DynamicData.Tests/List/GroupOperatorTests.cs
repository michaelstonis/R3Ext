// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

using R3.DynamicData.List;

namespace R3.DynamicData.Tests.List;

public class GroupOperatorTests
{
    [Fact]
    public void Group_CreatesAndRemovesGroups()
    {
        var list = new SourceList<string>();
        list.AddRange(new[] { "a1", "a2", "b1" });

        var results = new List<IChangeSet<Group<char, string>>>();
        list.Connect()
            .Group(s => s[0])
            .Subscribe(results.Add);

        // initial groups: 'a', 'b'
        Assert.Single(results);
        var snapshot = results[0].Select(c => c.Item.Key).ToList();
        Assert.Contains('a', snapshot);
        Assert.Contains('b', snapshot);

        list.Add("b2");
        list.Remove("a1");
        list.Remove("a2"); // removes last 'a' item => group 'a' should be removed

        var last = results.Last();

        // Expect at least one remove of a group
        Assert.Contains(last, c => c.Reason == ListChangeReason.Remove && c.Item.Key == 'a');
    }
}
