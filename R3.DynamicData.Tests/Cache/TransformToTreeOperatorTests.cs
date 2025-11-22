// Tests for TransformToTree operator and TreeBuilder

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;
using Xunit;

namespace R3.DynamicData.Tests.Cache;

public sealed class TransformToTreeOperatorTests : IDisposable
{
    private readonly SourceCache<Employee, int> _source;
    private readonly List<IDisposable> _disposables = new();

    public TransformToTreeOperatorTests()
    {
        _source = new SourceCache<Employee, int>(e => e.Id);
        _disposables.Add(_source);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        _source.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TransformToTree_CreatesRootNodes()
    {
        // Arrange
        var ceo = new Employee(1, "CEO", null);
        var vp1 = new Employee(2, "VP1", 1);
        var vp2 = new Employee(3, "VP2", 1);

        _source.AddOrUpdate(new[] { ceo, vp1, vp2 });

        List<IChangeSet<Node<Employee, int>, int>>? results = null;

        // Act
        var subscription = _source.Connect()
            .TransformToTree(e => e.ManagerId ?? 0) // Root nodes have ManagerId == 0
            .Subscribe(changes =>
            {
                results ??= new List<IChangeSet<Node<Employee, int>, int>>();
                results.Add(changes);
            });
        _disposables.Add(subscription);

        // Assert
        Assert.NotNull(results);
        Assert.True(results!.Count > 0);

        // First emission should contain only the root (CEO)
        var firstChange = results[0];
        Assert.Equal(1, firstChange.Count);
        var ceoNode = firstChange.First();
        Assert.Equal("CEO", ceoNode.Current.Item.Name);
        Assert.True(ceoNode.Current.IsRoot);
        Assert.Equal(2, ceoNode.Current.Children.Count); // VP1 and VP2
    }

    [Fact]
    public void TransformToTree_BuildsCorrectHierarchy()
    {
        // Arrange
        var ceo = new Employee(1, "CEO", null);
        var vp = new Employee(2, "VP", 1);
        var manager = new Employee(3, "Manager", 2);
        var employee = new Employee(4, "Employee", 3);

        _source.AddOrUpdate(new[] { ceo, vp, manager, employee });

        Node<Employee, int>? rootNode = null;

        // Act
        var subscription = _source.Connect()
            .TransformToTree(e => e.ManagerId ?? 0)
            .Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    // CEO
                    if (change.Current.Item.Id == 1)
                    {
                        rootNode = change.Current;
                    }
                }
            });
        _disposables.Add(subscription);

        // Assert
        Assert.NotNull(rootNode);
        Assert.True(rootNode!.IsRoot);
        Assert.Equal(0, rootNode.Depth);
        Assert.Equal(1, rootNode.Children.Count); // VP

        var vpNode = rootNode.Children.Items.First();
        Assert.Equal("VP", vpNode.Item.Name);
        Assert.Equal(1, vpNode.Depth);
        Assert.Equal(1, vpNode.Children.Count); // Manager

        var managerNode = vpNode.Children.Items.First();
        Assert.Equal("Manager", managerNode.Item.Name);
        Assert.Equal(2, managerNode.Depth);
        Assert.Equal(1, managerNode.Children.Count); // Employee

        var employeeNode = managerNode.Children.Items.First();
        Assert.Equal("Employee", employeeNode.Item.Name);
        Assert.Equal(3, employeeNode.Depth);
        Assert.Equal(0, employeeNode.Children.Count);
    }

    [Fact]
    public void TransformToTree_HandlesOrphans()
    {
        // Arrange
        var ceo = new Employee(1, "CEO", null);
        var orphan = new Employee(99, "Orphan", 999); // Non-existent manager

        _source.AddOrUpdate(new[] { ceo, orphan });

        var allNodes = new List<Node<Employee, int>>();

        // Act
        var subscription = _source.Connect()
            .TransformToTree(e => e.ManagerId ?? 0, predicateChanged: null) // Get all nodes
            .Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    if (change.Reason == ChangeReason.Add)
                    {
                        allNodes.Add(change.Current);
                    }
                }
            });
        _disposables.Add(subscription);

        // Assert
        Assert.Equal(2, allNodes.Count);

        // CEO should be root
        var ceoNode = allNodes.FirstOrDefault(n => n.Item.Id == 1);
        Assert.NotNull(ceoNode);
        Assert.True(ceoNode!.IsRoot);

        // Orphan becomes a root since its parent doesn't exist
        var orphanNode = allNodes.FirstOrDefault(n => n.Item.Id == 99);
        Assert.NotNull(orphanNode);
        Assert.True(orphanNode!.IsRoot); // No parent found, so it's a root
    }

    [Fact]
    public void TransformToTree_UpdatesWhenParentChanges()
    {
        // Arrange
        var ceo = new Employee(1, "CEO", null);
        var vp1 = new Employee(2, "VP1", 1);
        var employee = new Employee(3, "Employee", 2);

        _source.AddOrUpdate(new[] { ceo, vp1, employee });

        List<IChangeSet<Node<Employee, int>, int>>? results = null;

        var subscription = _source.Connect()
            .TransformToTree(e => e.ManagerId ?? 0)
            .Subscribe(changes =>
            {
                results ??= new List<IChangeSet<Node<Employee, int>, int>>();
                results.Add(changes);
            });
        _disposables.Add(subscription);

        // Assert initial state - CEO is the root
        Assert.NotNull(results);
        var ceoNode = results![0].First().Current;
        Assert.Equal("CEO", ceoNode.Item.Name);
        Assert.True(ceoNode.IsRoot);

        // CEO should have VP1 as a child, and VP1 should have Employee as child
        var vp1Node = ceoNode.Children.Items.First(n => n.Item.Id == 2);
        Assert.Equal("VP1", vp1Node.Item.Name);
        Assert.Equal(1, vp1Node.Children.Count); // Employee
        var employeeNode = vp1Node.Children.Items.First();
        Assert.Equal("Employee", employeeNode.Item.Name);
        Assert.True(employeeNode.Parent.HasValue);
        Assert.Equal(2, employeeNode.Parent.Value.Item.Id); // Parent is VP1
    }

    [Fact]
    public void TransformToTree_HandlesRemoval()
    {
        // Arrange
        var ceo = new Employee(1, "CEO", null);
        var vp = new Employee(2, "VP", 1);
        var manager = new Employee(3, "Manager", 2);

        _source.AddOrUpdate(new[] { ceo, vp, manager });

        List<IChangeSet<Node<Employee, int>, int>>? results = null;

        var subscription = _source.Connect()
            .TransformToTree(e => e.ManagerId ?? 0)
            .Subscribe(changes =>
            {
                results ??= new List<IChangeSet<Node<Employee, int>, int>>();
                results.Add(changes);
            });
        _disposables.Add(subscription);

        // Assert initial state - verify 3-level hierarchy
        Assert.NotNull(results);
        var ceoNode = results![0].First().Current;
        Assert.Equal("CEO", ceoNode.Item.Name);
        Assert.True(ceoNode.IsRoot);

        var vpNode = ceoNode.Children.Items.First(n => n.Item.Id == 2);
        Assert.Equal("VP", vpNode.Item.Name);
        Assert.Equal(1, vpNode.Children.Count); // Manager

        var managerNode = vpNode.Children.Items.First();
        Assert.Equal("Manager", managerNode.Item.Name);
        Assert.True(managerNode.Parent.HasValue);
        Assert.Equal(2, managerNode.Parent.Value.Item.Id); // Parent is VP
    }

    [Fact]
    public void TransformToTree_CustomPredicateFiltersNodes()
    {
        // Arrange
        var ceo = new Employee(1, "CEO", null);
        var vp1 = new Employee(2, "VP1", 1);
        var vp2 = new Employee(3, "VP2", 1);
        var manager = new Employee(4, "Manager", 2);

        _source.AddOrUpdate(new[] { ceo, vp1, vp2, manager });

        List<IChangeSet<Node<Employee, int>, int>>? results = null;

        // Act - Use default predicate (root nodes only)
        var subscription = _source.Connect()
            .TransformToTree(e => e.ManagerId ?? 0)
            .Subscribe(changes =>
            {
                results ??= new List<IChangeSet<Node<Employee, int>, int>>();
                results.Add(changes);
            });
        _disposables.Add(subscription);

        // Assert - Only root node (CEO) should be emitted
        Assert.NotNull(results);
        var singleChange = Assert.Single(results![0]);
        Assert.Equal("CEO", singleChange.Current.Item.Name);
        Assert.True(singleChange.Current.IsRoot);

        // But the CEO should have all children in the hierarchy
        var ceoNode = singleChange.Current;
        Assert.Equal(2, ceoNode.Children.Count); // VP1 and VP2
        var vp1Node = ceoNode.Children.Items.First(n => n.Item.Id == 2);
        Assert.Equal("VP1", vp1Node.Item.Name);
        Assert.Equal(1, vp1Node.Children.Count); // Manager
    }

    [Fact]
    public void TransformToTree_MultipleRoots()
    {
        // Arrange
        var root1 = new Employee(1, "Root1", null);
        var root2 = new Employee(2, "Root2", null);
        var child1 = new Employee(3, "Child1", 1);
        var child2 = new Employee(4, "Child2", 2);

        _source.AddOrUpdate(new[] { root1, root2, child1, child2 });

        var rootNodes = new List<Node<Employee, int>>();

        // Act
        var subscription = _source.Connect()
            .TransformToTree(e => e.ManagerId ?? 0) // Default: root nodes only
            .Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    if (change.Reason == ChangeReason.Add && change.Current.IsRoot)
                    {
                        rootNodes.Add(change.Current);
                    }
                }
            });
        _disposables.Add(subscription);

        // Assert
        Assert.Equal(2, rootNodes.Count);
        var rootNames = rootNodes.Select(n => n.Item.Name).ToList();
        Assert.Contains("Root1", rootNames);
        Assert.Contains("Root2", rootNames);

        var root1Node = rootNodes.First(n => n.Item.Id == 1);
        Assert.Equal(1, root1Node.Children.Count);
        Assert.Equal("Child1", root1Node.Children.Items.First().Item.Name);

        var root2Node = rootNodes.First(n => n.Item.Id == 2);
        Assert.Equal(1, root2Node.Children.Count);
        Assert.Equal("Child2", root2Node.Children.Items.First().Item.Name);
    }

    [Fact]
    public void TransformToTree_EmptySource()
    {
        // Arrange
        var changeCount = 0;

        // Act
        var subscription = _source.Connect()
            .TransformToTree(e => e.ManagerId ?? 0)
            .Subscribe(_ => changeCount++);
        _disposables.Add(subscription);

        // Assert
        Assert.Equal(0, changeCount); // No changes emitted for empty source
    }

    // Test helper class
    private class Employee
    {
        public Employee(int id, string name, int? managerId)
        {
            Id = id;
            Name = name;
            ManagerId = managerId;
        }

        public int Id { get; }

        public string Name { get; }

        public int? ManagerId { get; }
    }
}
