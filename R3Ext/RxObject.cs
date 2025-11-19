using System.ComponentModel;
using System.Runtime.CompilerServices;
using R3;

namespace R3Ext;

/// <summary>
/// Lightweight port of ReactiveUI's ReactiveObject providing property change notifications
/// plus Changing/Changed reactive streams and notification suppression / delay semantics.
/// </summary>
#pragma warning disable CA1001
public abstract class RxObject : INotifyPropertyChanged, INotifyPropertyChanging
#pragma warning restore CA1001
{
    private readonly Subject<PropertyChangingEventArgs> _changing = new();
    private readonly Subject<PropertyChangedEventArgs> _changed = new();
    private int _suppressCount;
    private int _delayCount;
    private HashSet<string>? _delayedProperties;

    private bool NotificationsEnabled => _suppressCount == 0;

    /// <summary>
    /// Gets observable stream of PropertyChanging events.
    /// </summary>
    public Observable<PropertyChangingEventArgs> Changing => _changing.AsObservable();

    /// <summary>
    /// Gets observable stream of PropertyChanged events.
    /// </summary>
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
            return; // suppression discards
        }

        if (_delayCount > 0)
        {
            return; // delay skips changing events
        }

        PropertyChangingEventArgs args = new(propertyName);
        this.PropertyChanging?.Invoke(this, args);
        _changing.OnNext(args);
    }

    public void RaisePropertyChanged(string propertyName)
    {
        if (_suppressCount > 0)
        {
            return; // suppression discards
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

    /// <summary>
    /// Suppresses notifications; changes made inside the scope will not raise events.
    /// </summary>
    public IDisposable SuppressChangeNotifications()
    {
        Interlocked.Increment(ref _suppressCount);
        return new ActionDisposable(() => Interlocked.Decrement(ref _suppressCount));
    }

    /// <summary>
    /// Delays notifications; changes are aggregated and raised once when scope ends.
    /// </summary>
    public IDisposable DelayChangeNotifications()
    {
        Interlocked.Increment(ref _delayCount);
        return new ActionDisposable(() =>
        {
            if (Interlocked.Decrement(ref _delayCount) == 0 && _delayedProperties is { Count: > 0, })
            {
                foreach (string prop in _delayedProperties)
                {
                    PropertyChangedEventArgs args = new(prop);
                    this.PropertyChanged?.Invoke(this, args);
                    _changed.OnNext(args);
                }

                _delayedProperties.Clear();
            }
        });
    }

    /// <summary>
    /// Indicates whether notifications are currently enabled.
    /// </summary>
    public bool AreChangeNotificationsEnabled()
    {
        return NotificationsEnabled && _delayCount == 0;
    }

    private sealed class ActionDisposable(Action onDispose) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            onDispose();
        }
    }
}

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
        return new ActionDisposable(() => Interlocked.Decrement(ref _suppressCount));
    }

    public IDisposable DelayChangeNotifications()
    {
        Interlocked.Increment(ref _delayCount);
        return new ActionDisposable(() =>
        {
            if (Interlocked.Decrement(ref _delayCount) == 0 && _delayedProperties is { Count: > 0, })
            {
                foreach (string prop in _delayedProperties)
                {
                    PropertyChangedEventArgs args = new(prop);
                    this.PropertyChanged?.Invoke(this, args);
                    _changed.OnNext(args);
                }

                _delayedProperties.Clear();
            }
        });
    }

    public bool AreChangeNotificationsEnabled()
    {
        return NotificationsEnabled && _delayCount == 0;
    }

    private sealed class ActionDisposable(Action onDispose) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            onDispose();
        }
    }
}
