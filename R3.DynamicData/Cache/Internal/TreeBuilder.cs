// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Port of DynamicData TreeBuilder to R3.
// This complex operator builds and maintains hierarchical tree structures from flat changesets.

using System;
using System.Linq;
using R3;
using R3.DynamicData.Kernel;
using R3.DynamicData.Operators;

namespace R3.DynamicData.Cache.Internal;

/// <summary>
/// Internal class that builds and maintains a tree structure from a flat changeset.
/// Algorithm:
/// 1. Synchronize and cache all source data.
/// 2. Transform each object into a Node (with Parent/Children references).
/// 3. GroupOn nodes by pivot: pivots form the basis of hierarchy.
/// 4. Maintain UpdateChildren: when parent changes update its Children collection.
/// 5. Filter result based on predicate (default: root nodes only).
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
internal sealed class TreeBuilder<TObject, TKey>
    where TObject : class
    where TKey : notnull
{
    private readonly Func<TObject, TKey> _pivotOn;
    private readonly Observable<Func<Node<TObject, TKey>, bool>> _predicateChanged;
    private readonly Observable<IChangeSet<TObject, TKey>> _source;

    private static Func<Node<TObject, TKey>, bool> DefaultPredicate => node => node.IsRoot;

    public TreeBuilder(
        Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TKey> pivotOn,
        Observable<Func<Node<TObject, TKey>, bool>>? predicateChanged)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _pivotOn = pivotOn ?? throw new ArgumentNullException(nameof(pivotOn));
        _predicateChanged = predicateChanged ?? Observable.Return(DefaultPredicate);
    }

    public Observable<IChangeSet<Node<TObject, TKey>, TKey>> Run()
    {
        return Observable.Create<IChangeSet<Node<TObject, TKey>, TKey>>(
            observer =>
            {
                var locker = new object();
                var reFilterSubject = new Subject<Unit>();

                // Step 1: Cache all source data synchronized
                var allData = _source.Synchronize(locker).AsObservableCache();

                // Step 2: Transform each object into a Node
                var allNodes = allData.Connect()
                    .Synchronize(locker)
                    .Transform((t, key) => new Node<TObject, TKey>(t, key))
                    .AsObservableCache();

                // Step 3: Group nodes by their parent key (pivotOn returns the parent key for each node)
                var groupedByPivot = allNodes.Connect()
                    .Synchronize(locker)
                    .GroupOn(node => _pivotOn(node.Item))
                    .AsObservableCache();

                // Step 4: Maintain parent-child relationships by watching all node changes
                var allNodesSubscription = allNodes.Connect()
                    .Synchronize(locker)
                    .Subscribe(changes =>
                    {
                        foreach (var change in changes)
                        {
                            // Update the node's children based on grouped data
                            UpdateChildren(change.Current);

                            // If it's being removed, clear all its children's parent references
                            if (change.Reason == ChangeReason.Remove)
                            {
                                var children = change.Current.Children.Items.ToList();
                                change.Current.Update(updater => updater.Remove(children.Select(c => c.Key)));
                                foreach (var child in children)
                                {
                                    child.Parent = Optional<Node<TObject, TKey>>.None;
                                }
                            }
                        }
                    });

                // Step 5: Filter the tree based on the predicate
                // Combine predicate changes with refilter trigger
                Func<Node<TObject, TKey>, bool> currentPredicate = DefaultPredicate;
                var predicateSubscription = _predicateChanged
                    .Prepend(DefaultPredicate)
                    .Subscribe(pred =>
                    {
                        currentPredicate = pred;
                        reFilterSubject.OnNext(Unit.Default);
                    });

                var result = allNodes.Connect()
                    .Synchronize(locker)
                    .Filter(node => currentPredicate(node))
                    .Subscribe(observer.OnNext, observer.OnErrorResume, observer.OnCompleted);

                return Disposable.Create(() =>
                {
                    predicateSubscription.Dispose();
                    allNodesSubscription.Dispose();
                    result.Dispose();
                    reFilterSubject.Dispose();
                    allData.Dispose();
                    allNodes.Dispose();
                    groupedByPivot.Dispose();
                });

                // Helper to update a node's children based on the grouping
                void UpdateChildren(Node<TObject, TKey> parentNode)
                {
                    // Look up the group whose key matches this parent node's key
                    var childrenGroup = groupedByPivot.Lookup(parentNode.Key);
                    if (childrenGroup.HasValue && childrenGroup.Value != null)
                    {
                        // The group contains all nodes whose parent key equals this node's key
                        var children = childrenGroup.Value.Cache.Items.ToList();
                        parentNode.Update(updater => updater.AddOrUpdate(children));
                        foreach (var child in children)
                        {
                            child.Parent = Optional<Node<TObject, TKey>>.Some(parentNode);
                        }
                    }
                }
            });
    }
}
