using System.Runtime.CompilerServices;
using R3;

namespace R3Ext.Tests;

/// <summary>
/// Module initializer that ensures ObservableSystem.DefaultFrameProvider is set
/// before any tests run. This fixes the issue where running individual tests
/// from VS Code GUI would fail because the collection fixture hadn't initialized yet.
/// </summary>
internal static class TestModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Only set if not already configured (allows collection fixtures to override)
        if (ObservableSystem.DefaultFrameProvider is null)
        {
            ObservableSystem.DefaultFrameProvider = new FakeFrameProvider();
        }
    }
}
