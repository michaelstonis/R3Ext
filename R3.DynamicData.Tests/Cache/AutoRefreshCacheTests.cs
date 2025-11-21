// Port of DynamicData to R3.

using System.ComponentModel;
using R3.DynamicData.Cache;

namespace R3.DynamicData.Tests.Cache;

public class AutoRefreshCacheTests
{
    private sealed class Person : INotifyPropertyChanged
    {
        private int age;
        public int Id { get; }
        public int Age
        {
            get => age;
            set
            {
                if (age != value)
                {
                    age = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Age)));
                }
            }
        }

        public Person(int id, int age)
        {
            Id = id;
            Age = age;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [Fact]
    public void AutoRefresh_EmitsRefreshOnPropertyChange()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var refreshCounts = 0;
        var refreshReasons = new List<Kernel.ChangeReason>();

        using var sub = cache.Connect()
            .AutoRefresh<Person, int, int>(p => p.Age)
            .Subscribe(changes =>
            {
                foreach (var c in changes)
                {
                    if (c.Reason == Kernel.ChangeReason.Refresh)
                    {
                        refreshCounts++;
                        refreshReasons.Add(c.Reason);
                    }
                }
            });

        var p1 = new Person(1, 10);
        cache.AddOrUpdate(p1); // add
        p1.Age = 20; // refresh
        p1.Age = 25; // refresh

        Assert.Equal(2, refreshCounts);
        Assert.All(refreshReasons, r => Assert.Equal(Kernel.ChangeReason.Refresh, r));
    }
}
