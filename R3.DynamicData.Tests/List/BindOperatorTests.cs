// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

using R3.DynamicData.List;

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
}
