using System.ComponentModel;
using System.Runtime.CompilerServices;
using R3;
using R3.DynamicData.Cache;
using R3Ext;

namespace R3.DynamicData.Tests.Cache;

public class WhenValueChangedTests
{
    [Fact]
    public void WhenValueChanged_EmitsInitialValue()
    {
        var cache = new SourceCache<TestPerson, int>(x => x.Id);
        var results = new List<PropertyValue<TestPerson, string>>();

        using var sub = cache.Connect()
            .WhenValueChanged(p => p.Name, x => x.Id, notifyOnInitialValue: true)
            .Subscribe(results.Add);

        var person = new TestPerson { Id = 1, Name = "Alice" };
        cache.AddOrUpdate(person);

        Assert.Single(results);
        Assert.Equal("Alice", results[0].Value);
        Assert.Same(person, results[0].Sender);
    }

    [Fact]
    public void WhenValueChanged_EmitsOnPropertyChange()
    {
        var cache = new SourceCache<TestPerson, int>(x => x.Id);
        var results = new List<PropertyValue<TestPerson, string>>();

        var person = new TestPerson { Id = 1, Name = "Alice" };
        cache.AddOrUpdate(person);

        using var sub = cache.Connect()
            .WhenValueChanged(p => p.Name, x => x.Id, notifyOnInitialValue: false)
            .Subscribe(results.Add);

        // Change the name
        person.Name = "Bob";

        Assert.Single(results);
        Assert.Equal("Bob", results[0].Value);
    }

    [Fact]
    public void WhenValueChanged_TracksMultipleObjects()
    {
        var cache = new SourceCache<TestPerson, int>(x => x.Id);
        var results = new List<PropertyValue<TestPerson, string>>();

        using var sub = cache.Connect()
            .WhenValueChanged(p => p.Name, x => x.Id, notifyOnInitialValue: false)
            .Subscribe(results.Add);

        var person1 = new TestPerson { Id = 1, Name = "Alice" };
        var person2 = new TestPerson { Id = 2, Name = "Bob" };

        cache.AddOrUpdate(person1);
        cache.AddOrUpdate(person2);

        person1.Name = "Alice2";
        person2.Name = "Bob2";

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Sender == person1 && r.Value == "Alice2");
        Assert.Contains(results, r => r.Sender == person2 && r.Value == "Bob2");
    }

    [Fact]
    public void WhenValueChanged_StopsTrackingOnRemove()
    {
        var cache = new SourceCache<TestPerson, int>(x => x.Id);
        var results = new List<PropertyValue<TestPerson, string>>();

        var person = new TestPerson { Id = 1, Name = "Alice" };
        cache.AddOrUpdate(person);

        using var sub = cache.Connect()
            .WhenValueChanged(p => p.Name, x => x.Id, notifyOnInitialValue: false)
            .Subscribe(results.Add);

        cache.Remove(1);

        // Change after removal - should not emit
        person.Name = "Bob";

        Assert.Empty(results);
    }

    [Fact]
    public void WhenValueChanged_TracksIntProperty()
    {
        var cache = new SourceCache<TestPerson, int>(x => x.Id);
        var results = new List<PropertyValue<TestPerson, int>>();

        using var sub = cache.Connect()
            .WhenValueChanged(p => p.Age, x => x.Id, notifyOnInitialValue: false)
            .Subscribe(results.Add);

        var person = new TestPerson { Id = 1, Age = 25 };
        cache.AddOrUpdate(person);

        person.Age = 26;
        person.Age = 27;

        Assert.Equal(2, results.Count);
        Assert.Equal(26, results[0].Value);
        Assert.Equal(27, results[1].Value);
    }

    [Fact]
    public void WhenValueChangedWithPrevious_EmitsBeforeAndAfter()
    {
        var cache = new SourceCache<TestPerson, int>(x => x.Id);
        var results = new List<PropertyValueChange<TestPerson, string>>();

        var person = new TestPerson { Id = 1, Name = "Alice" };
        cache.AddOrUpdate(person);

        using var sub = cache.Connect()
            .WhenValueChangedWithPrevious(p => p.Name, x => x.Id)
            .Subscribe(results.Add);

        person.Name = "Bob";
        person.Name = "Charlie";

        Assert.Equal(2, results.Count);
        Assert.Equal("Alice", results[0].Previous);
        Assert.Equal("Bob", results[0].Current);
        Assert.Equal("Bob", results[1].Previous);
        Assert.Equal("Charlie", results[1].Current);
    }

    [Fact]
    public void WhenValueChanged_HandlesUpdate()
    {
        var cache = new SourceCache<TestPerson, int>(x => x.Id);
        var results = new List<PropertyValue<TestPerson, string>>();

        var person1 = new TestPerson { Id = 1, Name = "Alice" };
        cache.AddOrUpdate(person1);

        using var sub = cache.Connect()
            .WhenValueChanged(p => p.Name, x => x.Id, notifyOnInitialValue: false)
            .Subscribe(results.Add);

        // Update with new instance
        var person2 = new TestPerson { Id = 1, Name = "Bob" };
        cache.AddOrUpdate(person2);

        // Old instance should not be tracked
        person1.Name = "Alice2";

        // New instance should be tracked
        person2.Name = "Bob2";

        Assert.Single(results);
        Assert.Equal("Bob2", results[0].Value);
    }

    [Fact]
    public void WhenValueChanged_MultipleProperties()
    {
        var cache = new SourceCache<TestPerson, int>(x => x.Id);
        var nameResults = new List<PropertyValue<TestPerson, string>>();
        var ageResults = new List<PropertyValue<TestPerson, int>>();

        var person = new TestPerson { Id = 1, Name = "Alice", Age = 25 };
        cache.AddOrUpdate(person);

        using var nameSub = cache.Connect()
            .WhenValueChanged(p => p.Name, x => x.Id, notifyOnInitialValue: false)
            .Subscribe(nameResults.Add);

        using var ageSub = cache.Connect()
            .WhenValueChanged(p => p.Age, x => x.Id, notifyOnInitialValue: false)
            .Subscribe(ageResults.Add);

        person.Name = "Bob";
        person.Age = 26;

        Assert.Single(nameResults);
        Assert.Equal("Bob", nameResults[0].Value);
        Assert.Single(ageResults);
        Assert.Equal(26, ageResults[0].Value);
    }

    [Fact]
    public void WhenValueChanged_DisposalCleansUpHandlers()
    {
        var cache = new SourceCache<TestPerson, int>(x => x.Id);
        var results = new List<PropertyValue<TestPerson, string>>();

        var person = new TestPerson { Id = 1, Name = "Alice" };
        cache.AddOrUpdate(person);

        var sub = cache.Connect()
            .WhenValueChanged(p => p.Name, x => x.Id, notifyOnInitialValue: false)
            .Subscribe(results.Add);

        person.Name = "Bob";
        Assert.Single(results);

        // Dispose subscription
        sub.Dispose();

        // Should not emit after disposal
        person.Name = "Charlie";
        Assert.Single(results); // Still only 1
    }

    public class TestPerson : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private int _age;

        public int Id { get; set; }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Age
        {
            get => _age;
            set
            {
                if (_age != value)
                {
                    _age = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
