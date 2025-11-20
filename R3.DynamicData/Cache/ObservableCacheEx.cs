// Port of DynamicData to R3.

using System.Collections.ObjectModel;
using R3.DynamicData.Binding;
using R3.DynamicData.Kernel;

namespace R3.DynamicData.Cache;

/// <summary>
/// Observable cache extension methods.
/// </summary>
public static class ObservableCacheEx
{
    /// <summary>
    /// Binds a cache changeset to an IList, applying Add/Update/Remove operations.
    /// Note: SourceCache is inherently unordered, so items will be added/updated/removed
    /// without regard to position. Consider using Sort + Bind for ordered scenarios.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source changeset.</param>
    /// <param name="target">The target list to bind to.</param>
    /// <returns>A disposable to stop the binding.</returns>
    public static IDisposable Bind<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        IList<TObject> target)
        where TKey : notnull
    {
        return source.Subscribe(changeSet =>
        {
            foreach (var change in changeSet)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        target.Add(change.Current);
                        break;

                    case ChangeReason.Update:
                        // Update: remove old value and add new value
                        // Since cache is unordered, we remove by finding the previous item
                        if (change.Previous.HasValue)
                        {
                            target.Remove(change.Previous.Value);
                        }

                        target.Add(change.Current);
                        break;

                    case ChangeReason.Remove:
                        target.Remove(change.Current);
                        break;

                    case ChangeReason.Refresh:
                        // Refresh doesn't require action for basic binding
                        break;

                    case ChangeReason.Moved:
                        // Moved is not applicable to unordered cache binding
                        break;
                }
            }
        });
    }

    /// <summary>
    /// Binds a cache changeset to an IObservableCollection, with reset threshold support.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source changeset.</param>
    /// <param name="targetCollection">The target observable collection to bind to.</param>
    /// <param name="resetThreshold">The threshold for resetting the collection instead of applying individual changes.</param>
    /// <returns>A disposable to stop the binding.</returns>
    public static IDisposable Bind<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        IObservableCollection<TObject> targetCollection,
        int resetThreshold = BindingOptions.DefaultResetThreshold)
        where TKey : notnull
    {
        var options = new BindingOptions { ResetThreshold = resetThreshold };
        var adaptor = new ObservableCollectionCacheAdaptor<TObject, TKey>(targetCollection, options);
        return source.Subscribe(changes => adaptor.Adapt(changes));
    }

    /// <summary>
    /// Binds a cache changeset to a ReadOnlyObservableCollection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source changeset.</param>
    /// <param name="readOnlyObservableCollection">The resulting read-only observable collection.</param>
    /// <param name="resetThreshold">The threshold for resetting the collection instead of applying individual changes.</param>
    /// <returns>A disposable to stop the binding.</returns>
    public static IDisposable Bind<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        int resetThreshold = BindingOptions.DefaultResetThreshold)
        where TKey : notnull
    {
        var target = new ObservableCollectionExtended<TObject>();
        readOnlyObservableCollection = new ReadOnlyObservableCollection<TObject>(target);
        var options = new BindingOptions { ResetThreshold = resetThreshold };
        var adaptor = new ObservableCollectionCacheAdaptor<TObject, TKey>(target, options);
        return source.Subscribe(changes => adaptor.Adapt(changes));
    }
}
