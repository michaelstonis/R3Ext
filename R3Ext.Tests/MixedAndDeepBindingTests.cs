using System.ComponentModel;
using R3; // Observable and Subscribe helpers

namespace R3Ext.Tests;

[Collection("FrameProvider")]
public class MixedAndDeepBindingTests(FrameProviderFixture fp)
{
    // Deep chain types (all notify)
    internal sealed class DLeaf : INotifyPropertyChanged
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    internal sealed class DD : INotifyPropertyChanged
    {
        private DLeaf _leaf = new();

        public DLeaf Leaf
        {
            get => _leaf;
            set
            {
                if (_leaf == value)
                {
                    return;
                }

                _leaf = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Leaf)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    internal sealed class DC : INotifyPropertyChanged
    {
        private DD _d = new();

        public DD D
        {
            get => _d;
            set
            {
                if (_d == value)
                {
                    return;
                }

                _d = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(D)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    internal sealed class DB : INotifyPropertyChanged
    {
        private DC _c = new();

        public DC C
        {
            get => _c;
            set
            {
                if (_c == value)
                {
                    return;
                }

                _c = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(C)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    internal sealed class DA : INotifyPropertyChanged
    {
        private DB _b = new();

        public DB B
        {
            get => _b;
            set
            {
                if (_b == value)
                {
                    return;
                }

                _b = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(B)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    internal sealed class DRoot : INotifyPropertyChanged
    {
        private DA _a = new();

        public DA A
        {
            get => _a;
            set
            {
                if (_a == value)
                {
                    return;
                }

                _a = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(A)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [Fact]
    public void Deep_WhenChanged_Rewires_Through_Five_Levels()
    {
        DRoot root = new();
        root.A.B.C.D.Leaf.Name = "L0";
        List<string> vals = new();
        using IDisposable d = root.WhenChanged(r => r.A.B.C.D.Leaf.Name).Subscribe(v => vals.Add(v ?? "<null>"));
        Assert.Equal("L0", vals.Last());
        root.A.B.C.D.Leaf.Name = "L1";
        Assert.Equal("L1", vals.Last());
        root.A.B.C.D.Leaf = new DLeaf { Name = "L2", };
        Assert.Equal("L2", vals.Last());
        root.A.B.C.D = new DD { Leaf = new DLeaf { Name = "L3", }, };
        Assert.Equal("L3", vals.Last());
        root.A.B = new DB { C = new DC { D = new DD { Leaf = new DLeaf { Name = "L4", }, }, }, };
        Assert.Equal("L4", vals.Last());
    }

    // Mixed chain: Non-notify parent
    internal sealed class Person : INotifyPropertyChanged
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    internal sealed class NonNotifyParent // does not implement INotifyPropertyChanged
    {
        public Person Child { get; set; } = new();
    }

    internal sealed class MRoot : INotifyPropertyChanged
    {
        private NonNotifyParent _mid = new();

        public NonNotifyParent Mid
        {
            get => _mid;
            set
            {
                if (_mid == value)
                {
                    return;
                }

                _mid = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mid)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [Fact]
    public void Mixed_WhenChanged_Handles_NonNotify_Parent_And_Nulls()
    {
        MRoot root = new() { Mid = new NonNotifyParent { Child = new Person { Name = "A", }, }, };
        List<string> values = new();
        using IDisposable d = root.WhenChanged(r => r.Mid.Child.Name).Subscribe(v => values.Add(v ?? "<null>"));
        fp.Advance();
        Assert.Equal("A", values.Last());

        // Update child name (leaf notifies)
        root.Mid.Child.Name = "B";
        fp.Advance();
        Assert.Equal("B", values.Last());

        // Replace child (parent non-notify, fallback EveryValueChanged should detect)
        root.Mid.Child = new Person { Name = "C", };
        fp.Advance();
        Assert.Equal("C", values.Last());

        // Null then restore
        root.Mid.Child = null!; // should not throw
        fp.Advance();
        root.Mid.Child = new Person { Name = "D", };
        fp.Advance();
        Assert.Equal("D", values.Last());

        // Replace non-notify parent entirely
        root.Mid = new NonNotifyParent { Child = new Person { Name = "E", }, };
        fp.Advance();
        Assert.Equal("E", values.Last());
    }
}
