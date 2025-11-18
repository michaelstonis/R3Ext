using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using R3;

namespace R3Ext;

/// <summary>
/// Lightweight port of ReactiveUI's ReactiveObject providing property change notifications
/// plus Changing/Changed reactive streams and notification suppression / delay semantics.
/// </summary>
public abstract class RxObject : INotifyPropertyChanged, INotifyPropertyChanging
{
    private readonly Subject<PropertyChangingEventArgs> _changing = new();
    private readonly Subject<PropertyChangedEventArgs> _changed = new();
    private int _suppressCount;
    private int _delayCount;
    private HashSet<string>? _delayedProperties;
    private bool NotificationsEnabled => _suppressCount == 0;

    /// <summary>
    /// Observable stream of PropertyChanging events.
    /// </summary>
    public Observable<PropertyChangingEventArgs> Changing => _changing.AsObservable();
    /// <summary>
    /// Observable stream of PropertyChanged events.
    /// </summary>
    public Observable<PropertyChangedEventArgs> Changed => _changed.AsObservable();

    public event PropertyChangingEventHandler? PropertyChanging;
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool RaiseAndSetIfChanged<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        RaisePropertyChanging(propertyName);
        field = value!;
        RaisePropertyChanged(propertyName);
        return true;
    }

    public void RaisePropertyChanging(string propertyName)
    {
        if (_suppressCount > 0) return; // suppression discards
        if (_delayCount > 0) return;    // delay skips changing events
        var args = new PropertyChangingEventArgs(propertyName);
        PropertyChanging?.Invoke(this, args);
        _changing.OnNext(args);
    }

    public void RaisePropertyChanged(string propertyName)
    {
        if (_suppressCount > 0) return; // suppression discards
        if (_delayCount > 0)
        {
            _delayedProperties ??= new HashSet<string>();
            _delayedProperties.Add(propertyName);
            return;
        }
        var args = new PropertyChangedEventArgs(propertyName);
        PropertyChanged?.Invoke(this, args);
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
            if (Interlocked.Decrement(ref _delayCount) == 0 && _delayedProperties is { Count: > 0 })
            {
                foreach (var prop in _delayedProperties)
                {
                    var args = new PropertyChangedEventArgs(prop);
                    PropertyChanged?.Invoke(this, args);
                    _changed.OnNext(args);
                }
                _delayedProperties.Clear();
            }
        });
    }

    /// <summary>
    /// Indicates whether notifications are currently enabled.
    /// </summary>
    public bool AreChangeNotificationsEnabled() => NotificationsEnabled && _delayCount == 0;

    private sealed class ActionDisposable : IDisposable
    {
        private readonly Action _onDispose;
        private int _disposed;
        public ActionDisposable(Action onDispose) => _onDispose = onDispose;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            _onDispose();
        }
    }
}

/// <summary>
/// Immutable-style record variant providing same reactive capabilities; property setters still invoke raise helpers.
/// </summary>
public abstract record RxRecord : INotifyPropertyChanged, INotifyPropertyChanging
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
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        RaisePropertyChanging(propertyName);
        field = value!;
        RaisePropertyChanged(propertyName);
        return true;
    }

    public void RaisePropertyChanging(string propertyName)
    {
        if (_suppressCount > 0) return;
        if (_delayCount > 0) return;
        var args = new PropertyChangingEventArgs(propertyName);
        PropertyChanging?.Invoke(this, args);
        _changing.OnNext(args);
    }

    public void RaisePropertyChanged(string propertyName)
    {
        if (_suppressCount > 0) return;
        if (_delayCount > 0)
        {
            _delayedProperties ??= new HashSet<string>();
            _delayedProperties.Add(propertyName);
            return;
        }
        var args = new PropertyChangedEventArgs(propertyName);
        PropertyChanged?.Invoke(this, args);
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
            if (Interlocked.Decrement(ref _delayCount) == 0 && _delayedProperties is { Count: > 0 })
            {
                foreach (var prop in _delayedProperties)
                {
                    var args = new PropertyChangedEventArgs(prop);
                    PropertyChanged?.Invoke(this, args);
                    _changed.OnNext(args);
                }
                _delayedProperties.Clear();
            }
        });
    }

    public bool AreChangeNotificationsEnabled() => NotificationsEnabled && _delayCount == 0;
    private sealed class ActionDisposable : IDisposable
    {
        private readonly Action _onDispose;
        private int _disposed;
        public ActionDisposable(Action onDispose) => _onDispose = onDispose;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            _onDispose();
        }
    }
}