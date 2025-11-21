// Port of DynamicData to R3.

using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using R3.DynamicData.Kernel;

namespace R3.DynamicData.Cache;

public static partial class ObservableCacheEx
{
    /// <summary>
    /// Monitors property changes on cache objects and emits values when the specified property changes.
    /// Objects must implement INotifyPropertyChanged.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the property value.</typeparam>
    /// <param name="source">The source cache observable.</param>
    /// <param name="propertyAccessor">Expression to access the property (e.g., x => x.Name).</param>
    /// <param name="notifyOnInitialValue">If true, emits initial property value for each object.</param>
    /// <returns>Observable that emits property change notifications.</returns>
    public static Observable<PropertyValue<TObject, TValue>> WhenValueChanged<TObject, TKey, TValue>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Expression<Func<TObject, TValue>> propertyAccessor,
        bool notifyOnInitialValue = true)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        var propertyName = GetPropertyName(propertyAccessor);
        var getter = propertyAccessor.Compile();

        return Observable.Create<PropertyValue<TObject, TValue>>(observer =>
        {
            var trackedObjects = new Dictionary<TKey, (TObject obj, PropertyChangedEventHandler handler)>();
            var keySelector = GetKeySelector<TObject, TKey>();

            var subscription = source.Subscribe(
                changeSet =>
                {
                    try
                    {
                        foreach (var change in changeSet)
                        {
                            switch (change.Reason)
                            {
                                case ChangeReason.Add:
                                case ChangeReason.Update:
                                    var obj = change.Current;
                                    var key = keySelector(obj);

                                    // Remove old handler if updating
                                    if (trackedObjects.TryGetValue(key, out var existing))
                                    {
                                        existing.obj.PropertyChanged -= existing.handler;
                                    }

                                    // Create handler
                                    PropertyChangedEventHandler handler = (sender, e) =>
                                    {
                                        if (e.PropertyName == propertyName || string.IsNullOrEmpty(e.PropertyName))
                                        {
                                            var currentValue = getter(obj);
                                            observer.OnNext(new PropertyValue<TObject, TValue>(obj, currentValue));
                                        }
                                    };

                                    obj.PropertyChanged += handler;
                                    trackedObjects[key] = (obj, handler);

                                    // Emit initial value if requested
                                    if (notifyOnInitialValue)
                                    {
                                        var initialValue = getter(obj);
                                        observer.OnNext(new PropertyValue<TObject, TValue>(obj, initialValue));
                                    }

                                    break;

                                case ChangeReason.Remove:
                                    var removeKey = keySelector(change.Current);
                                    if (trackedObjects.TryGetValue(removeKey, out var removed))
                                    {
                                        removed.obj.PropertyChanged -= removed.handler;
                                        trackedObjects.Remove(removeKey);
                                    }

                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        observer.OnErrorResume(ex);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);

            return Disposable.Create(() =>
            {
                // Clean up all handlers
                foreach (var (_, (obj, handler)) in trackedObjects)
                {
                    obj.PropertyChanged -= handler;
                }

                trackedObjects.Clear();
                subscription.Dispose();
            });
        });
    }

    /// <summary>
    /// Monitors property changes on cache objects, emitting both previous and current values.
    /// Objects must implement INotifyPropertyChanged.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the property value.</typeparam>
    /// <param name="source">The source cache observable.</param>
    /// <param name="propertyAccessor">Expression to access the property (e.g., x => x.Name).</param>
    /// <returns>Observable that emits property change notifications with previous and current values.</returns>
    public static Observable<PropertyValueChange<TObject, TValue>> WhenValueChangedWithPrevious<TObject, TKey, TValue>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Expression<Func<TObject, TValue>> propertyAccessor)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        var propertyName = GetPropertyName(propertyAccessor);
        var getter = propertyAccessor.Compile();

        return Observable.Create<PropertyValueChange<TObject, TValue>>(observer =>
        {
            var trackedObjects = new Dictionary<TKey, (TObject obj, TValue currentValue, PropertyChangedEventHandler handler)>();
            var keySelector = GetKeySelector<TObject, TKey>();

            var subscription = source.Subscribe(
                changeSet =>
                {
                    try
                    {
                        foreach (var change in changeSet)
                        {
                            switch (change.Reason)
                            {
                                case ChangeReason.Add:
                                case ChangeReason.Update:
                                    var obj = change.Current;
                                    var key = keySelector(obj);
                                    var currentValue = getter(obj);

                                    // Remove old handler if updating
                                    if (trackedObjects.TryGetValue(key, out var existing))
                                    {
                                        existing.obj.PropertyChanged -= existing.handler;
                                    }

                                    // Create handler (declare first to allow capture in lambda)
                                    PropertyChangedEventHandler? handler = null;
                                    handler = (sender, e) =>
                                    {
                                        if (e.PropertyName == propertyName || string.IsNullOrEmpty(e.PropertyName))
                                        {
                                            if (trackedObjects.TryGetValue(key, out var tracked))
                                            {
                                                var previousValue = tracked.currentValue;
                                                var newValue = getter(obj);

                                                observer.OnNext(new PropertyValueChange<TObject, TValue>(
                                                    obj, previousValue, newValue));

                                                // Update tracked value
                                                trackedObjects[key] = (obj, newValue, handler!);
                                            }
                                        }
                                    };

                                    obj.PropertyChanged += handler;
                                    trackedObjects[key] = (obj, currentValue, handler);

                                    break;

                                case ChangeReason.Remove:
                                    var removeKey = keySelector(change.Current);
                                    if (trackedObjects.TryGetValue(removeKey, out var removed))
                                    {
                                        removed.obj.PropertyChanged -= removed.handler;
                                        trackedObjects.Remove(removeKey);
                                    }

                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        observer.OnErrorResume(ex);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);

            return Disposable.Create(() =>
            {
                // Clean up all handlers
                foreach (var (_, (obj, _, handler)) in trackedObjects)
                {
                    obj.PropertyChanged -= handler;
                }

                trackedObjects.Clear();
                subscription.Dispose();
            });
        });
    }

    private static string GetPropertyName<TObject, TValue>(Expression<Func<TObject, TValue>> propertyAccessor)
    {
        if (propertyAccessor.Body is MemberExpression memberExpr)
        {
            return memberExpr.Member.Name;
        }

        throw new ArgumentException(
            $"Expression '{propertyAccessor}' must be a simple property accessor (e.g., x => x.PropertyName)",
            nameof(propertyAccessor));
    }

    private static Func<TObject, TKey> GetKeySelector<TObject, TKey>()
    {
        // Look for Id property first
        var idProperty = typeof(TObject).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        if (idProperty != null && idProperty.PropertyType == typeof(TKey))
        {
            return obj => (TKey)idProperty.GetValue(obj)!;
        }

        // Try to find a property with [Key] attribute or named like the type + "Id"
        var properties = typeof(TObject).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            if (prop.PropertyType == typeof(TKey))
            {
                // Check for KeyAttribute
                if (prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.KeyAttribute>() != null)
                {
                    return obj => (TKey)prop.GetValue(obj)!;
                }

                // Check for common naming patterns
                if (prop.Name == typeof(TObject).Name + "Id" || prop.Name == "Key")
                {
                    return obj => (TKey)prop.GetValue(obj)!;
                }
            }
        }

        throw new InvalidOperationException(
            $"Cannot determine key selector for type {typeof(TObject).Name}. " +
            "Please ensure the type has an 'Id' property, a property with [Key] attribute, or matches the pattern TypeNameId.");
    }
}

/// <summary>
/// Represents a property value notification.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TValue">The type of the property value.</typeparam>
public readonly record struct PropertyValue<TObject, TValue>(TObject Sender, TValue Value);

/// <summary>
/// Represents a property value change notification with previous and current values.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TValue">The type of the property value.</typeparam>
public readonly record struct PropertyValueChange<TObject, TValue>(TObject Sender, TValue Previous, TValue Current);
