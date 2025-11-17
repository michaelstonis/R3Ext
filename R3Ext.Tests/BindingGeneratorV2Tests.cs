using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using R3Ext;
using Xunit;

namespace R3Ext.Tests;

public class BindingGeneratorV2Tests
{
    [Fact]
    public void TwoWay_SingleLevel_INPC_UpdatesBothWays()
    {
        var host = new Host();
        var target = new Target();

        using var d = host.BindTwoWay(target, h => h.A, t => t.Text);

        host.A = "alpha";
        Assert.Equal("alpha", target.Text);

        target.Text = "beta";
        Assert.Equal("beta", host.A);
    }

    [Fact]
    public void OneWay_NestedChain_UpdatesTarget()
    {
        var host = new Host { B = new Nested { Name = "start" } };
        var target = new Target();

        using var d = host.BindOneWay(target, h => h.B!.Name, t => t.Text);

        Assert.Equal("start", target.Text);

        host.B.Name = "changed";
        Assert.Equal("changed", target.Text);

        host.B = new Nested { Name = "new" };
        Assert.Equal("new", target.Text);
    }

    internal sealed class Host : ObservableObject
    {
        private string _a = string.Empty;
        public string A { get => _a; set => Set(ref _a, value); }

        private Nested? _b;
        public Nested? B { get => _b; set => Set(ref _b, value); }
    }

    internal sealed class Target : ObservableObject
    {
        private string _text = string.Empty;
        public string Text { get => _text; set => Set(ref _text, value); }
    }

    internal sealed class Nested : ObservableObject
    {
        private string _name = string.Empty;
        public string Name { get => _name; set => Set(ref _name, value); }
    }

    internal abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }
}
