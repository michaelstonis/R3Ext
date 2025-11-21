using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using R3; // R3 observable extensions
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;
using ListChangeReason = R3.DynamicData.List.ListChangeReason; // Alias only the enum to avoid Group<> ambiguity
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
        var groupsObserved = new List<IReadOnlyList<Group<Person, string>>>();
        var subscription = cache.Connect()
            .GroupOn<Person, int, string>(p => p.Department)
            .Subscribe(cs =>
            {
                var currentGroups = cs.Where(c => c.Reason == ListChangeReason.Add).Select(c => c.Item).ToList();
                groupsObserved.Add(currentGroups);
            });

        cache.AddOrUpdate(new Person(1, "HR"));
        cache.AddOrUpdate(new Person(2, "Eng"));
        cache.AddOrUpdate(new Person(3, "Eng"));

        Assert.True(groupsObserved.Count >= 3); // Reset after each add
        var last = groupsObserved[^1];
        Assert.Equal(2, last.Count); // HR, Eng
        var engGroup = last.First(g => g.Key == "Eng");
        Assert.Equal(2, engGroup.Items.Count);

        subscription.Dispose();
    }

    [Fact]
    public void RefreshMovesItemBetweenGroups()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var groupsObserved = new List<IReadOnlyList<Group<Person, string>>>();

        var subscription = cache.Connect()
            .GroupOn<Person, int, string>(p => p.Department)
            .Subscribe(cs =>
            {
                var currentGroups = cs.Where(c => c.Reason == ListChangeReason.Add).Select(c => c.Item).ToList();
                groupsObserved.Add(currentGroups);
            });

        var p1 = new Person(1, "HR");
        cache.AddOrUpdate(p1);
        p1.Department = "Finance"; // change department
        cache.Edit(u => u.Refresh(p1.Id));

        var last = groupsObserved[^1];
        Assert.Contains(last, g => g.Key == "Finance");
        Assert.DoesNotContain(last, g => g.Key == "HR" && g.Items.Contains(p1));

        subscription.Dispose();
    }
}
