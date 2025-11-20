
// Supporting types for Bind operator in R3.DynamicData

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq.Expressions;
using R3;
using R3.DynamicData.List;

namespace R3.DynamicData.Binding;

public class BindingOptions
{
    public const int DefaultResetThreshold = 50;

    public int ResetThreshold { get; set; } = DefaultResetThreshold;
}

public interface IObservableCollection<T> : IList<T>, INotifyCollectionChanged, INotifyPropertyChanged
{
}

public class ObservableCollectionExtended<T> : ObservableCollection<T>, IObservableCollection<T>
{
}

public class ObservableCollectionAdaptor<T>
{
    private readonly IObservableCollection<T> _target;
    private readonly BindingOptions _options;

    public ObservableCollectionAdaptor(IObservableCollection<T> target, BindingOptions options)
    {
        _target = target;
        _options = options;
    }

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

public static class NotifyPropertyChangedEx
{
    public static Observable<Unit> WhenAnyPropertyChanged<T>(this T source)
        where T : INotifyPropertyChanged
    {
        return Observable.FromEvent<PropertyChangedEventHandler, PropertyChangedEventArgs>(
            handler => (sender, args) => handler(args),
            h => source.PropertyChanged += h,
            h => source.PropertyChanged -= h)
            .Select(_ => Unit.Default);
    }

    public static Observable<PropertyValue<TObject, TProperty>> WhenPropertyChanged<TObject, TProperty>(
        this TObject source,
        Expression<Func<TObject, TProperty>> propertyAccessor,
        bool beforeChange = false)
        where TObject : INotifyPropertyChanged
    {
        var propertyName = GetPropertyName(propertyAccessor);
        return Observable.FromEvent<PropertyChangedEventHandler, PropertyChangedEventArgs>(
            handler => (sender, args) => handler(args),
            h => source.PropertyChanged += h,
            h => source.PropertyChanged -= h)
            .Where(args => args.PropertyName == propertyName)
            .Select(_ => new PropertyValue<TObject, TProperty>(source, propertyAccessor.Compile()(source)));
    }

    private static string GetPropertyName<TObject, TProperty>(Expression<Func<TObject, TProperty>> propertyAccessor)
    {
        if (propertyAccessor.Body is MemberExpression memberExpression && memberExpression.Member is System.Reflection.PropertyInfo)
        {
            return memberExpression.Member.Name;
        }

        throw new ArgumentException("Expression must be a property access expression", nameof(propertyAccessor));
    }
}

public class PropertyValue<TObject, TProperty>
{
    public PropertyValue(TObject sender, TProperty value)
    {
        Sender = sender;
        Value = value;
    }

    public TObject Sender { get; }

    public TProperty Value { get; }
}
