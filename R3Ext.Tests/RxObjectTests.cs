using System;
using System.ComponentModel;
using R3;
using R3Ext;
using Xunit;

namespace R3Ext.Tests;

public class RxObjectTests
{
    private class SampleVm : RxObject
    {
        private string _name = "initial";
        public string Name
        {
            get => _name;
            set => RaiseAndSetIfChanged(ref _name, value);
        }
    }

    [Fact]
    public void RaiseAndSetIfChanged_RaisesEvents_WhenValueChanges()
    {
        var vm = new SampleVm();
        string? changingProp = null; string? changedProp = null;
        vm.PropertyChanging += (_, e) => changingProp = e.PropertyName;
        vm.PropertyChanged += (_, e) => changedProp = e.PropertyName;
        vm.Name = "next";
        Assert.Equal("Name", changingProp);
        Assert.Equal("Name", changedProp);
    }

    [Fact]
    public void RaiseAndSetIfChanged_DoesNotRaise_WhenSameValue()
    {
        var vm = new SampleVm();
        int changing = 0; int changed = 0;
        vm.PropertyChanging += (_, _) => changing++;
        vm.PropertyChanged += (_, _) => changed++;
        vm.Name = "initial"; // same
        Assert.Equal(0, changing);
        Assert.Equal(0, changed);
    }

    [Fact]
    public void ChangingAndChanged_Observables_Emit()
    {
        var vm = new SampleVm();
        var changing = vm.Changing.ToLiveList();
        var changed = vm.Changed.ToLiveList();
        vm.Name = "one";
        vm.Name = "two";
        Assert.Equal(2, changing.Count);
        Assert.Equal(2, changed.Count);
        Assert.Equal("Name", changing[0].PropertyName);
        Assert.Equal("Name", changed[1].PropertyName);
    }

    [Fact]
    public void SuppressChangeNotifications_DiscardsEvents()
    {
        var vm = new SampleVm();
        var changed = vm.Changed.ToLiveList();
        using (vm.SuppressChangeNotifications())
        {
            vm.Name = "a";
            vm.Name = "b";
        }
        vm.Name = "c";
        Assert.Single(changed); // only final change outside suppression
        Assert.Equal("Name", changed[0].PropertyName);
    }

    [Fact]
    public void DelayChangeNotifications_AggregatesAndEmitsOncePerProperty()
    {
        var vm = new SampleVm();
        var changed = vm.Changed.ToLiveList();
        using (vm.DelayChangeNotifications())
        {
            vm.Name = "a";
            vm.Name = "b";
            vm.Name = "c";
        }
        Assert.Single(changed);
        Assert.Equal("Name", changed[0].PropertyName);
    }

    [Fact]
    public void AreChangeNotificationsEnabled_ReflectsSuppression()
    {
        var vm = new SampleVm();
        Assert.True(vm.AreChangeNotificationsEnabled());
        using (vm.SuppressChangeNotifications())
        {
            Assert.False(vm.AreChangeNotificationsEnabled());
        }
        Assert.True(vm.AreChangeNotificationsEnabled());
    }
}