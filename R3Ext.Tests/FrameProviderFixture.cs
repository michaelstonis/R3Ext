using R3;

namespace R3Ext.Tests;

// Reusable xUnit collection fixture that installs a FakeFrameProvider
// to drive frame-based operators like EveryValueChanged deterministically.
public sealed class FrameProviderFixture : IDisposable
{
    private readonly dynamic _previous;

    public FakeFrameProvider Provider { get; } = new();

    public FrameProviderFixture()
    {
        _previous = ObservableSystem.DefaultFrameProvider;
        ObservableSystem.DefaultFrameProvider = Provider;
    }

    public void Advance()
    {
        Provider.Advance();
    }

    public void Dispose()
    {
        ObservableSystem.DefaultFrameProvider = _previous;
    }
}

[CollectionDefinition("FrameProvider")]
public sealed class FrameProviderCollection : ICollectionFixture<FrameProviderFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
