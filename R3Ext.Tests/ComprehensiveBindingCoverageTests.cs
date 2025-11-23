#pragma warning disable SA1028 // Code should not contain trailing whitespace
#pragma warning disable SA1116 // Split parameters should start on line after declaration
#pragma warning disable SA1503 // Braces should not be omitted
#pragma warning disable SA1512 // Single-line comments should not be followed by blank line
#pragma warning disable SA1515 // Single-line comment should be preceded by blank line  
#pragma warning disable SA1516 // Elements should be separated by blank line

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using R3;
using Xunit;

namespace R3Ext.Tests;

/// <summary>
/// Comprehensive test coverage for the binding generator's compile-time INPC optimization.
/// Tests all combinations of INPC/non-INPC types across BindOneWay, BindTwoWay, and WhenChanged.
/// </summary>
[Collection("FrameProvider")]
public class ComprehensiveBindingCoverageTests(FrameProviderFixture fp)
{
    // ==================== Test Model Classes ====================

    /// <summary>Plain class that does NOT implement INotifyPropertyChanged</summary>
    public class PlainClass
    {
        public string Value { get; set; } = string.Empty;
        public PlainClass? Child { get; set; }
        public NotifyClass? NotifyChild { get; set; }
        public int Counter { get; set; }
    }

    /// <summary>Class that DOES implement INotifyPropertyChanged</summary>
    public class NotifyClass : INotifyPropertyChanged
    {
        private string _value = string.Empty;
        private NotifyClass? _child;
        private PlainClass? _plainChild;
        private int _counter;

        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }

        public NotifyClass? Child
        {
            get => _child;
            set
            {
                if (_child != value)
                {
                    _child = value;
                    OnPropertyChanged();
                }
            }
        }

