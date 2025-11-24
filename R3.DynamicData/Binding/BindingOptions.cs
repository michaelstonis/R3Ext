// Supporting types for Bind operator in R3.DynamicData

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq.Expressions;
using R3;
using R3.DynamicData.Kernel;
using R3.DynamicData.List;
using R3Ext;

namespace R3.DynamicData.Binding;

/// <summary>
/// Options for binding operations.
/// </summary>
public class BindingOptions
{
    /// <summary>
    /// The default threshold for resetting a collection instead of applying individual changes.
    /// </summary>
    public const int DefaultResetThreshold = 50;

    /// <summary>
    /// Gets or sets the threshold for resetting a collection instead of applying individual changes.
    /// </summary>
    public int ResetThreshold { get; set; } = DefaultResetThreshold;
}

/// <summary>
/// An observable collection interface that combines IList, INotifyCollectionChanged, and INotifyPropertyChanged.
/// </summary>
/// <typeparam name="T">The type of elements in the collection.</typeparam>
public interface IObservableCollection<T> : IList<T>, INotifyCollectionChanged, INotifyPropertyChanged
{
}

/// <summary>
/// An extended observable collection implementation.
/// </summary>
/// <typeparam name="T">The type of elements in the collection.</typeparam>
public class ObservableCollectionExtended<T> : ObservableCollection<T>, IObservableCollection<T>
{
}

/// <summary>
/// Adaptor for binding list changesets to an observable collection.
/// </summary>
/// <typeparam name="T">The type of elements in the collection.</typeparam>
public class ObservableCollectionAdaptor<T>
{
    private readonly IObservableCollection<T> _target;
    private readonly BindingOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableCollectionAdaptor{T}"/> class.
    /// </summary>
    /// <param name="target">The target observable collection.</param>
    /// <param name="options">The binding options.</param>
    public ObservableCollectionAdaptor(IObservableCollection<T> target, BindingOptions options)
    {
        _target = target;
        _options = options;
    }

    /// <summary>
    /// Adapts a list changeset to the target collection.
    /// </summary>
    /// <param name="changes">The changeset to adapt.</param>
    public void Adapt(IChangeSet<T> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    _target.Insert(change.CurrentIndex, change.Item);
                    break;
                case ListChangeReason.AddRange:
                    int idx = change.CurrentIndex;
                    foreach (var item in change.Range)
                    {
                        _target.Insert(idx++, item);
                    }

                    break;
                case ListChangeReason.Remove:
                    _target.RemoveAt(change.CurrentIndex);
                    break;
                case ListChangeReason.RemoveRange:
                    for (int i = 0; i < change.Range.Count; i++)
                    {
                        _target.RemoveAt(change.CurrentIndex);
                    }

                    break;
                case ListChangeReason.Replace:
                    _target[change.CurrentIndex] = change.Item;
                    break;
                case ListChangeReason.Moved:
                    var movedItem = _target[change.PreviousIndex];
                    _target.RemoveAt(change.PreviousIndex);
                    _target.Insert(change.CurrentIndex, movedItem);
                    break;
                case ListChangeReason.Clear:
                    _target.Clear();
                    break;
                case ListChangeReason.Refresh:
                    break;
            }
        }
    }
}

/// <summary>
/// Extension methods for INotifyPropertyChanged.
/// </summary>
public static class NotifyPropertyChangedEx
{
    /// <summary>
    /// Creates an observable that emits when any property changes.
    /// </summary>
    /// <typeparam name="T">The type of the source object.</typeparam>
    /// <param name="source">The source object to observe.</param>
    /// <returns>An observable that emits Unit when any property changes.</returns>
    public static Observable<Unit> WhenAnyPropertyChanged<T>(this T source)
        where T : INotifyPropertyChanged
    {
        return Observable.FromEvent<PropertyChangedEventHandler, PropertyChangedEventArgs>(
            handler => (sender, args) => handler(args),
            h => source.PropertyChanged += h,
            h => source.PropertyChanged -= h)
            .Select(_ => Unit.Default);
    }

    /// <summary>
    /// Observes property changes on an object that implements INotifyPropertyChanged.
    /// Uses R3Ext's source-generated WhenChanged operator for AOT-compatible property monitoring.
    /// </summary>
    /// <typeparam name="TObject">The type of object to observe.</typeparam>
    /// <typeparam name="TProperty">The type of property to observe.</typeparam>
    /// <param name="source">The source object to observe.</param>
    /// <param name="propertyAccessor">An expression identifying the property to observe.</param>
    /// <param name="beforeChange">Whether to emit before the change (not currently supported).</param>
    /// <returns>An observable that emits PropertyValue when the specified property changes.</returns>
    public static Observable<PropertyValue<TObject, TProperty>> WhenPropertyChanged<TObject, TProperty>(
        this TObject source,
        Expression<Func<TObject, TProperty>> propertyAccessor,
        bool beforeChange = false)
        where TObject : INotifyPropertyChanged
    {
        // Use R3Ext's WhenChangedWithPath with explicit path extraction for AOT compatibility
        // Extract path from expression to pass explicitly (CallerArgumentExpression doesn't work through variables)
        string path = Cache.ObservableCacheEx.ExtractPropertyPath(propertyAccessor);

        // WhenChanged emits initial value, but WhenPropertyChanged should only emit on changes (not initial)
        // Skip(1) removes the initial value emission
        return source.WhenChangedWithPath(propertyAccessor, path)
            .Skip(1)
            .Select(value => new PropertyValue<TObject, TProperty>(source, value));
    }
}

/// <summary>
/// Represents a property value change.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TProperty">The type of the property.</typeparam>
public class PropertyValue<TObject, TProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyValue{TObject, TProperty}"/> class.
    /// </summary>
    /// <param name="sender">The object that raised the property change.</param>
    /// <param name="value">The new value of the property.</param>
    public PropertyValue(TObject sender, TProperty value)
    {
        Sender = sender;
        Value = value;
    }

    /// <summary>
    /// Gets the object that raised the property change.
    /// </summary>
    public TObject Sender { get; }

    /// <summary>
    /// Gets the new value of the property.
    /// </summary>
    public TProperty Value { get; }
}

/// <summary>
/// Adaptor for binding cache changesets to an observable collection.
/// </summary>
/// <typeparam name="TObject">The type of objects in the collection.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public class ObservableCollectionCacheAdaptor<TObject, TKey>
    where TKey : notnull
{
    private readonly IObservableCollection<TObject> _target;
    private readonly BindingOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableCollectionCacheAdaptor{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="target">The target observable collection.</param>
    /// <param name="options">The binding options.</param>
    public ObservableCollectionCacheAdaptor(IObservableCollection<TObject> target, BindingOptions options)
    {
        _target = target;
        _options = options;
    }

    /// <summary>
    /// Adapts a cache changeset to the target collection.
    /// </summary>
    /// <param name="changes">The changeset to adapt.</param>
    public void Adapt(Cache.IChangeSet<TObject, TKey> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ChangeReason.Add:
                    _target.Add(change.Current);
                    break;

                case ChangeReason.Update:
                    // For updates in an unordered collection, remove the old value and add the new one
                    if (change.Previous.HasValue)
                    {
                        _target.Remove(change.Previous.Value);
                    }

                    _target.Add(change.Current);
                    break;

                case ChangeReason.Remove:
                    _target.Remove(change.Current);
                    break;

                case ChangeReason.Refresh:
                    // Refresh doesn't require action for basic binding
                    break;

                case ChangeReason.Moved:
                    // Moved is not applicable to unordered cache binding
                    break;
            }
        }
    }
}
