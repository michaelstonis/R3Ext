using R3.DynamicData.Cache;
using System.Collections.Generic;
using System.ComponentModel;

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
                // Extract group snapshot after reset
                var currentGroups = new List<Group<Person, string>>();
                foreach (var c in cs)
                {
                    if (c.Reason == ListChangeReason.Add)
                        currentGroups.Add(c.Item);
                }
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
                var currentGroups = new List<Group<Person, string>>();
                foreach (var c in cs)
                {
                    if (c.Reason == ListChangeReason.Add)
                        currentGroups.Add(c.Item);
                }
                groupsObserved.Add(currentGroups);
            });

        var p1 = new Person(1, "HR");
        cache.AddOrUpdate(p1);
        p1.Department = "Finance"; // triggers refresh path via INotifyPropertyChanged
        cache.Refresh(p1);

        var last = groupsObserved[^1];
        Assert.Contains(last, g => g.Key == "Finance");
        Assert.DoesNotContain(last, g => g.Key == "HR" && g.Items.Contains(p1));

        subscription.Dispose();
    }
}
