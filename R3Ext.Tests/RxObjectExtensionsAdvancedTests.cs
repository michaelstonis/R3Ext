using System.ComponentModel;
using R3;
using R3.Collections;

namespace R3Ext.Tests;

public class RxObjectExtensionsAdvancedTests
{
    private class TestRxObject : RxObject
    {
        private string _value = "initial";
        private int _counter;

        public string Value
        {
            get => _value;
            set => this.RaiseAndSetIfChanged(ref _value, value);
        }

        public int Counter
        {
            get => _counter;
            set => this.RaiseAndSetIfChanged(ref _counter, value);
        }
    }

    private record TestRxRecord : RxRecord
    {
        private string _name = "default";
        private int _age;

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public int Age
        {
            get => _age;
            set => this.RaiseAndSetIfChanged(ref _age, value);
        }
    }

    [Fact]
    public void RaiseAndSetIfChanged_RxObject_ThrowsOnNullPropertyName()
    {
        var obj = new TestRxObject();
        var field = "test";
        Assert.Throws<ArgumentNullException>(() =>
            obj.RaiseAndSetIfChanged(ref field, "new", propertyName: null!));
    }

    [Fact]
    public void RaiseAndSetIfChanged_RxObject_ChangesFieldWhenValueDifferent()
    {
        var obj = new TestRxObject();
        var field = "initial";
        var result = obj.RaiseAndSetIfChanged(ref field, "updated");
        Assert.Equal("updated", field);
        Assert.Equal("updated", result);
    }

    [Fact]
    public void RaiseAndSetIfChanged_RxObject_DoesNotChangeFieldWhenValueSame()
    {
        var obj = new TestRxObject();
        var field = "same";
        var result = obj.RaiseAndSetIfChanged(ref field, "same");
        Assert.Equal("same", field);
        Assert.Equal("same", result);
    }

    [Fact]
    public void RaiseAndSetIfChanged_RxObject_RaisesChangingAndChangedWhenValueDifferent()
    {
        var obj = new TestRxObject();
        var field = "initial";
        var changingFired = false;
        var changedFired = false;

        obj.PropertyChanging += (_, e) =>
        {
            changingFired = true;
            Assert.Equal("TestProperty", e.PropertyName);
        };
        obj.PropertyChanged += (_, e) =>
        {
            changedFired = true;
            Assert.Equal("TestProperty", e.PropertyName);
        };

        obj.RaiseAndSetIfChanged(ref field, "updated", "TestProperty");

        Assert.True(changingFired);
        Assert.True(changedFired);
    }

    [Fact]
    public void RaiseAndSetIfChanged_RxObject_DoesNotRaiseWhenValueSame()
    {
        var obj = new TestRxObject();
        var field = "same";
        var changingFired = false;
        var changedFired = false;

        obj.PropertyChanging += (_, _) => changingFired = true;
        obj.PropertyChanged += (_, _) => changedFired = true;

        obj.RaiseAndSetIfChanged(ref field, "same");

        Assert.False(changingFired);
        Assert.False(changedFired);
    }

    [Fact]
    public void RaiseAndSetIfChanged_RxObject_WorksWithValueTypes()
    {
        var obj = new TestRxObject();
        var field = 42;
        var changed = obj.Changed.ToLiveList();

        obj.RaiseAndSetIfChanged(ref field, 99, "Counter");

        Assert.Equal(99, field);
        Assert.Single(changed);
        Assert.Equal("Counter", changed[0].PropertyName);
    }

    [Fact]
    public void RaiseAndSetIfChanged_RxObject_WorksWithNullValues()
    {
        var obj = new TestRxObject();
        string? field = "initial";
        var changed = obj.Changed.ToLiveList();

        obj.RaiseAndSetIfChanged(ref field, null, "NullableField");

        Assert.Null(field);
        Assert.Single(changed);
        Assert.Equal("NullableField", changed[0].PropertyName);
    }

    [Fact]
    public void RaiseAndSetIfChanged_RxRecord_ChangesFieldWhenValueDifferent()
    {
        var record = new TestRxRecord();
        var field = "initial";
        var result = record.RaiseAndSetIfChanged(ref field, "updated");
        Assert.Equal("updated", field);
        Assert.Equal("updated", result);
    }

    [Fact]
    public void RaiseAndSetIfChanged_RxRecord_RaisesEventsWhenValueDifferent()
    {
        var record = new TestRxRecord();
        var field = "initial";
        var changing = record.Changing.ToLiveList();
        var changed = record.Changed.ToLiveList();

        record.RaiseAndSetIfChanged(ref field, "updated", "Name");

        Assert.Single(changing);
        Assert.Single(changed);
        Assert.Equal("Name", changing[0].PropertyName);
        Assert.Equal("Name", changed[0].PropertyName);
    }

    [Fact]
    public void RaiseAndSetIfChanged_RxRecord_DoesNotRaiseWhenValueSame()
    {
        var record = new TestRxRecord();
        var field = "same";
        var changed = record.Changed.ToLiveList();

        record.RaiseAndSetIfChanged(ref field, "same");

        Assert.Empty(changed);
    }

    [Fact]
    public void RaisePropertyChanged_RxObject_RaisesEvent()
    {
        var obj = new TestRxObject();
        var changed = obj.Changed.ToLiveList();

        obj.RaisePropertyChanged("CustomProperty");

        Assert.Single(changed);
        Assert.Equal("CustomProperty", changed[0].PropertyName);
    }

