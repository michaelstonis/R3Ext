using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using R3;
using R3.DynamicData.List;

namespace R3Ext.SampleApp;

public class TaskItem : INotifyPropertyChanged
{
    private bool _isCompleted;
    private int _updateCount;

    public string Name { get; set; } = string.Empty;

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (_isCompleted != value)
            {
                _isCompleted = value;
                _updateCount++;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UpdateCount));
            }
        }
    }

    public int UpdateCount => _updateCount;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

#pragma warning disable CA1001
public partial class DDRefreshPage : ContentPage
#pragma warning restore CA1001
{
    private readonly SourceList<TaskItem> _source = new();
    private readonly ReadOnlyObservableCollection<TaskItem> _activeTasks = null!;
    private readonly ReadOnlyObservableCollection<TaskItem> _completedTasks = null!;
    private readonly IDisposable _activeSubscription;
    private readonly IDisposable _completedSubscription;
    private readonly IDisposable _activeCountSubscription;
    private readonly IDisposable _completedCountSubscription;

    public DDRefreshPage()
    {
        InitializeComponent();

        // AutoRefresh: watches for property changes and automatically refreshes the changeset
        // This allows Filter to re-evaluate when IsCompleted changes

        // Active tasks - bind and update count from the filtered stream
        var activeStream = _source.Connect()
            .AutoRefresh(task => task.IsCompleted)
            .Filter(task => !task.IsCompleted);

        _activeSubscription = activeStream.Bind(out _activeTasks);
        ActiveTasksView.ItemsSource = _activeTasks;

        _activeCountSubscription = activeStream
            .Subscribe(_ => ActiveCountLabel.Text = $"Active: {_activeTasks.Count}");

        // Completed tasks - bind and update count from the filtered stream
        var completedStream = _source.Connect()
            .AutoRefresh(task => task.IsCompleted)
            .Filter(task => task.IsCompleted);

        _completedSubscription = completedStream.Bind(out _completedTasks);
        CompletedTasksView.ItemsSource = _completedTasks;

        _completedCountSubscription = completedStream
            .Subscribe(_ => CompletedCountLabel.Text = $"Completed: {_completedTasks.Count}");
    }

    private void OnAddTask(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TaskEntry.Text))
        {
            _source.Add(new TaskItem { Name = TaskEntry.Text });
            TaskEntry.Text = string.Empty;
        }
    }

    private void OnAddSampleTasks(object sender, EventArgs e)
    {
        var tasks = new[]
        {
            new TaskItem { Name = "Review pull request" },
            new TaskItem { Name = "Write documentation" },
            new TaskItem { Name = "Fix bug #123" },
        };
        _source.AddRange(tasks);
    }

    private void OnTaskCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        // Property change is automatically detected by AutoRefresh
        // No manual refresh needed!
    }

    private void OnCompleteAll(object sender, EventArgs e)
    {
        foreach (var task in _source.Items)
        {
            task.IsCompleted = true;
        }
    }

    private void OnClearAll(object sender, EventArgs e)
    {
        _source.Clear();
    }
}
