using System.Threading;

namespace R3Ext;

public sealed class InteractionContext<TInput, TOutput> : IOutputContext<TInput, TOutput>
{
    private TOutput _output = default!;
    private int _outputSet;

    internal InteractionContext(TInput input)
    {
        Input = input;
    }

    public TInput Input { get; }

    public bool IsHandled => _outputSet == 1;

    public void SetOutput(TOutput output)
    {
        if (Interlocked.CompareExchange(ref _outputSet, 1, 0) != 0)
        {
            throw new InvalidOperationException("Output has already been set.");
        }
        _output = output;
    }

    public TOutput GetOutput()
    {
        if (_outputSet == 0)
        {
            throw new InvalidOperationException("Output has not been set.");
        }
        return _output;
    }
}
