using System;
using System.Collections.Generic;
using System.ComponentModel;
using R3;
using Xunit;

namespace R3Ext.Tests;

/// <summary>
/// Tests for RxRecord base class functionality.
/// </summary>
[Collection("FrameProvider")]
public class RxRecordTests(FrameProviderFixture fp)
{
    // Test record implementation
    private sealed record TestRecord : RxRecord
    {
        private string _name = string.Empty;
        private int _age;
        private TestRecord? _child;

        public string Name
        {
            get => _name;
            set => RaiseAndSetIfChanged(ref _name, value);
        }

        public int Age
        {
            get => _age;
            set => RaiseAndSetIfChanged(ref _age, value);
        }

        public TestRecord? Child
        {
            get => _child;
            set => RaiseAndSetIfChanged(ref _child, value);
        }
    }

    [Fact]
    public void RxRecord_RaiseAndSetIfChanged_RaisesWhenValueChanges()
    {
        // Test: Property changes trigger notifications
        TestRecord record = new();
        List<string> changedProperties = new();

        record.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        record.Name = "Alice";
        Assert.Single(changedProperties);
        Assert.Equal(nameof(TestRecord.Name), changedProperties[0]);

        record.Age = 30;
        Assert.Equal(2, changedProperties.Count);
        Assert.Equal(nameof(TestRecord.Age), changedProperties[1]);
    }

    [Fact]
    public void RxRecord_RaiseAndSetIfChanged_DoesNotRaiseWhenValueSame()
    {
        // Test: Setting same value doesn't trigger notification
        TestRecord record = new() { Name = "Bob" };
        int changeCount = 0;

        record.PropertyChanged += (s, e) => changeCount++;

        record.Name = "Bob"; // Same value
        Assert.Equal(0, changeCount);

        record.Name = "Charlie"; // Different value
        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void RxRecord_PropertyChanging_RaisesBeforePropertyChanged()
    {
        // Test: PropertyChanging fires before PropertyChanged
        TestRecord record = new();
        List<string> events = new();

        record.PropertyChanging += (s, e) => events.Add($"Changing:{e.PropertyName}");
        record.PropertyChanged += (s, e) => events.Add($"Changed:{e.PropertyName}");

        record.Name = "Test";

        Assert.Equal(2, events.Count);
        Assert.Equal("Changing:Name", events[0]);
        Assert.Equal("Changed:Name", events[1]);
    }

    [Fact]
    public void RxRecord_Changed_Observable_EmitsCorrectly()
    {
        // Test: Changed observable emits property change events
        TestRecord record = new();
        List<string> changedProperties = new();

        using var sub = record.Changed.Subscribe(e => changedProperties.Add(e.PropertyName!));

        record.Name = "Alice";
        record.Age = 25;

        Assert.Equal(2, changedProperties.Count);
        Assert.Equal(nameof(TestRecord.Name), changedProperties[0]);
        Assert.Equal(nameof(TestRecord.Age), changedProperties[1]);
    }

    [Fact]
    public void RxRecord_Changing_Observable_EmitsCorrectly()
    {
        // Test: Changing observable emits before property changes
        TestRecord record = new();
        List<string> changingProperties = new();

        using var sub = record.Changing.Subscribe(e => changingProperties.Add(e.PropertyName!));

        record.Name = "Bob";
        record.Age = 30;

        Assert.Equal(2, changingProperties.Count);
        Assert.Equal(nameof(TestRecord.Name), changingProperties[0]);
        Assert.Equal(nameof(TestRecord.Age), changingProperties[1]);
    }

    [Fact]
    public void RxRecord_RecordEquality_ComparesDataProperties()
    {
        // Test: Record equality compares data properties (not observable references)
        // Note: Records with reference-type properties compare by reference
        TestRecord record1 = new() { Name = "Alice", Age = 30 };
        TestRecord record2 = new() { Name = "Alice", Age = 30 };
        TestRecord record3 = new() { Name = "Bob", Age = 30 };

        // RxRecord includes Observable reference properties, so instances are not equal
        // But data properties can be checked individually
        Assert.NotEqual(record1, record2); // Different instances with Observable fields
        Assert.Equal(record1.Name, record2.Name);
        Assert.Equal(record1.Age, record2.Age);
        Assert.NotEqual(record1.Name, record3.Name);
    }

    [Fact]
    public void RxRecord_RecordWith_CreatesModifiedCopy()
    {
        // Test: Record 'with' creates new instance with modifications
        TestRecord original = new() { Name = "Alice", Age = 30 };
        TestRecord modified = original with { Age = 31 };

        Assert.Equal("Alice", modified.Name);
        Assert.Equal(31, modified.Age);
        Assert.NotSame(original, modified);
    }

    [Fact]
    public void RxRecord_SuppressChangeNotifications_PreventsNotifications()
    {
        // Test: Suppressing notifications prevents events
        TestRecord record = new();
        int changeCount = 0;

        record.PropertyChanged += (s, e) => changeCount++;

        using (record.SuppressChangeNotifications())
        {
            record.Name = "Suppressed";
            record.Age = 40;
        }

        Assert.Equal(0, changeCount);
        Assert.Equal("Suppressed", record.Name); // Value still changed
        Assert.Equal(40, record.Age);
    }

    [Fact]
    public void RxRecord_SuppressChangeNotifications_RestoresAfterDispose()
    {
        // Test: Notifications resume after suppression is disposed
        TestRecord record = new();
        int changeCount = 0;

        record.PropertyChanged += (s, e) => changeCount++;

        var suppression = record.SuppressChangeNotifications();
        record.Name = "Suppressed";
        Assert.Equal(0, changeCount);

        suppression.Dispose();

        record.Name = "Active";
        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void RxRecord_SuppressChangeNotifications_NestedSuppressionWorks()
    {
        // Test: Nested suppression requires all to be disposed
        TestRecord record = new();
        int changeCount = 0;

        record.PropertyChanged += (s, e) => changeCount++;

        using (record.SuppressChangeNotifications())
        using (record.SuppressChangeNotifications())
        {
            record.Name = "Nested";
        }

        Assert.Equal(0, changeCount);

        record.Name = "After";
        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void RxRecord_DelayChangeNotifications_BatchesNotifications()
    {
        // Test: Delayed notifications are batched until disposal
        TestRecord record = new();
        List<string> changedProperties = new();

        record.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        using (record.DelayChangeNotifications())
        {
            record.Name = "Delayed1";
            record.Age = 25;
            record.Name = "Delayed2";
            Assert.Empty(changedProperties); // No notifications yet
        }

        // After disposal, all unique properties are notified
        Assert.Equal(2, changedProperties.Count);
        Assert.Contains(nameof(TestRecord.Name), changedProperties);
        Assert.Contains(nameof(TestRecord.Age), changedProperties);
    }

    [Fact]
    public void RxRecord_DelayChangeNotifications_DeduplicatesPropertyNames()
    {
        // Test: Multiple changes to same property result in single notification
        TestRecord record = new();
        int changeCount = 0;

        record.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TestRecord.Name))
            {
                changeCount++;
            }
        };

        using (record.DelayChangeNotifications())
        {
            record.Name = "Value1";
            record.Name = "Value2";
            record.Name = "Value3";
        }

        Assert.Equal(1, changeCount); // Only one notification for Name
    }