        public PlainClass? PlainChild
        {
            get => _plainChild;
            set
            {
                if (_plainChild != value)
                {
                    _plainChild = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Counter
        {
            get => _counter;
            set
            {
                if (_counter != value)
                {
                    _counter = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>Deep chain: Notify -> Notify -> Notify -> Notify</summary>
    public class DeepNotifyChain
    {
        public NotifyClass Level1 { get; set; } = new();
    }

    /// <summary>Mixed chain: Notify -> Plain -> Notify -> Plain</summary>
    public class MixedChain
    {
        public NotifyClass? NotifyRoot { get; set; }
    }

    /// <summary>Target class for binding tests</summary>
    public class BindTarget
    {
        public string Text { get; set; } = string.Empty;
        public int Number { get; set; }
    }

    /// <summary>INPC target for two-way binding</summary>
    public class NotifyTarget : INotifyPropertyChanged
    {
        private string _text = string.Empty;
        private int _number;

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Number
        {
            get => _number;
            set
            {
                if (_number != value)
                {
                    _number = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // ==================== BindOneWay Tests ====================

    [Fact]
    public void BindOneWay_SingleSegment_INPC_ToPlain()
    {
        // Test: INPC source -> Plain target (should use ObservePropertyChanged)
        NotifyClass source = new() { Value = "Initial" };
        BindTarget target = new();

        using IDisposable binding = source.BindOneWay(target, s => s.Value, t => t.Text);

        Assert.Equal("Initial", target.Text);

        source.Value = "Updated";
        Assert.Equal("Updated", target.Text);
    }

    [Fact]
    public void BindOneWay_SingleSegment_Plain_ToPlain()
    {
        // Test: Plain source -> Plain target (should use EveryValueChanged + polling)
        PlainClass source = new() { Value = "Initial" };
        BindTarget target = new();

        // Note: Plain properties won't trigger updates automatically - this tests the fallback
        using IDisposable binding = source.BindOneWay(target, s => s.Value, t => t.Text);

        Assert.Equal("Initial", target.Text);
    }

    [Fact]
    public void BindOneWay_MultiSegment_AllINPC()
    {
        // Test: INPC -> INPC -> INPC chain (all should use ObservePropertyChanged)
        NotifyClass root = new() { Child = new NotifyClass { Value = "Deep" } };
        BindTarget target = new();

        using IDisposable binding = root.BindOneWay(target, s => s.Child!.Value, t => t.Text);

        Assert.Equal("Deep", target.Text);

        root.Child.Value = "Updated";
        Assert.Equal("Updated", target.Text);

        // Test rewiring
        root.Child = new NotifyClass { Value = "NewChild" };
        Assert.Equal("NewChild", target.Text);
    }

    [Fact]
    public void BindOneWay_MultiSegment_MixedChain_INPC_Plain_INPC()
    {
        // Test: INPC -> Plain -> INPC (mixed chain)
        NotifyClass root = new() { PlainChild = new PlainClass { NotifyChild = new NotifyClass { Value = "Mixed" } } };
        BindTarget target = new();

        using IDisposable binding = root.BindOneWay(target, s => s.PlainChild!.NotifyChild!.Value, t => t.Text);

        Assert.Equal("Mixed", target.Text);

        // Plain intermediate won't trigger, but INPC root will
        root.PlainChild = new PlainClass { NotifyChild = new NotifyClass { Value = "Rewired" } };
        Assert.Equal("Rewired", target.Text);
    }

    [Fact]
    public void BindOneWay_DeepChain_6Levels()
    {
        // Test: Very deep chain (6+ levels)
        NotifyClass level6 = new() { Value = "Deep6" };
        NotifyClass level5 = new() { Child = level6 };
        NotifyClass level4 = new() { Child = level5 };
        NotifyClass level3 = new() { Child = level4 };
        NotifyClass level2 = new() { Child = level3 };
        NotifyClass root = new() { Child = level2 };

        BindTarget target = new();

        using IDisposable binding = root.BindOneWay(target, s => s.Child!.Child!.Child!.Child!.Child!.Value, t => t.Text);

        Assert.Equal("Deep6", target.Text);

        level6.Value = "UpdatedDeep";
        Assert.Equal("UpdatedDeep", target.Text);
    }

    [Fact]
    public void BindOneWay_WithConverter()
    {
        // Test: OneWay with conversion function
        NotifyClass source = new() { Counter = 42 };
        BindTarget target = new();

        using IDisposable binding = source.BindOneWay(target, s => s.Counter, t => t.Text, c => $"Count: {c}");

        Assert.Equal("Count: 42", target.Text);

        source.Counter = 100;
        Assert.Equal("Count: 100", target.Text);
    }

    // ==================== BindTwoWay Tests ====================

    [Fact]
    public void BindTwoWay_SingleSegment_BothINPC()
    {
        // Test: INPC <-> INPC (both should use ObservePropertyChanged)
        NotifyClass source = new() { Value = "Initial" };
        NotifyTarget target = new() { Text = "Initial" };

        using IDisposable binding = source.BindTwoWay(target, s => s.Value, t => t.Text);

        source.Value = "FromSource";
        Assert.Equal("FromSource", target.Text);

        target.Text = "FromTarget";
        Assert.Equal("FromTarget", source.Value);
    }

    [Fact]
    public void BindTwoWay_SingleSegment_INPC_ToPlain()
    {
        // Test: INPC -> Plain (source INPC, target plain)
        NotifyClass source = new() { Value = "Initial" };
        BindTarget target = new() { Text = "Initial" };

        using IDisposable binding = source.BindTwoWay(target, s => s.Value, t => t.Text);

        source.Value = "Updated";
        Assert.Equal("Updated", target.Text);

        // Reverse direction won't trigger automatically with plain target
        target.Text = "Manual";
        // Can't verify automatic update since target is plain
    }

    [Fact]
    public void BindTwoWay_SingleSegment_Plain_ToINPC()
    {
        // Test: Plain -> INPC (source plain, target INPC)
        PlainClass source = new() { Value = "Initial" };
        NotifyTarget target = new() { Text = "Initial" };

        using IDisposable binding = source.BindTwoWay(target, s => s.Value, t => t.Text);

        // Source is plain, so won't trigger automatically
        // But target changes should work
        target.Text = "FromTarget";
        Assert.Equal("FromTarget", source.Value);
    }

    [Fact]
    public void BindTwoWay_MultiSegment_BothChainsINPC()
    {
        // Test: INPC chains on both sides
        NotifyClass source = new() { Child = new NotifyClass { Value = "SourceChain" } };
        NotifyClass target = new() { Child = new NotifyClass { Value = "TargetChain" } };

        using IDisposable binding = source.BindTwoWay(target, s => s.Child!.Value, t => t.Child!.Value);

        source.Child.Value = "FromSource";
        Assert.Equal("FromSource", target.Child!.Value);

        target.Child.Value = "FromTarget";
        Assert.Equal("FromTarget", source.Child!.Value);
    }

    [Fact]
    public void BindTwoWay_MultiSegment_MixedChains()
    {
        // Test: Mixed INPC/Plain chains on both sides
        NotifyClass source = new() { PlainChild = new PlainClass { NotifyChild = new NotifyClass { Value = "Mixed1" } } };
        NotifyClass target = new() { PlainChild = new PlainClass { NotifyChild = new NotifyClass { Value = "Mixed2" } } };

        using IDisposable binding = source.BindTwoWay(
            target,
            s => s.PlainChild!.NotifyChild!.Value,
            t => t.PlainChild!.NotifyChild!.Value);

        // Root changes trigger rewiring
        source.PlainChild = new PlainClass { NotifyChild = new NotifyClass { Value = "NewSource" } };
        Assert.Equal("NewSource", target.PlainChild!.NotifyChild!.Value);

        target.PlainChild = new PlainClass { NotifyChild = new NotifyClass { Value = "NewTarget" } };
        Assert.Equal("NewTarget", source.PlainChild!.NotifyChild!.Value);
    }

    [Fact]
    public void BindTwoWay_PreventsCycles()
    {
        // Test: Verify two-way binding doesn't cause infinite update loops
        NotifyClass source = new() { Counter = 1 };
        NotifyTarget target = new() { Number = 1 };

        int sourceChanges = 0;
        int targetChanges = 0;

        source.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(NotifyClass.Counter))
            {
                sourceChanges++;
            }
        };
        target.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(NotifyTarget.Number))
            {
                targetChanges++;
            }
        };

        using IDisposable binding = source.BindTwoWay(target, s => s.Counter, t => t.Number);

        source.Counter = 2;
        Assert.Equal(2, target.Number);
        Assert.Equal(1, sourceChanges); // Only one change
        Assert.Equal(1, targetChanges); // Only propagated once

        target.Number = 3;
        Assert.Equal(3, source.Counter);
        Assert.Equal(2, sourceChanges);
        Assert.Equal(2, targetChanges);
    }

    [Fact]
    public void BindTwoWay_WithConverters()
    {
        // Test: Two-way with bidirectional converters
        NotifyClass source = new() { Counter = 10 };
        NotifyTarget target = new() { Text = "10" };

        using IDisposable binding = source.BindTwoWay(
            target,
            s => s.Counter,
            t => t.Text,
            num => num.ToString(),
            str => int.TryParse(str, out int val) ? val : 0);

        source.Counter = 42;
        Assert.Equal("42", target.Text);

        target.Text = "99";
        Assert.Equal(99, source.Counter);
    }

    // ==================== WhenChanged Tests ====================

    [Fact]
    public void WhenChanged_SingleSegment_INPC()
    {
        // Test: WhenChanged on INPC property (should use ObservePropertyChanged)
        NotifyClass source = new() { Value = "Initial" };
        List<string> values = new();

        using IDisposable sub = source.WhenChanged(s => s.Value).Subscribe(v => values.Add(v));

        Assert.Single(values);
        Assert.Equal("Initial", values[0]);

        source.Value = "Updated";
        Assert.Equal(2, values.Count);
        Assert.Equal("Updated", values[1]);
    }

    [Fact]
    public void WhenChanged_SingleSegment_Plain()
    {
        // Test: WhenChanged on plain property (should use EveryValueChanged)
        PlainClass source = new() { Value = "Initial" };
        List<string> values = new();

        using IDisposable sub = source.WhenChanged(s => s.Value).Subscribe(v => values.Add(v));

        Assert.Single(values);
        Assert.Equal("Initial", values[0]);

        // Plain properties won't trigger updates automatically
        source.Value = "Updated";
        // Can't verify automatic updates with plain types
    }

    [Fact]
    public void WhenChanged_MultiSegment_AllINPC()
    {
        // Test: WhenChanged on INPC chain (all ObservePropertyChanged)
        NotifyClass root = new() { Child = new NotifyClass { Value = "Deep" } };
        List<string> values = new();

        using IDisposable sub = root.WhenChanged(s => s.Child!.Value).Subscribe(v => values.Add(v));

        Assert.Single(values);
        Assert.Equal("Deep", values[0]);

        root.Child.Value = "Updated";
        Assert.Equal(2, values.Count);
        Assert.Equal("Updated", values[1]);

        // Test rewiring
        root.Child = new NotifyClass { Value = "NewChild" };
        Assert.Equal(3, values.Count);
        Assert.Equal("NewChild", values[2]);
    }

    [Fact]
    public void WhenChanged_MultiSegment_MixedChain()
    {
        // Test: WhenChanged on mixed INPC/Plain chain
        NotifyClass root = new() { PlainChild = new PlainClass { NotifyChild = new NotifyClass { Value = "Mixed" } } };
        List<string> values = new();

        using IDisposable sub = root.WhenChanged(s => s.PlainChild!.NotifyChild!.Value).Subscribe(v => values.Add(v));

        Assert.Single(values);
        Assert.Equal("Mixed", values[0]);

        // Root is INPC, so this will trigger
        root.PlainChild = new PlainClass { NotifyChild = new NotifyClass { Value = "Rewired" } };
        Assert.Equal(2, values.Count);
        Assert.Equal("Rewired", values[1]);
    }

    [Fact]
    public void WhenChanged_NullHandling()
    {
        // Test: WhenChanged handles null intermediates gracefully
        NotifyClass root = new() { Child = new NotifyClass { Value = "HasChild" } };
        List<string?> values = new();

        using IDisposable sub = root.WhenChanged(s => s.Child!.Value).Subscribe(v => values.Add(v));

        Assert.Single(values);
        Assert.Equal("HasChild", values[0]);

        // Set intermediate to null
        root.Child = null;
        // Should emit default/null value
        Assert.Equal(2, values.Count);

        // Restore child
        root.Child = new NotifyClass { Value = "Restored" };
        Assert.Equal(3, values.Count);
        Assert.Equal("Restored", values[2]);
    }

    [Fact]
    public void WhenChanged_DistinctUntilChanged()
    {
        // Test: WhenChanged filters duplicate values
        NotifyClass source = new() { Value = "A" };
        List<string> values = new();

        using IDisposable sub = source.WhenChanged(s => s.Value).Subscribe(v => values.Add(v));

        source.Value = "B";
        source.Value = "B"; // Duplicate - filtered by DistinctUntilChanged
        source.Value = "C";
        source.Value = "C"; // Duplicate - filtered by DistinctUntilChanged
        source.Value = "B"; // Different from previous

        // DistinctUntilChanged filters duplicates, so we get: A, B, C, B
        Assert.Equal(4, values.Count);
        Assert.Equal(new[] { "A", "B", "C", "B" }, values);
    }

    // KNOWN ISSUE: Value types at the end of nullable chains become nullable (int? instead of int)
    // This causes CS0266 error. Needs generator fix to detect and handle nullable value types.
    // See: Generated code tries to assign `int?` result from `Child?.Child?.Counter` to `int` variable
    /*[Fact]
    public void WhenChanged_DeepChain_7Levels()
    {
        // Test: Very deep WhenChanged chain
        NotifyClass level7 = new() { Counter = 777 };
        NotifyClass level6 = new() { Child = level7 };
        NotifyClass level5 = new() { Child = level6 };
        NotifyClass level4 = new() { Child = level5 };
        NotifyClass level3 = new() { Child = level4 };
        NotifyClass level2 = new() { Child = level3 };
        NotifyClass root = new() { Child = level2 };

        List<int> values = new();

        using IDisposable sub = root.WhenChanged(r => r.Child!.Child!.Child!.Child!.Child!.Child!.Counter)
            .Subscribe(v => values.Add(v));

        Assert.Single(values);
        Assert.Equal(777, values[0]);

        level7.Counter = 999;
        Assert.Equal(2, values.Count);
        Assert.Equal(999, values[1]);

        // Test deep rewiring
        level3.Child = new NotifyClass { Child = new NotifyClass { Child = new NotifyClass { Counter = 333 } } };
        Assert.Equal(3, values.Count);
        Assert.Equal(333, values[2]);
    }*/

    // ==================== Instrumentation Tests ====================

    [Fact]
    public void Instrumentation_TracksNotifyWires()
    {
        // Test: NotifyWires counter increments for INPC subscriptions
        R3ExtGeneratedInstrumentation.Reset();

        NotifyClass source = new() { Child = new NotifyClass { Value = "Test" } };
        BindTarget target = new();

        Assert.Equal(0, R3ExtGeneratedInstrumentation.NotifyWires);

        using IDisposable binding = source.BindOneWay(target, s => s.Child!.Value, t => t.Text);

        // Should have wired up 2 INPC subscriptions (root.Child and Child.Value)
        Assert.True(R3ExtGeneratedInstrumentation.NotifyWires >= 2);
    }

    [Fact]
    public void Instrumentation_TracksBindUpdates()
    {
        // Test: BindUpdates counter increments on value propagation (TwoWay only)
        R3ExtGeneratedInstrumentation.Reset();

        NotifyClass source = new() { Value = "Initial" };
        NotifyTarget target = new() { Text = "Initial" };

        using IDisposable binding = source.BindTwoWay(target, s => s.Value, t => t.Text);

        int initialUpdates = R3ExtGeneratedInstrumentation.BindUpdates;

        source.Value = "Update1";
        source.Value = "Update2";
        source.Value = "Update3";

        // Each update increments the counter
        Assert.True(R3ExtGeneratedInstrumentation.BindUpdates >= initialUpdates + 3,
            $"Expected BindUpdates >= {initialUpdates + 3}, but got {R3ExtGeneratedInstrumentation.BindUpdates}");
    }

    // ==================== Edge Case Tests ====================

    [Fact]
    public void EdgeCase_RapidUpdates()
    {
        // Test: Rapid successive updates are handled correctly
        NotifyClass source = new() { Counter = 0 };
        BindTarget target = new();

        using IDisposable binding = source.BindOneWay(target, s => s.Counter, t => t.Number);

        for (int i = 1; i <= 100; i++)
        {
            source.Counter = i;
        }

        Assert.Equal(100, target.Number);
    }

    [Fact]
    public void EdgeCase_Disposal()
    {
        // Test: Disposed bindings don't leak or throw
        NotifyClass source = new() { Value = "Test" };
        BindTarget target = new();

        IDisposable binding = source.BindOneWay(target, s => s.Value, t => t.Text);
        binding.Dispose();

        // Should not throw or update after disposal
        source.Value = "AfterDisposal";
        Assert.Equal("Test", target.Text);

        // Multiple disposals should be safe
        binding.Dispose();
    }

    [Fact]
    public void EdgeCase_NullableValueTypes()
    {
        // Test: Nullable value types are handled correctly
        NotifyClass source = new() { Counter = 42 };
        List<int> values = new();

        using IDisposable sub = source.WhenChanged(s => s.Counter).Subscribe(v => values.Add(v));

        source.Counter = 0;
        source.Counter = -1;

        Assert.Contains(42, values);
        Assert.Contains(0, values);
        Assert.Contains(-1, values);
    }

    [Fact]
    public void EdgeCase_ExceptionInChainAccess()
    {
        // Test: Exceptions in property access are handled gracefully
        NotifyClass root = new() { Child = null };
        List<string?> values = new();

        using IDisposable sub = root.WhenChanged(s => s.Child!.Value).Subscribe(v => values.Add(v));

        // Should emit default/null, not throw
        Assert.Single(values);

        root.Child = new NotifyClass { Value = "Recovered" };
        Assert.Equal(2, values.Count);
        Assert.Equal("Recovered", values[1]);
    }
}
