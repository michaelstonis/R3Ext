#pragma warning disable SA1512 // Single-line comments should not be followed by blank line
#pragma warning disable SA1413 // Use trailing comma in multi-line initializers
#pragma warning disable SA1503 // Braces should not be omitted
#pragma warning disable SA1629 // Documentation text should end with a period
#pragma warning disable CS8602 // Dereference of a possibly null reference

using System.Globalization;
using R3;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.SampleApp;

/// <summary>
/// Comprehensive showcase of R3Ext binding features:
/// - Deep INPC chains (6+ levels) with automatic rewiring
/// - Null handling - graceful behavior when intermediates are null
/// - Mixed INPC/Plain chains with fallback to polling
/// - Two-way bindings through deep chains
/// - One-way bindings with converters
/// - WhenChanged observables for diagnostics
/// - Instrumentation tracking (NotifyWires, BindUpdates)
/// - Chain replacement and subscription rewiring
/// - Stress testing with rapid updates
/// </summary>
public partial class DeepBindingPage : ContentPage
{
    private readonly SampleViewModel _vm = new();
    private DisposableBag _bindings;

    // Update counters for diagnostics
    private int _deepAUpdates;
    private int _deepBUpdates;
    private int _deepCUpdates;
    private int _deepDUpdates;
    private int _deepEUpdates;
    private int _deepLeafUpdates;
    private int _deepLeafNameUpdates;
    private int _deepLeafValueUpdates;

    private int _mixedRootUpdates;
    private int _mixedPlainUpdates;
    private int _mixedChildUpdates;
    private int _mixedChildNameUpdates;

    private int _nullableRootUpdates;
    private int _nullableIntermediateUpdates;
    private int _nullableTargetNameUpdates;

    public DeepBindingPage()
    {
        this.InitializeComponent();
        this.SetupBindings();
        this.StartInstrumentationPolling();
    }

