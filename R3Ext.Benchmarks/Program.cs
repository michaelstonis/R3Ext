using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.ComponentModel;
using R3Ext; // binding extensions

// other extensions

BenchmarkRunner.Run<BindingBenchmarks>();

public class HostLeaf : INotifyPropertyChanged
{
    private int _value;
    public int Value { get => _value; set { if (_value!=value){ _value=value; PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Value))); } } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
public class HostNested : INotifyPropertyChanged
{
    private HostLeaf _leaf = new();
    public HostLeaf Leaf { get => _leaf; set { if(!ReferenceEquals(_leaf,value)){ _leaf=value; PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Leaf))); } } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
public class TargetLeaf : INotifyPropertyChanged
{
    private int _value;
    public int Value { get => _value; set { if (_value!=value){ _value=value; PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Value))); } } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
public class TargetNested : INotifyPropertyChanged
{
    private TargetLeaf _leaf = new();
    public TargetLeaf Leaf { get => _leaf; set { if(!ReferenceEquals(_leaf,value)){ _leaf=value; PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Leaf))); } } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

[MemoryDiagnoser]
public class BindingBenchmarks
{
    private HostLeaf _hostLeaf = new();
    private TargetLeaf _targetLeaf = new();
    private HostNested _hostNested = new();
    private TargetNested _targetNested = new();
    private IDisposable _specOneWay = default!;
    private IDisposable _specTwoWay = default!;
    private IDisposable _fallbackOneWay = default!;
    private IDisposable _fallbackTwoWay = default!;

    [GlobalSetup]
    public void Setup()
    {
        _specOneWay = _hostLeaf.BindOneWay(_targetLeaf, h=>h.Value, t=>t.Value, v=>v);
        _specTwoWay = _hostLeaf.BindTwoWay(_targetLeaf, h=>h.Value, t=>t.Value, v=>v, v=>v);
        _fallbackOneWay = _hostNested.BindOneWay(_targetLeaf, h=>h.Leaf.Value, t=>t.Value, v=>v);
        _fallbackTwoWay = _hostNested.BindTwoWay(_targetNested, h=>h.Leaf.Value, t=>t.Leaf.Value, v=>v, v=>v);
    }
    [GlobalCleanup]
    public void Cleanup()
    {
        _specOneWay.Dispose();
        _specTwoWay.Dispose();
        _fallbackOneWay.Dispose();
        _fallbackTwoWay.Dispose();
    }

    [Benchmark]
    public void Specialized_OneWay_Update()
    {
        _hostLeaf.Value++;
    }
    [Benchmark]
    public void Specialized_TwoWay_UpdateHost()
    {
        _hostLeaf.Value++;
    }
    [Benchmark]
    public void Specialized_TwoWay_UpdateTarget()
    {
        _targetLeaf.Value++;
    }
    [Benchmark]
    public void Fallback_OneWay_Update()
    {
        _hostNested.Leaf.Value++;
    }
    [Benchmark]
    public void Fallback_TwoWay_UpdateHost()
    {
        _hostNested.Leaf.Value++;
    }
    [Benchmark]
    public void Fallback_TwoWay_UpdateTarget()
    {
        _targetNested.Leaf.Value++;
    }
}