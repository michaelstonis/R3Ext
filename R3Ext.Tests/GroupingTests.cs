using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using R3; // R3 observable extensions
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;
#pragma warning disable SA1208
#pragma warning disable SA1516
#pragma warning disable SA1501
#pragma warning disable SA1107
#pragma warning disable SA1503
#pragma warning disable SA1502
#pragma warning disable SA1513
#pragma warning disable SA1515

namespace R3Ext.Tests;

public class GroupingTests
{
    private class Person : INotifyPropertyChanged
    {
        private string _dept = string.Empty;
        public int Id { get; }
        public string Department
        {
            get => _dept;
            set { if (_dept != value) { _dept = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Department))); } }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        public Person(int id, string dept) { Id = id; _dept = dept; }
    }

    [Fact]
    public void GroupsAreCreatedAndResetEmitted()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var currentGroups = new Dictionary<string, IGroup<Person, int, string>>();
        var changeSetCount = 0;

        var subscription = cache.Connect()
            .GroupOn<Person, int, string>(p => p.Department)
            .Subscribe(cs =>
            {
                changeSetCount++;
                foreach (var change in cs)
                {
                    if (change.Reason == ChangeReason.Add)
                        currentGroups[change.Key] = change.Current;
                    else if (change.Reason == ChangeReason.Remove)
                        currentGroups.Remove(change.Key);
                }
            });

        cache.AddOrUpdate(new Person(1, "HR"));
        cache.AddOrUpdate(new Person(2, "Eng"));
        cache.AddOrUpdate(new Person(3, "Eng"));

        Assert.True(changeSetCount >= 2); // At least 2 groups created
        Assert.Equal(2, currentGroups.Count); // HR, Eng
        Assert.True(currentGroups.ContainsKey("HR"));
        Assert.True(currentGroups.ContainsKey("Eng"));
        var engGroup = currentGroups["Eng"];
        Assert.Equal(2, engGroup.Cache.Count);

        subscription.Dispose();
    }

    [Fact]
    public void RefreshMovesItemBetweenGroups()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var currentGroups = new Dictionary<string, IGroup<Person, int, string>>();

        var subscription = cache.Connect()
            .GroupOn<Person, int, string>(p => p.Department)
            .Subscribe(cs =>
            {
                foreach (var change in cs)
                {
                    if (change.Reason == ChangeReason.Add)
                        currentGroups[change.Key] = change.Current;
                    else if (change.Reason == ChangeReason.Remove)
                        currentGroups.Remove(change.Key);
                }
            });

        var p1 = new Person(1, "HR");
        cache.AddOrUpdate(p1);
        p1.Department = "Finance"; // change department
        cache.Edit(u => u.Refresh(p1.Id));

        Assert.True(currentGroups.ContainsKey("Finance"));
        Assert.False(currentGroups.ContainsKey("HR")); // HR group should be removed when empty
        Assert.Single(currentGroups["Finance"].Cache.Items);

        subscription.Dispose();
    }
}
