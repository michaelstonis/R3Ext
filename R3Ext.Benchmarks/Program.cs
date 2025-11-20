using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Engines;
using ReactiveUI; // for IViewFor and UI bindings
using R3Ext; // R3Ext binding extensions
using System.ComponentModel;
using Moqs = R3Ext.Benchmarks.Moqs;

// Fast benchmark configuration to cut down warmup/pilot time
public sealed class FastConfig : ManualConfig
{
    public FastConfig()
    {
        AddJob(Job.Default
            .WithRuntime(CoreRuntime.Core90)
            .WithLaunchCount(1)
            .WithWarmupCount(1)
            .WithIterationCount(5));
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        var config = new FastConfig();
        BenchmarkSwitcher.FromTypes(new[] { typeof(BindingBenchmarks), typeof(CrossFrameworkBindBenchmarks) })
            .Run(args, config);
    }
}

public class HostLeaf : INotifyPropertyChanged
{
    private int _value;

    public int Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class HostNested : INotifyPropertyChanged
{
    private HostLeaf _leaf = new();

    public HostLeaf Leaf
    {
        get => _leaf;
        set
        {
            if (!ReferenceEquals(_leaf, value))
            {
                _leaf = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Leaf)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class TargetLeaf : INotifyPropertyChanged
{
    private int _value;

    public int Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class TargetNested : INotifyPropertyChanged
{
    private TargetLeaf _leaf = new();

    public TargetLeaf Leaf
    {
        get => _leaf;
        set
        {
            if (!ReferenceEquals(_leaf, value))
            {
                _leaf = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Leaf)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

[MemoryDiagnoser]
public class BindingBenchmarks
{
    private readonly HostLeaf _hostLeaf = new();
    private readonly TargetLeaf _targetLeaf = new();
    private readonly HostNested _hostNested = new();
    private readonly TargetNested _targetNested = new();
    private IDisposable _specOneWay = default!;
    private IDisposable _specTwoWay = default!;
    private IDisposable _fallbackOneWay = default!;
    private IDisposable _fallbackTwoWay = default!;

    [GlobalSetup]
    public void Setup()
    {
        _specOneWay = _hostLeaf.BindOneWay(_targetLeaf, h => h.Value, t => t.Value, v => v);
        _specTwoWay = _hostLeaf.BindTwoWay(_targetLeaf, h => h.Value, t => t.Value, v => v, v => v);
        _fallbackOneWay = _hostNested.BindOneWay(_targetLeaf, h => h.Leaf.Value, t => t.Value, v => v);
        _fallbackTwoWay = _hostNested.BindTwoWay(_targetNested, h => h.Leaf.Value, t => t.Leaf.Value, v => v, v => v);
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


// Cross-framework binding benchmarks modeled after ReactiveMarbles' BindBenchmarks
// Compares: ReactiveUI.Bind (UI), ReactiveMarbles.PropertyChanged.BindTwoWay (PC), and R3Ext.BindTwoWay (R3)
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[MemoryDiagnoser]
public class CrossFrameworkBindBenchmarks
{
    private Moqs.TestClass _from = default!;
    private Moqs.TestClass _to = default!;
    private IDisposable _binding = default!;

    [Params(1, 2)]
    public int Depth { get; set; }

    [Params(1, 10)]
    public int Changes { get; set; }

    [GlobalSetup(Targets = new[]
    {
        nameof(BindAndChange_UI),
        nameof(BindAndChange_PC),
        nameof(BindAndChange_R3),
    })]
    public void BindAndChangeSetup()
    {
        _from = new Moqs.TestClass(Depth);
        _to = new Moqs.TestClass(Depth);
    }

    // (Removed generic ChangeOnlySetup; per-target setups initialize state)

    [GlobalCleanup(Targets = new[] { nameof(ChangeOnly_UI), nameof(ChangeOnly_PC), nameof(ChangeOnly_R3) })]
    public void ChangeOnlyCleanup()
    {
        _binding?.Dispose();
    }

    private void PerformMutations()
    {
        // Alternate mutations between source and destination across depths, similar to RM benchmarks
        var d2 = Depth * 2;
        for (var i = 0; i < Changes; ++i)
        {
            var a = i % d2;
            var target = (a % 2) > 0 ? _to : _from;
            target.Mutate(a / 2);
        }
    }

    [Benchmark(Description = "UI Bind + Change")]
    [BenchmarkCategory("BindAndChange")]
    public void BindAndChange_UI()
    {
        using var binding = BindUI();
        PerformMutations();
    }

    [Benchmark(Description = "PC BindTwoWay + Change")]
    [BenchmarkCategory("BindAndChange")]
    public void BindAndChange_PC()
    {
        using var binding = BindPC();
        PerformMutations();
    }

    [Benchmark(Description = "R3 BindTwoWay + Change")]
    [BenchmarkCategory("BindAndChange")]
    public void BindAndChange_R3()
    {
        using var binding = BindR3();
        PerformMutations();
    }

    [GlobalSetup(Target = nameof(ChangeOnly_UI))]
    public void ChangeOnly_UISetup()
    {
        _from = new Moqs.TestClass(Depth);
        _to = new Moqs.TestClass(Depth);
        _binding?.Dispose();
        _binding = BindUI();
    }

    [GlobalSetup(Target = nameof(ChangeOnly_PC))]
    public void ChangeOnly_PCSetup()
    {
        _from = new Moqs.TestClass(Depth);
        _to = new Moqs.TestClass(Depth);
        _binding?.Dispose();
        _binding = BindPC();
    }

    [GlobalSetup(Target = nameof(ChangeOnly_R3))]
    public void ChangeOnly_R3Setup()
    {
        _from = new Moqs.TestClass(Depth);
        _to = new Moqs.TestClass(Depth);
        _binding?.Dispose();
        _binding = BindR3();
    }

    [Benchmark(Description = "UI Change only")]
    [BenchmarkCategory("ChangeOnly")]
    public void ChangeOnly_UI() => PerformMutations();

    [Benchmark(Description = "PC Change only")]
    [BenchmarkCategory("ChangeOnly")]
    public void ChangeOnly_PC() => PerformMutations();

    [Benchmark(Baseline = true, Description = "R3 Change only")]
    [BenchmarkCategory("ChangeOnly")]
    public void ChangeOnly_R3() => PerformMutations();

    private IDisposable BindUI()
    {
        return Depth switch
        {
            1 => ReactiveUI.PropertyBindingMixins.Bind(_from, _to, vm => vm.Value, v => v.Value),
            2 => ReactiveUI.PropertyBindingMixins.Bind(_from, _to, vm => vm.Child!.Value, v => v.Child!.Value),
            _ => ReactiveUI.PropertyBindingMixins.Bind(_from, _to, vm => vm.Child!.Child!.Value, v => v.Child!.Child!.Value),
        };
    }

    private IDisposable BindPC()
    {
        return Depth switch
        {
            1 => ReactiveMarbles.PropertyChanged.BindExtensions.BindTwoWay(_from, _to, vm => vm.Value, v => v.Value),
            2 => ReactiveMarbles.PropertyChanged.BindExtensions.BindTwoWay(_from, _to, vm => vm.Child!.Value, v => v.Child!.Value),
            _ => ReactiveMarbles.PropertyChanged.BindExtensions.BindTwoWay(_from, _to, vm => vm.Child!.Child!.Value, v => v.Child!.Child!.Value),
        };
    }

    private IDisposable BindR3()
    {
        return Depth switch
        {
            1 => _from.BindTwoWay(_to, vm => vm.Value, v => v.Value),
            2 => _from.BindTwoWay(_to, vm => vm.Child!.Value, v => v.Child!.Value),
            _ => _from.BindTwoWay(_to, vm => vm.Child!.Child!.Value, v => v.Child!.Child!.Value),
        };
    }
}
