using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Xunit;
using R3;
using R3Ext;

namespace R3Ext.Tests;

public class RewiringTests
{
    // Deep property chain test types as nested classes (like DRoot pattern in MixedAndDeepBindingTests)
    internal sealed class Leaf : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public string Name { get => _name; set { if (_name == value) return; _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    internal sealed class Mid : INotifyPropertyChanged
    {
        private Leaf _leaf = new();
        public Leaf Leaf { get => _leaf; set { if (_leaf == value) return; _leaf = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Leaf))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    internal sealed class Root : INotifyPropertyChanged
    {
        private Mid _mid = new();
        public Mid Mid { get => _mid; set { if (_mid == value) return; _mid = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mid))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [Fact]
    public void WhenChanged_Rewires_On_Intermediate_Replacements()
    {
        var root = new Root { Mid = new Mid { Leaf = new Leaf { Name = "A" } } };
        var values = new List<string>();
        using var disp = root.WhenChanged(r => r.Mid.Leaf.Name).Subscribe(v => values.Add(v ?? "<null>"));

        Assert.Equal("A", values.Last());
        root.Mid.Leaf.Name = "B"; Assert.Equal("B", values.Last());
        root.Mid.Leaf = new Leaf { Name = "C" }; Assert.Equal("C", values.Last());
        root.Mid = new Mid { Leaf = new Leaf { Name = "D" } }; Assert.Equal("D", values.Last());
        root.Mid.Leaf.Name = "E"; Assert.Equal("E", values.Last());
    }
}
