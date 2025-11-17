using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Xunit;
using R3Ext;
using R3; // Observable and Subscribe helpers

namespace R3Ext.Tests;

[Collection("FrameProvider")]
public class MixedAndDeepBindingTests
{
    private readonly FrameProviderFixture _fp;
    public MixedAndDeepBindingTests(FrameProviderFixture fp) => _fp = fp;
    

    // Deep chain types (all notify)
    internal sealed class DLeaf : INotifyPropertyChanged
    {
        string _name = string.Empty;
        public string Name { get => _name; set { if (_name == value) return; _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    internal sealed class DD : INotifyPropertyChanged
    {
        DLeaf _leaf = new();
        public DLeaf Leaf { get => _leaf; set { if (_leaf == value) return; _leaf = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Leaf))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    internal sealed class DC : INotifyPropertyChanged
    {
        DD _d = new();
        public DD D { get => _d; set { if (_d == value) return; _d = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(D))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    internal sealed class DB : INotifyPropertyChanged
    {
        DC _c = new();
        public DC C { get => _c; set { if (_c == value) return; _c = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(C))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    internal sealed class DA : INotifyPropertyChanged
    {
        DB _b = new();
        public DB B { get => _b; set { if (_b == value) return; _b = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(B))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    internal sealed class DRoot : INotifyPropertyChanged
    {
        DA _a = new();
        public DA A { get => _a; set { if (_a == value) return; _a = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(A))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [Fact]
    public void Deep_WhenChanged_Rewires_Through_Five_Levels()
    {
        var root = new DRoot();
        root.A.B.C.D.Leaf.Name = "L0";
        var vals = new List<string>();
        using var d = root.WhenChanged(r => r.A.B.C.D.Leaf.Name).Subscribe(v => vals.Add(v ?? "<null>"));
        Assert.Equal("L0", vals.Last());
        root.A.B.C.D.Leaf.Name = "L1"; Assert.Equal("L1", vals.Last());
        root.A.B.C.D.Leaf = new DLeaf { Name = "L2" }; Assert.Equal("L2", vals.Last());
        root.A.B.C.D = new DD { Leaf = new DLeaf { Name = "L3" } }; Assert.Equal("L3", vals.Last());
        root.A.B = new DB { C = new DC { D = new DD { Leaf = new DLeaf { Name = "L4" } } } }; Assert.Equal("L4", vals.Last());
    }

    // Mixed chain: Non-notify parent
    internal sealed class Person : INotifyPropertyChanged
    {
        string _name = string.Empty;
        public string Name { get => _name; set { if (_name == value) return; _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    internal sealed class NonNotifyParent // does not implement INotifyPropertyChanged
    {
        public Person Child { get; set; } = new Person();
    }
    internal sealed class MRoot : INotifyPropertyChanged
    {
        NonNotifyParent _mid = new();
        public NonNotifyParent Mid { get => _mid; set { if (_mid == value) return; _mid = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mid))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [Fact]
    public void Mixed_WhenChanged_Handles_NonNotify_Parent_And_Nulls()
    {
        var root = new MRoot { Mid = new NonNotifyParent { Child = new Person { Name = "A" } } };
        var values = new List<string>();
        using var d = root.WhenChanged(r => r.Mid.Child.Name).Subscribe(v => values.Add(v ?? "<null>"));
        _fp.Advance(); Assert.Equal("A", values.Last());
        // Update child name (leaf notifies)
        root.Mid.Child.Name = "B"; _fp.Advance(); Assert.Equal("B", values.Last());
        // Replace child (parent non-notify, fallback EveryValueChanged should detect)
        root.Mid.Child = new Person { Name = "C" }; _fp.Advance(); Assert.Equal("C", values.Last());
        // Null then restore
        root.Mid.Child = null!; // should not throw
        _fp.Advance();
        root.Mid.Child = new Person { Name = "D" }; _fp.Advance(); Assert.Equal("D", values.Last());
        // Replace non-notify parent entirely
        root.Mid = new NonNotifyParent { Child = new Person { Name = "E" } }; _fp.Advance(); Assert.Equal("E", values.Last());
    }
}
