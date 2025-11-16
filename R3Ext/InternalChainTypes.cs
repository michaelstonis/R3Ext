using System.ComponentModel;

namespace R3Ext;

// Types exposing internal property chain for extended generator tests.
public sealed class InternalLeaf : INotifyPropertyChanged
{
    private string _name = string.Empty;
    internal string Name { get => _name; set { if (_name == value) return; _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class InternalMid : INotifyPropertyChanged
{
    private InternalLeaf _leaf = new();
    internal InternalLeaf Leaf { get => _leaf; set { if (_leaf == value) return; _leaf = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Leaf))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class InternalRoot : INotifyPropertyChanged
{
    private InternalMid _mid = new();
    internal InternalMid Mid { get => _mid; set { if (_mid == value) return; _mid = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mid))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}