using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Xunit;
using R3Ext;
// internal types + instrumentation
using R3; // Observable extension subscription overloads

namespace R3Ext.Tests;

public class BindingGeneratorExtendedTests
{
    [Fact]
    public void InternalChain_WhenChanged_WiresAndRewires()
    {
        var root = new InternalRoot { Mid = new InternalMid { Leaf = new InternalLeaf { Name = "A" } } };
        var values = new List<string>();
        using var d = root.WhenChanged(r => r.Mid.Leaf.Name).Subscribe(v => values.Add(v ?? "<null>"));
        Assert.Equal("A", values.Last());
        root.Mid.Leaf.Name = "B"; Assert.Equal("B", values.Last());
        root.Mid.Leaf = new InternalLeaf { Name = "C" }; Assert.Equal("C", values.Last());
        root.Mid = new InternalMid { Leaf = new InternalLeaf { Name = "D" } }; Assert.Equal("D", values.Last());
        root.Mid.Leaf.Name = "E"; Assert.Equal("E", values.Last());
    }

    [Fact]
    public void TwoWayBinding_NoCycles_WithReentrancyGuard()
    {
        var host = new CycleHost { Value = 1 };
        var target = new CycleTarget { Value = 1 };
        using var disp = host.BindTwoWay(target, h => h.Value, t => t.Value, v => v, v => v);
        host.Value = 2; Assert.Equal(2, target.Value); Assert.Equal(2, host.Value);
        target.Value = 3; Assert.Equal(3, host.Value); Assert.Equal(3, target.Value);
        // Change again rapidly
        host.Value = 4; target.Value = 5; host.Value = 6;
        Assert.Equal(6, host.Value); Assert.Equal(6, target.Value);
    }

    [Fact]
    public void Instrumentation_Counters_Increase()
    {
        R3ExtGeneratedInstrumentation.Reset();
        var host = new CycleHost { Value = 10 };
        var target = new CycleTarget { Value = 10 };
        using var disp = host.BindTwoWay(target, h => h.Value, t => t.Value, v => v, v => v);
        host.Value = 11; target.Value = 12; host.Value = 13;
        Assert.True(R3ExtGeneratedInstrumentation.BindUpdates >= 3);
        Assert.True(R3ExtGeneratedInstrumentation.NotifyWires >= 2);
    }

    [Fact]
    public void WhenChanged_NullIntermediate_Recover()
    {
        var root = new InternalRoot { Mid = new InternalMid { Leaf = new InternalLeaf { Name = "X" } } };
        var values = new List<string>();
        using var d = root.WhenChanged(r => r.Mid.Leaf.Name).Subscribe(v => values.Add(v ?? "<null>"));
        Assert.Equal("X", values.Last());
        root.Mid.Leaf = null!; // simulate null break (will stop emissions)
        root.Mid.Leaf = new InternalLeaf { Name = "Y" }; Assert.Equal("Y", values.Last());
    }

    // Support classes for two-way cycle test
    internal sealed class CycleHost : INotifyPropertyChanged
    {
        private int _value;
        public int Value { get => _value; set { if (_value == value) return; _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    internal sealed class CycleTarget : INotifyPropertyChanged
    {
        private int _value;
        public int Value { get => _value; set { if (_value == value) return; _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}