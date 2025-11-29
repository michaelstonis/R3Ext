using System.ComponentModel;
using System.Runtime.CompilerServices;
using R3;

namespace R3Ext;

/// <summary>
/// Immutable-style record variant providing same reactive capabilities; property setters still invoke raise helpers.
/// </summary>
#pragma warning disable CA1001
public abstract record RxRecord : INotifyPropertyChanged, INotifyPropertyChanging
#pragma warning restore CA1001
{
    private readonly Subject<PropertyChangingEventArgs> _changing = new();
    private readonly Subject<PropertyChangedEventArgs> _changed = new();
    private int _suppressCount;
    private int _delayCount;
    private HashSet<string>? _delayedProperties;

    private bool NotificationsEnabled => _suppressCount == 0;

    public Observable<PropertyChangingEventArgs> Changing => _changing.AsObservable();

    public Observable<PropertyChangedEventArgs> Changed => _changed.AsObservable();

    public event PropertyChangingEventHandler? PropertyChanging;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool RaiseAndSetIfChanged<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        this.RaisePropertyChanging(propertyName);
        field = value!;
        this.RaisePropertyChanged(propertyName);
        return true;
    }

    public void RaisePropertyChanging(string propertyName)
    {
        if (_suppressCount > 0)
        {
            return;
        }

        if (_delayCount > 0)
        {
            return;
        }

        PropertyChangingEventArgs args = new(propertyName);
        this.PropertyChanging?.Invoke(this, args);
        _changing.OnNext(args);
    }

    public void RaisePropertyChanged(string propertyName)
    {
        if (_suppressCount > 0)
        {
            return;
        }

        if (_delayCount > 0)
        {
            _delayedProperties ??= new HashSet<string>();
            _delayedProperties.Add(propertyName);
            return;
        }

        PropertyChangedEventArgs args = new(propertyName);
        this.PropertyChanged?.Invoke(this, args);
        _changed.OnNext(args);
    }

    public IDisposable SuppressChangeNotifications()
    {
        Interlocked.Increment(ref _suppressCount);
        return new SuppressDisposable(this);
    }

    public IDisposable DelayChangeNotifications()
    {
        Interlocked.Increment(ref _delayCount);
        return new DelayDisposable(this);
    }

    public bool AreChangeNotificationsEnabled()
    {
        return NotificationsEnabled && _delayCount == 0;
    }

    private sealed class SuppressDisposable(RxRecord owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            Interlocked.Decrement(ref owner._suppressCount);
        }
    }

    private sealed class DelayDisposable(RxRecord owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            if (Interlocked.Decrement(ref owner._delayCount) == 0 && owner._delayedProperties is { Count: > 0, })
            {
                foreach (string prop in owner._delayedProperties)
                {
                    PropertyChangedEventArgs args = new(prop);
                    owner.PropertyChanged?.Invoke(owner, args);
                    owner._changed.OnNext(args);
                }

                owner._delayedProperties.Clear();
            }
        }
    }
}
