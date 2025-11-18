using System;
using System.ComponentModel;
using R3;
using R3Ext;
using Xunit;

namespace R3Ext.Tests;

public class RxObjectExtensionsTests
{
    private class TestVm : RxObject
    {
        private string _value = "initial";
        public string Value
        {
            get => _value;
            set => this.RaiseAndSetIfChanged(ref _value, value); // using extension
        }
    }

    [Fact]
    public void ExtensionRaiseAndSetIfChanged_WorksOutsideClass()
    {
        var vm = new TestVm();
        var changed = vm.Changed.ToLiveList();
        vm.Value = "changed";
        Assert.Single(changed);
        Assert.Equal("Value", changed[0].PropertyName);
    }

    [Fact]
    public void ExtensionRaisePropertyChanged_TriggersEvent()
    {
        var vm = new TestVm();
        string? propName = null;
        vm.PropertyChanged += (_, e) => propName = e.PropertyName;
        vm.RaisePropertyChanged("CustomProp");
        Assert.Equal("CustomProp", propName);
    }

    [Fact]
    public void ExtensionRaisePropertyChanging_TriggersEvent()
    {
        var vm = new TestVm();
        string? propName = null;
        vm.PropertyChanging += (_, e) => propName = e.PropertyName;
        vm.RaisePropertyChanging("CustomProp");
        Assert.Equal("CustomProp", propName);
    }
}
