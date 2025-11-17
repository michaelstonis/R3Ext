using System;
using R3;
using R3Ext;
using Xunit;

namespace R3Ext.Tests;

public class BaseTypeMatchingTests
{
    private sealed class Vm
    {
        public string Name { get; set; } = string.Empty;
    }

    private class BaseTarget
    {
        public string Text { get; set; } = string.Empty;
    }

    private sealed class DerivedTarget : BaseTarget {}

    private sealed class DummyDisposable : IDisposable
    {
        public void Dispose() { }
    }

    [Fact]
    public void OneWay_RegistrationOnBase_MatchesDerivedAtRuntime()
    {
        var keyFrom = "f => f.Name";
        var keyTo = "t => t.Text";

        bool invoked = false;
        BindingRegistry.RegisterOneWay<Vm, string, BaseTarget, string>(
            keyFrom,
            keyTo,
            (vm, target, conv) =>
            {
                invoked = target is DerivedTarget;
                return new DummyDisposable();
            });

        var vm = new Vm { Name = "Alice" };
        var target = new DerivedTarget { Text = "" };

        var ok = BindingRegistry.TryCreateOneWay<Vm, string, DerivedTarget, string>(
            keyFrom, keyTo, vm, target, null, out var disp);

        Assert.True(ok);
        Assert.True(invoked);
        disp.Dispose();
    }

    private class BaseWhen
    {
        public int Value { get; set; }
    }

    private sealed class DerivedWhen : BaseWhen {}

    [Fact]
    public void WhenChanged_RegistrationOnBase_MatchesDerivedAtRuntime()
    {
        var fullKey = "BaseWhen|o => o.Value";

        BindingRegistry.RegisterWhenChanged<BaseWhen, int>(
            fullKey,
            o => Observable.Return(o.Value));

        var obj = new DerivedWhen { Value = 42 };
        var ok = BindingRegistry.TryCreateWhenChanged<DerivedWhen, int>(fullKey, obj, out var obs);

        Assert.True(ok);
        // Subscribe once to ensure observable pipeline is usable
        int received = -1;
        using var sub = obs.Subscribe(v => received = v);
        Assert.Equal(42, received);
    }
}
