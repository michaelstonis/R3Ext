using System.ComponentModel;
using R3;
using R3.Collections;

namespace R3Ext.Tests;

public class RxObjectTests
{
    private class SampleVm : RxObject
    {
        private string _name = "initial";

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }
    }

    [Fact]
    public void RaiseAndSetIfChanged_RaisesEvents_WhenValueChanges()
    {
        SampleVm vm = new();
        string? changingProp = null;
        string? changedProp = null;
        vm.PropertyChanging += (_, e) => changingProp = e.PropertyName;
        vm.PropertyChanged += (_, e) => changedProp = e.PropertyName;
        vm.Name = "next";
        Assert.Equal("Name", changingProp);
        Assert.Equal("Name", changedProp);
    }

    [Fact]
    public void RaiseAndSetIfChanged_DoesNotRaise_WhenSameValue()
    {
        SampleVm vm = new();
        int changing = 0;
        int changed = 0;
        vm.PropertyChanging += (_, _) => changing++;
        vm.PropertyChanged += (_, _) => changed++;
        vm.Name = "initial"; // same
        Assert.Equal(0, changing);
        Assert.Equal(0, changed);
    }

    [Fact]
    public void ChangingAndChanged_Observables_Emit()
    {
        SampleVm vm = new();
        LiveList<PropertyChangingEventArgs> changing = vm.Changing.ToLiveList();
        LiveList<PropertyChangedEventArgs> changed = vm.Changed.ToLiveList();
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
        SampleVm vm = new();
        LiveList<PropertyChangedEventArgs> changed = vm.Changed.ToLiveList();
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
        SampleVm vm = new();
        LiveList<PropertyChangedEventArgs> changed = vm.Changed.ToLiveList();
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
        SampleVm vm = new();
        Assert.True(vm.AreChangeNotificationsEnabled());
        using (vm.SuppressChangeNotifications())
        {
            Assert.False(vm.AreChangeNotificationsEnabled());
        }

        Assert.True(vm.AreChangeNotificationsEnabled());
    }
}