    [Fact]
    public void RaisePropertyChanged_RxObject_WorksWithEmptyString()
    {
        var obj = new TestRxObject();
        var changed = obj.Changed.ToLiveList();
        obj.RaisePropertyChanged(string.Empty);
        Assert.Single(changed);
        Assert.Equal(string.Empty, changed[0].PropertyName);
    }

    [Fact]
    public void RaisePropertyChanged_RxRecord_RaisesEvent()
    {
        var record = new TestRxRecord();
        var changed = record.Changed.ToLiveList();

        record.RaisePropertyChanged("ComputedProperty");

        Assert.Single(changed);
        Assert.Equal("ComputedProperty", changed[0].PropertyName);
    }

    [Fact]
    public void RaisePropertyChanged_RxRecord_WorksWithEmptyString()
    {
        var record = new TestRxRecord();
        var changed = record.Changed.ToLiveList();
        record.RaisePropertyChanged(string.Empty);
        Assert.Single(changed);
        Assert.Equal(string.Empty, changed[0].PropertyName);
    }

    [Fact]
    public void RaisePropertyChanging_RxObject_RaisesEvent()
    {
        var obj = new TestRxObject();
        var changing = obj.Changing.ToLiveList();

        obj.RaisePropertyChanging("AboutToChange");

        Assert.Single(changing);
        Assert.Equal("AboutToChange", changing[0].PropertyName);
    }

    [Fact]
    public void RaisePropertyChanging_RxObject_WorksWithEmptyString()
    {
        var obj = new TestRxObject();
        var changing = obj.Changing.ToLiveList();
        obj.RaisePropertyChanging(string.Empty);
        Assert.Single(changing);
        Assert.Equal(string.Empty, changing[0].PropertyName);
    }

    [Fact]
    public void RaisePropertyChanging_RxRecord_RaisesEvent()
    {
        var record = new TestRxRecord();
        var changing = record.Changing.ToLiveList();

        record.RaisePropertyChanging("WillChange");

        Assert.Single(changing);
        Assert.Equal("WillChange", changing[0].PropertyName);
    }

    [Fact]
    public void RaisePropertyChanging_RxRecord_WorksWithEmptyString()
    {
        var record = new TestRxRecord();
        var changing = record.Changing.ToLiveList();
        record.RaisePropertyChanging(string.Empty);
        Assert.Single(changing);
        Assert.Equal(string.Empty, changing[0].PropertyName);
    }

    [Fact]
    public void RaiseAndSetIfChanged_ExtensionMethod_WorksWithMultipleChanges()
    {
        var obj = new TestRxObject();
        var field = 0;
        var changed = obj.Changed.ToLiveList();

        obj.RaiseAndSetIfChanged(ref field, 1, "Counter");
        obj.RaiseAndSetIfChanged(ref field, 2, "Counter");
        obj.RaiseAndSetIfChanged(ref field, 3, "Counter");

        Assert.Equal(3, field);
        Assert.Equal(3, changed.Count);
    }

    [Fact]
    public void RaiseAndSetIfChanged_RxObject_ChangingFiresBeforeChanged()
    {
        var obj = new TestRxObject();
        var field = "initial";
        var events = new List<string>();

        obj.PropertyChanging += (_, e) => events.Add($"Changing:{e.PropertyName}");
        obj.PropertyChanged += (_, e) => events.Add($"Changed:{e.PropertyName}");

        obj.RaiseAndSetIfChanged(ref field, "updated", "Test");

        Assert.Equal(2, events.Count);
        Assert.Equal("Changing:Test", events[0]);
        Assert.Equal("Changed:Test", events[1]);
    }

    [Fact]
    public void RaiseAndSetIfChanged_RxObject_RespectsEqualityComparerForCustomTypes()
    {
        var obj = new TestRxObject();
        var field = new CustomEquatable(1, "test");
        var changed = obj.Changed.ToLiveList();

        // Same values according to custom equality
        obj.RaiseAndSetIfChanged(ref field, new CustomEquatable(1, "test"), "Custom");
        Assert.Empty(changed);

        // Different values
        obj.RaiseAndSetIfChanged(ref field, new CustomEquatable(2, "different"), "Custom");
        Assert.Single(changed);
    }

    [Fact]
    public void RaisePropertyChanged_RxObject_MultipleCallsRaiseMultipleEvents()
    {
        var obj = new TestRxObject();
        var changed = obj.Changed.ToLiveList();

        obj.RaisePropertyChanged("Prop1");
        obj.RaisePropertyChanged("Prop2");
        obj.RaisePropertyChanged("Prop3");

        Assert.Equal(3, changed.Count);
        Assert.Equal("Prop1", changed[0].PropertyName);
        Assert.Equal("Prop2", changed[1].PropertyName);
        Assert.Equal("Prop3", changed[2].PropertyName);
    }

    [Fact]
    public void RaisePropertyChanging_RxObject_MultipleCallsRaiseMultipleEvents()
    {
        var obj = new TestRxObject();
        var changing = obj.Changing.ToLiveList();

        obj.RaisePropertyChanging("Prop1");
        obj.RaisePropertyChanging("Prop2");
        obj.RaisePropertyChanging("Prop3");

        Assert.Equal(3, changing.Count);
        Assert.Equal("Prop1", changing[0].PropertyName);
        Assert.Equal("Prop2", changing[1].PropertyName);
        Assert.Equal("Prop3", changing[2].PropertyName);
    }

    private record CustomEquatable(int Id, string Name);
}
