using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using R3; // R3 observable extensions
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;
using R3.DynamicData.List; // For ListChangeReason if needed later
#pragma warning disable SA1208
#pragma warning disable SA1516
#pragma warning disable SA1501
#pragma warning disable SA1107
#pragma warning disable SA1503
#pragma warning disable SA1502
#pragma warning disable SA1513
#pragma warning disable SA1515

namespace R3Ext.Tests;

public class JoinTests
{
    private class LeftItem : INotifyPropertyChanged
    {
        private string _name;
        public int Id { get; }
        public string Name { get => _name; set { if (_name != value) { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } } }
        public event PropertyChangedEventHandler? PropertyChanged;
        public LeftItem(int id, string name) { Id = id; _name = name; }
    }

    private class RightItem : INotifyPropertyChanged
    {
        private int _value;
        public int Id { get; }
        public int Value { get => _value; set { if (_value != value) { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); } } }
        public event PropertyChangedEventHandler? PropertyChanged;
        public RightItem(int id, int value) { Id = id; _value = value; }
    }

    private readonly record struct Combined(int Id, string Name, int Value);

    [Fact]
    public void InnerJoin_AddsUpdatesAndRemoves()
    {
        var left = new SourceCache<LeftItem, int>(l => l.Id);
        var right = new SourceCache<RightItem, int>(r => r.Id);

        var captured = new List<IChangeSet<Combined, int>>();
        var sub = left.Connect()
            .InnerJoin(right.Connect(), (l, r) => new Combined(l.Id, l.Name, r.Value))
            .Subscribe(cs => captured.Add(cs));

        // Add only left -> no join emitted
        left.AddOrUpdate(new LeftItem(1, "A"));
        Assert.DoesNotContain(captured, cs => cs.Any());

        // Add right -> pair emitted
        right.AddOrUpdate(new RightItem(1, 10));
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Add && c.Key == 1);

        // Update left -> update emitted
        var l1 = left.Lookup(1).Value!;
        l1.Name = "A1"; left.AddOrUpdate(l1); // SourceCache will emit update
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Update && c.Key == 1);

        // Update right -> update emitted
        var r1 = right.Lookup(1).Value!;
        r1.Value = 11; right.AddOrUpdate(r1);
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Update && c.Key == 1);

        // Remove left -> remove emitted
        left.Remove(1);
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Remove && c.Key == 1);

        sub.Dispose();
    }

    [Fact]
    public void LeftJoin_IncludesAllLeftItems()
    {
        var left = new SourceCache<LeftItem, int>(l => l.Id);
        var right = new SourceCache<RightItem, int>(r => r.Id);

        var captured = new List<IChangeSet<Combined, int>>();
        var sub = left.Connect()
            .LeftJoin(right.Connect(), (l, r) => new Combined(l.Id, l.Name, r?.Value ?? 0))
            .Subscribe(cs => captured.Add(cs));

        // Add left with no match -> emit with zero value
        left.AddOrUpdate(new LeftItem(1, "A"));
        Assert.Single(captured);
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Add && c.Key == 1 && c.Current.Value == 0);

        // Add right -> update emitted (left already present)
        right.AddOrUpdate(new RightItem(1, 10));
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Update && c.Key == 1 && c.Current.Value == 10);

        // Remove right -> update emitted back to default
        right.Remove(1);
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Update && c.Key == 1 && c.Current.Value == 0);

        // Remove left -> remove emitted
        left.Remove(1);
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Remove && c.Key == 1);

        sub.Dispose();
    }

    [Fact]
    public void RightJoin_IncludesAllRightItems()
    {
        var left = new SourceCache<LeftItem, int>(l => l.Id);
        var right = new SourceCache<RightItem, int>(r => r.Id);

        var captured = new List<IChangeSet<Combined, int>>();
        var sub = left.Connect()
            .RightJoin(right.Connect(), (l, r) => new Combined(r.Id, l?.Name ?? "None", r.Value))
            .Subscribe(cs => captured.Add(cs));

        // Add right with no left match -> emit with default name
        right.AddOrUpdate(new RightItem(1, 10));
        Assert.Single(captured);
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Add && c.Key == 1 && c.Current.Name == "None");

        // Add left -> update emitted
        left.AddOrUpdate(new LeftItem(1, "A"));
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Update && c.Key == 1 && c.Current.Name == "A");

        // Remove left -> update back to default name
        left.Remove(1);
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Update && c.Key == 1 && c.Current.Name == "None");

        // Remove right -> remove emitted
        right.Remove(1);
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Remove && c.Key == 1);

        sub.Dispose();
    }

    [Fact]
    public void FullOuterJoin_IncludesAllKeys()
    {
        var left = new SourceCache<LeftItem, int>(l => l.Id);
        var right = new SourceCache<RightItem, int>(r => r.Id);

        var captured = new List<IChangeSet<Combined, int>>();
        var sub = left.Connect()
            .FullOuterJoin(right.Connect(), (l, r) => new Combined(l?.Id ?? r!.Id, l?.Name ?? "None", r?.Value ?? 0))
            .Subscribe(cs => captured.Add(cs));

        // Add left only
        left.AddOrUpdate(new LeftItem(1, "A"));
        Assert.Single(captured);
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Add && c.Key == 1 && c.Current.Name == "A" && c.Current.Value == 0);

        // Add right only (different key)
        right.AddOrUpdate(new RightItem(2, 10));
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Add && c.Key == 2 && c.Current.Name == "None" && c.Current.Value == 10);

        // Add matching right
        right.AddOrUpdate(new RightItem(1, 20));
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Update && c.Key == 1 && c.Current.Value == 20);

        // Remove left (right 1 still present)
        left.Remove(1);
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Update && c.Key == 1 && c.Current.Name == "None");

        // Remove all right
        right.Clear();
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Remove && c.Key == 1);
        Assert.Contains(captured.Last(), c => c.Reason == ChangeReason.Remove && c.Key == 2);

        sub.Dispose();
    }
}