    private void SetupBindings()
    {
        // ==================== Feature 1: Deep INPC Chain (6 levels) ====================
        // Each level is monitored to show automatic rewiring when intermediates change

        _vm.WhenChanged(v => v.Deep.A)
            .Subscribe(a => UpdateInstanceLabel(DeepALabel, ref _deepAUpdates, a, "A"))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Deep.A.B)
            .Subscribe(b => UpdateInstanceLabel(DeepBLabel, ref _deepBUpdates, b, "B"))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Deep.A.B.C)
            .Subscribe(c => UpdateInstanceLabel(DeepCLabel, ref _deepCUpdates, c, "C"))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Deep.A.B.C.D)
            .Subscribe(d => UpdateInstanceLabel(DeepDLabel, ref _deepDUpdates, d, "D"))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Deep.A.B.C.D.E)
            .Subscribe(e => UpdateInstanceLabel(DeepELabel, ref _deepEUpdates, e, "E"))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Deep.A.B.C.D.E.Leaf)
            .Subscribe(leaf => UpdateInstanceLabel(DeepLeafLabel, ref _deepLeafUpdates, leaf, "Leaf"))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Deep.A.B.C.D.E!.Leaf!.Name)
            .Subscribe(name => UpdateValueLabel(DeepLeafNameLabel, ref _deepLeafNameUpdates, name))
            .AddTo(ref _bindings);

        // Note: Can't use Value property here due to nullable value type issue (int? vs int)
        // Use Name property for demonstration instead
        _vm.WhenChanged(v => v.Deep.A.B.C.D.E!.Leaf!.Name)
            .Subscribe(name => UpdateValueLabel(DeepLeafValueLabel, ref _deepLeafValueUpdates, $"Name: {name}"))
            .AddTo(ref _bindings);

        // ==================== Feature 2: Two-Way Deep Binding ====================
        // Demonstrates two-way binding through 6 levels of chain

        this.BindTwoWay(DeepLeafNameEntry, v => v._vm.Deep.A.B.C.D.E!.Leaf!.Name, e => e.Text)
            .AddTo(ref _bindings);

        this.BindOneWay(DeepLeafNameMirror, v => v._vm.Deep.A.B.C.D.E!.Leaf!.Name, l => l.Text,
            name => $"Mirror: \"{name}\" (updated via two-way binding through 6 levels)")
            .AddTo(ref _bindings);

        // Using Name for "Value" entry to avoid nullable int issue - still demonstrates deep binding
        this.BindTwoWay(DeepLeafValueEntry, v => v._vm.Deep.A.B.C.D.E!.Leaf!.Name, e => e.Text)
            .AddTo(ref _bindings);

        this.BindOneWay(DeepLeafValueMirror, v => v._vm.Deep.A.B.C.D.E!.Leaf!.Name, l => l.Text,
            name => $"Mirror: \"{name}\" (deep chain with nullable intermediates)")
            .AddTo(ref _bindings);

        // ==================== Feature 3: Mixed INPC/Plain Chain ====================
        // INPC Root → Plain Node → INPC Child (tests fallback to polling)

        _vm.WhenChanged(v => v.Mixed)
            .Subscribe(root => UpdateInstanceLabel(MixedRootLabel, ref _mixedRootUpdates, root, "MixedRoot"))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Mixed.PlainNode)
            .Subscribe(plain => UpdateInstanceLabel(MixedPlainLabel, ref _mixedPlainUpdates, plain, "PlainNode"))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Mixed.PlainNode.NotifyChild)
            .Subscribe(child => UpdateInstanceLabel(MixedChildLabel, ref _mixedChildUpdates, child, "NotifyChild"))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.Mixed.PlainNode.NotifyChild!.Name)
            .Subscribe(name => UpdateValueLabel(MixedChildNameLabel, ref _mixedChildNameUpdates, name))
            .AddTo(ref _bindings);

        this.BindTwoWay(MixedChildEntry, v => v._vm.Mixed.PlainNode.NotifyChild!.Name, e => e.Text)
            .AddTo(ref _bindings);

        this.BindOneWay(MixedChildMirror, v => v._vm.Mixed.PlainNode.NotifyChild!.Name, l => l.Text,
            name => $"Mirror: \"{name}\" (INPC→Plain→INPC chain)")
            .AddTo(ref _bindings);

        // ==================== Feature 4: Null Handling ====================
        // Demonstrates graceful null handling without crashes

        _vm.WhenChanged(v => v.NullableChain)
            .Subscribe(root => UpdateInstanceLabel(NullableRootLabel, ref _nullableRootUpdates, root, "NullableRoot"))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.NullableChain.Intermediate)
            .Subscribe(inter => UpdateInstanceLabel(NullableIntermediateLabel, ref _nullableIntermediateUpdates, inter, "Intermediate"))
            .AddTo(ref _bindings);

        _vm.WhenChanged(v => v.NullableChain.Intermediate!.Target!.Name)
            .Subscribe(name => UpdateValueLabel(NullableTargetNameLabel, ref _nullableTargetNameUpdates, name))
            .AddTo(ref _bindings);

        this.BindTwoWay(NullableTargetEntry, v => v._vm.NullableChain.Intermediate!.Target!.Name, e => e.Text)
            .AddTo(ref _bindings);

        this.BindOneWay(NullableTargetMirror, v => v._vm.NullableChain.Intermediate!.Target!.Name, l => l.Text,
            name => $"Mirror: \"{name ?? "(null)"}\" (nullable chain)")
            .AddTo(ref _bindings);

        // ==================== Feature 6: OneWay with Converter ====================
        // Demonstrates value conversion in binding pipeline

        // Demonstrate converter with string transformation instead of int to avoid nullable value type issue
        this.BindOneWay(ConvertedValueLabel, v => v._vm.Deep.A.B.C.D.E!.Leaf!.Name, l => l.Text,
            name => $"🎨 Converted: \"{name}\" → Length: {name?.Length ?? 0} | Upper: {name?.ToUpper() ?? "(null)"}")
            .AddTo(ref _bindings);
    }

    private void StartInstrumentationPolling()
    {
        // Poll instrumentation counters every 100ms for live updates
        Observable.Interval(TimeSpan.FromMilliseconds(100))
            .Subscribe(_ =>
            {
                NotifyWiresLabel.Text = $"{R3ExtGeneratedInstrumentation.NotifyWires} subscriptions";
                BindUpdatesLabel.Text = $"{R3ExtGeneratedInstrumentation.BindUpdates} propagations";
            })
            .AddTo(ref _bindings);
    }

    // ==================== Formatting Helpers ====================

    private static string FormatTimestamp()
    {
        return DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    private static string DescribeInstance(object? value, string typeName)
    {
        if (value is null)
            return "❌ null";

        return $"✅ {typeName} #{value.GetHashCode():X6}";
    }

    private void UpdateInstanceLabel<T>(Label label, ref int counter, T instance, string typeName)
    {
        counter++;
        label.Text = $"[{counter}] {DescribeInstance(instance, typeName)} @ {FormatTimestamp()}";
    }

    private void UpdateValueLabel(Label label, ref int counter, string? value)
    {
        counter++;
        string formatted = string.IsNullOrEmpty(value) ? "❌ (empty)" : $"✅ \"{value}\"";
        label.Text = $"[{counter}] {formatted} @ {FormatTimestamp()}";
    }

    private void UpdateIntLabel(Label label, ref int counter, int value)
    {
        counter++;
        label.Text = $"[{counter}] ✅ {value} @ {FormatTimestamp()}";
    }

    // ==================== Feature 5: Chain Replacement & Rewiring ====================

    private void OnReplaceDeepB(object sender, EventArgs e)
    {
        // Replace B in the chain - all subscriptions to B.C.D.E.Leaf should rewire
        _vm.Deep.A.B = new SampleViewModel.DeepB
        {
            C = new SampleViewModel.DeepC
            {
                D = new SampleViewModel.DeepD
                {
                    E = new SampleViewModel.DeepE
                    {
                        Leaf = new SampleViewModel.DeepLeaf
                        {
                            Name = $"New B at {FormatTimestamp()}",
                            Value = new Random().Next(1000)
                        }
                    }
                }
            }
        };
    }

    private void OnReplaceDeepD(object sender, EventArgs e)
    {
        // Replace D in the chain
        _vm.Deep.A.B.C.D = new SampleViewModel.DeepD
        {
            E = new SampleViewModel.DeepE
            {
                Leaf = new SampleViewModel.DeepLeaf
                {
                    Name = $"New D at {FormatTimestamp()}",
                    Value = new Random().Next(1000)
                }
            }
        };
    }

    private void OnReplaceLeaf(object sender, EventArgs e)
    {
        // Replace just the leaf
        if (_vm.Deep.A.B.C.D.E != null)
        {
            _vm.Deep.A.B.C.D.E.Leaf = new SampleViewModel.DeepLeaf
            {
                Name = $"New Leaf at {FormatTimestamp()}",
                Value = new Random().Next(1000)
            };
        }
    }

    private void OnReplaceMixedRoot(object sender, EventArgs e)
    {
        // Replace entire mixed root
        _vm.Mixed = new SampleViewModel.MixedRoot
        {
            PlainNode = new SampleViewModel.PlainIntermediate
            {
                NotifyChild = new Person { Name = $"New Mixed Root {FormatTimestamp()}" },
                PlainValue = new Random().Next(1000),
            },
        };
    }

    private void OnReplacePlainNode(object sender, EventArgs e)
    {
        // Replace plain intermediate node
        _vm.Mixed.PlainNode = new SampleViewModel.PlainIntermediate
        {
            NotifyChild = new Person { Name = $"New Plain Node {FormatTimestamp()}" },
            PlainValue = new Random().Next(1000),
        };
    }

    // ==================== Feature 4: Null Injection & Recovery ====================

    private void OnSetNullableNull(object sender, EventArgs e)
    {
        // Set intermediate to null - bindings should handle gracefully
        _vm.NullableChain.Intermediate = null;
    }

    private void OnRestoreNullable(object sender, EventArgs e)
    {
        // Restore from null
        _vm.NullableChain.Intermediate = new SampleViewModel.NullableIntermediate
        {
            Target = new Person { Name = $"Restored at {FormatTimestamp()}" },
        };
    }

    // ==================== Feature 7: Instrumentation ====================

    private void OnResetCounters(object sender, EventArgs e)
    {
        R3ExtGeneratedInstrumentation.Reset();

        // Dispose and recreate bindings to see fresh counter values
        _bindings.Dispose();
        _bindings = default;

        // Reset update counters
        _deepAUpdates = _deepBUpdates = _deepCUpdates = _deepDUpdates = _deepEUpdates = 0;
        _deepLeafUpdates = _deepLeafNameUpdates = _deepLeafValueUpdates = 0;
        _mixedRootUpdates = _mixedPlainUpdates = _mixedChildUpdates = _mixedChildNameUpdates = 0;
        _nullableRootUpdates = _nullableIntermediateUpdates = _nullableTargetNameUpdates = 0;

        SetupBindings();
        StartInstrumentationPolling();
    }

    // ==================== Feature 8: Stress Test ====================

    private async void OnStressTest(object sender, EventArgs e)
    {
        StressTestLabel.Text = "Running...";

        DateTime start = DateTime.Now;

        for (int i = 0; i < 100; i++)
        {
            if (_vm.Deep.A.B.C.D.E?.Leaf != null)
            {
                _vm.Deep.A.B.C.D.E.Leaf.Name = $"Stress {i}";
                _vm.Deep.A.B.C.D.E.Leaf.Value = i;
            }

            // Small delay to allow UI updates
            if (i % 10 == 0)
                await Task.Delay(1);
        }

        TimeSpan elapsed = DateTime.Now - start;
        StressTestLabel.Text = $"✅ 100 updates in {elapsed.TotalMilliseconds:F1}ms";
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _bindings.Dispose();
    }
}
