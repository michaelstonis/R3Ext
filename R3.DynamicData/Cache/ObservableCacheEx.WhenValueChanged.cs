// Port of DynamicData to R3.
// Uses R3Ext's AOT-compatible WhenChanged operator for property monitoring.

using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using R3.DynamicData.Kernel;
using R3Ext;

namespace R3.DynamicData.Cache;

public static partial class ObservableCacheEx
{
    /// <summary>
    /// Extracts property path from lambda expression for source generator key matching.
    /// Converts Expression&lt;Func&lt;T, TValue&gt;&gt; to "p => p.PropertyName" format to match CallerArgumentExpression output.
    /// </summary>
    private static string ExtractPropertyPath<TObject, TValue>(Expression<Func<TObject, TValue>> expression)
    {
        if (expression.Body is MemberExpression memberExpr)
        {
            var parameterName = expression.Parameters[0].Name ?? "p";

            // Format as "p => p.PropertyName" to match what CallerArgumentExpression generates
            return $"{parameterName} => {parameterName}.{memberExpr.Member.Name}";
        }

        throw new ArgumentException($"Expression must be a simple member access. Got: {expression}", nameof(expression));
    }

    /// <summary>
    /// Monitors property changes on cache objects and emits values when the specified property changes.
    /// Uses R3Ext's source-generated WhenChanged operator for AOT-compatible property monitoring.
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
        // Extract property path from expression for source generator matching
        var expressionPath = ExtractPropertyPath(propertyAccessor);
        return new WhenValueChangedOperator<TObject, TKey, TValue>(source, propertyAccessor, notifyOnInitialValue, expressionPath);
    }

    /// <summary>
    /// Monitors property changes on cache objects, emitting both previous and current values.
    /// Uses R3Ext's source-generated WhenChanged operator for AOT-compatible property monitoring.
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
        // Must use notifyOnInitialValue: true so Pairwise() has the initial value to work with
        return source.WhenValueChanged(propertyAccessor, notifyOnInitialValue: true)
            .Select(pv => (obj: pv.Sender, value: pv.Value))
            .Pairwise()
            .Select(pair => new PropertyValueChange<TObject, TValue>(
                pair.Current.obj,
                pair.Previous.value,
                pair.Current.value));
    }

    internal static Func<TObject, TKey> GetKeySelector<TObject, TKey>()
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

// Custom operator implementation to avoid closures and use R3Ext's WhenChanged
internal sealed class WhenValueChangedOperator<TObject, TKey, TValue> : Observable<PropertyValue<TObject, TValue>>
    where TObject : INotifyPropertyChanged
    where TKey : notnull
{
    private readonly Observable<IChangeSet<TObject, TKey>> _source;
    private readonly Expression<Func<TObject, TValue>> _propertyAccessor;
    private readonly bool _notifyOnInitialValue;
    private readonly string _expressionPath;

    public WhenValueChangedOperator(
        Observable<IChangeSet<TObject, TKey>> source,
        Expression<Func<TObject, TValue>> propertyAccessor,
        bool notifyOnInitialValue,
        string expressionPath)
    {
        _source = source;
        _propertyAccessor = propertyAccessor;
        _notifyOnInitialValue = notifyOnInitialValue;
        _expressionPath = expressionPath;
    }

    protected override IDisposable SubscribeCore(Observer<PropertyValue<TObject, TValue>> observer)
    {
        return new _WhenValueChanged(observer, _source, _propertyAccessor, _notifyOnInitialValue, _expressionPath).Run();
    }

    private sealed class _WhenValueChanged : IDisposable
    {
        private readonly Observer<PropertyValue<TObject, TValue>> _observer;
        private readonly Observable<IChangeSet<TObject, TKey>> _source;
        private readonly Expression<Func<TObject, TValue>> _propertyAccessor;
        private readonly bool _notifyOnInitialValue;
        private readonly string _expressionPath;
        private readonly Dictionary<TKey, IDisposable> _subscriptions = new();
        private readonly Func<TObject, TKey> _keySelector;
        private IDisposable? _sourceSubscription;

        public _WhenValueChanged(
            Observer<PropertyValue<TObject, TValue>> observer,
            Observable<IChangeSet<TObject, TKey>> source,
            Expression<Func<TObject, TValue>> propertyAccessor,
            bool notifyOnInitialValue,
            string expressionPath)
        {
            _observer = observer;
            _source = source;
            _propertyAccessor = propertyAccessor;
            _notifyOnInitialValue = notifyOnInitialValue;
            _expressionPath = expressionPath;
            _keySelector = ObservableCacheEx.GetKeySelector<TObject, TKey>();
        }

        public IDisposable Run()
        {
            _sourceSubscription = _source.Subscribe(new SourceObserver(this));
            return this;
        }

        public void Dispose()
        {
            _sourceSubscription?.Dispose();
            foreach (var subscription in _subscriptions.Values)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();
        }

        private sealed class SourceObserver : Observer<IChangeSet<TObject, TKey>>
        {
            private readonly _WhenValueChanged _parent;

            public SourceObserver(_WhenValueChanged parent)
            {
                _parent = parent;
            }

            protected override void OnNextCore(IChangeSet<TObject, TKey> changeSet)
            {
                foreach (var change in changeSet)
                {
                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                        case ChangeReason.Update:
                            var obj = change.Current;
                            var key = _parent._keySelector(obj);

                            // Remove old subscription if updating
                            if (_parent._subscriptions.TryGetValue(key, out var existing))
                            {
                                existing.Dispose();
                            }

                            // Subscribe to property changes using R3Ext's WhenChanged with explicit path
                            // R3Ext's WhenChanged emits the current value immediately if notifyOnInitialValue is true
                            var propertyObservable = obj.WhenChangedWithPath(_parent._propertyAccessor, _parent._expressionPath);

                            // Skip initial emission if notifyOnInitialValue is false
                            if (!_parent._notifyOnInitialValue)
                            {
                                propertyObservable = propertyObservable.Skip(1);
                            }

                            var subscription = propertyObservable.Subscribe(new PropertyObserver(_parent, obj));
                            _parent._subscriptions[key] = subscription;

                            break;

                        case ChangeReason.Remove:
                            var removeKey = _parent._keySelector(change.Current);
                            if (_parent._subscriptions.TryGetValue(removeKey, out var removed))
                            {
                                removed.Dispose();
                                _parent._subscriptions.Remove(removeKey);
                            }

                            break;
                    }
                }
            }

            protected override void OnErrorResumeCore(Exception error)
            {
                _parent._observer.OnErrorResume(error);
            }

            protected override void OnCompletedCore(Result result)
            {
                _parent._observer.OnCompleted(result);
            }
        }

        private sealed class PropertyObserver : Observer<TValue>
        {
            private readonly _WhenValueChanged _parent;
            private readonly TObject _obj;

            public PropertyObserver(_WhenValueChanged parent, TObject obj)
            {
                _parent = parent;
                _obj = obj;
            }

            protected override void OnNextCore(TValue value)
            {
                _parent._observer.OnNext(new PropertyValue<TObject, TValue>(_obj, value));
            }

            protected override void OnErrorResumeCore(Exception error)
            {
                _parent._observer.OnErrorResume(error);
            }

            protected override void OnCompletedCore(Result result)
            {
                // Property observables don't complete, they're managed by the cache lifecycle
            }
        }
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