    [Fact]
    public void RxRecord_DelayChangeNotifications_NestedDelayWorks()
    {
        // Test: Nested delays batch until all are disposed
        TestRecord record = new();
        List<string> changedProperties = new();

        record.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        using (record.DelayChangeNotifications())
        {
            record.Name = "Outer";

            using (record.DelayChangeNotifications())
            {
                record.Age = 30;
                Assert.Empty(changedProperties);
            }

            Assert.Empty(changedProperties); // Still delayed
        }

        Assert.Equal(2, changedProperties.Count);
    }

    [Fact]
    public void RxRecord_AreChangeNotificationsEnabled_ReflectsState()
    {
        // Test: Method correctly reports notification state
        TestRecord record = new();

        Assert.True(record.AreChangeNotificationsEnabled());

        using (record.SuppressChangeNotifications())
        {
            Assert.False(record.AreChangeNotificationsEnabled());
        }

        Assert.True(record.AreChangeNotificationsEnabled());

        using (record.DelayChangeNotifications())
        {
            Assert.False(record.AreChangeNotificationsEnabled());
        }

        Assert.True(record.AreChangeNotificationsEnabled());
    }

    [Fact]
    public void RxRecord_ReferenceTypeProperty_ChangesCorrectly()
    {
        // Test: Reference type properties work correctly
        TestRecord parent = new();
        TestRecord child1 = new() { Name = "Child1" };
        TestRecord child2 = new() { Name = "Child2" };

        int changeCount = 0;
        parent.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TestRecord.Child))
            {
                changeCount++;
            }
        };

        parent.Child = child1;
        Assert.Equal(1, changeCount);

        parent.Child = child2;
        Assert.Equal(2, changeCount);

        parent.Child = child2; // Same reference
        Assert.Equal(2, changeCount); // No change
    }

    [Fact]
    public void RxRecord_MultipleSubscribers_AllReceiveNotifications()
    {
        // Test: Multiple subscribers all receive events
        TestRecord record = new();
        int count1 = 0;
        int count2 = 0;
        int count3 = 0;

        record.PropertyChanged += (s, e) => count1++;
        record.PropertyChanged += (s, e) => count2++;
        using var sub = record.Changed.Subscribe(_ => count3++);

        record.Name = "Test";

        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
        Assert.Equal(1, count3);
    }

    [Fact]
    public void RxRecord_ValueTypes_HandleDefaultValues()
    {
        // Test: Value types with default values work correctly
        TestRecord record = new() { Age = 0 };
        int changeCount = 0;

        record.PropertyChanged += (s, e) => changeCount++;

        record.Age = 0; // Setting to same default
        Assert.Equal(0, changeCount);

        record.Age = 1;
        Assert.Equal(1, changeCount);

        record.Age = 0; // Back to default
        Assert.Equal(2, changeCount);
    }

    [Fact]
    public void RxRecord_Dispose_SafetyCheck()
    {
        // Test: Record continues to work after subscriptions are disposed
        TestRecord record = new();
        int externalCount = 0;
        int observableCount = 0;

        record.PropertyChanged += (s, e) => externalCount++;
        var sub = record.Changed.Subscribe(_ => observableCount++);

        record.Name = "Before";
        Assert.Equal(1, externalCount);
        Assert.Equal(1, observableCount);

        sub.Dispose();

        record.Name = "After";
        Assert.Equal(2, externalCount); // Event still works
        Assert.Equal(1, observableCount); // Observable subscription disposed
    }
}
