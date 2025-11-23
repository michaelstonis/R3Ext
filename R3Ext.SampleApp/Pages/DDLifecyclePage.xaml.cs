using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Microsoft.Maui.Controls;
using R3;
using R3.DynamicData.List;

namespace R3Ext.SampleApp;

public sealed class DisposableItem : IDisposable
{
    private readonly Action<string> _onDispose;
    private bool _disposed;

    public DisposableItem(string name, Action<string> onDispose)
    {
        Name = name;
        _onDispose = onDispose;
    }

    public string Name { get; }

    public void Dispose()
    {
        if (!_disposed)
        {
            _onDispose(Name);
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

#pragma warning disable CA1001
public partial class DDLifecyclePage : ContentPage
#pragma warning restore CA1001
{
    private readonly SourceList<string> _limitedSource = new();
    private readonly SourceList<DisposableItem> _disposableSource = new();
    private readonly ReadOnlyObservableCollection<string> _limitedItems = null!;
    private readonly IDisposable _limitSubscription;
    private readonly IDisposable _limitCountSubscription;
    private readonly IDisposable _disposeSubscription;
    private readonly StringBuilder _disposableLog = new();
    private int _disposedCount;

    public DDLifecyclePage()
    {
        InitializeComponent();

        // LimitSizeTo: keeps only the most recent 5 items
        _limitSubscription = _limitedSource.Connect()
            .LimitSizeTo(5)
            .Bind(out _limitedItems);
        LimitView.ItemsSource = _limitedItems;

        _limitCountSubscription = _limitedSource.CountChanged
            .Subscribe(count => LimitCountLabel.Text = $"Count: {count} (Max: 5)");

        // DisposeMany: automatically disposes items when removed
        _disposeSubscription = _disposableSource.Connect()
            .DisposeMany()
            .Subscribe(_ => UpdateDisposableUI());

        UpdateDisposableUI();
    }

    private void OnAddLimited(object sender, EventArgs e)
    {
        var text = string.IsNullOrWhiteSpace(LimitEntry.Text)
            ? $"Item {_limitedSource.Count + 1}"
            : LimitEntry.Text;

        _limitedSource.Add(text);
        LimitEntry.Text = string.Empty;
    }

    private void OnAddMultipleLimited(object sender, EventArgs e)
    {
        var items = Enumerable.Range(1, 10)
            .Select(i => $"Batch Item {i}")
            .ToArray();
        _limitedSource.AddRange(items);
    }

    private void OnAddDisposable(object sender, EventArgs e)
    {
        var item = new DisposableItem($"Disposable {_disposableSource.Count + 1}", OnItemDisposed);
        _disposableSource.Add(item);
        LogDisposableAction($"Created: {item.Name}");
    }

    private void OnClearDisposables(object sender, EventArgs e)
    {
        _disposableSource.Clear();
    }

    private void OnItemDisposed(string name)
    {
        _disposedCount++;
        LogDisposableAction($"Disposed: {name}");
    }

    private void LogDisposableAction(string message)
    {
        _disposableLog.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        DisposableLog.Text = _disposableLog.ToString();
    }

    private void UpdateDisposableUI()
    {
        DisposableCountLabel.Text = $"Active: {_disposableSource.Count}, Disposed: {_disposedCount}";
    }
}
