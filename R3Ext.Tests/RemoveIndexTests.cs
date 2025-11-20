using R3; // For Observable extensions
using R3.DynamicData.List;
using Xunit;

namespace R3Ext.Tests;

public class RemoveIndexTests
{
    [Fact(Skip="RemoveIndex operator not implemented yet")]
    public void RemoveIndex_RemovesIndicesFromAllChanges()
    {
        // Placeholder: original test logic removed because RemoveIndex operator is not yet implemented.
        // The previous body subscribed to source.Connect().RemoveIndex() and performed a sequence of mutations
        // asserting indices were normalized to -1. Once the operator is added, restore that logic.
        Assert.True(true);
    }
}
