using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using R3;
using R3.DynamicData.List;
using Xunit;

namespace R3.DynamicData.Tests;

public class AutoRefreshWithFilterTests
{
    internal class TaskItem : INotifyPropertyChanged
    {
        private bool _isCompleted;

        public string Name { get; set; } = string.Empty;

        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (_isCompleted != value)
                {
                    _isCompleted = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    [Fact]
    public void AutoRefresh_WithFilter_UpdatesCollectionsWhenPropertyChanges()
    {
        // Arrange
        var source = new SourceList<TaskItem>();
        ReadOnlyObservableCollection<TaskItem> activeTasks = null!;
        ReadOnlyObservableCollection<TaskItem> completedTasks = null!;

        var activeSubscription = source.Connect()
            .AutoRefresh(task => task.IsCompleted)
            .Filter(task => !task.IsCompleted)
            .Bind(out activeTasks);

        var completedSubscription = source.Connect()
            .AutoRefresh(task => task.IsCompleted)
            .Filter(task => task.IsCompleted)
            .Bind(out completedTasks);

        var task1 = new TaskItem { Name = "Task 1", IsCompleted = false };
        var task2 = new TaskItem { Name = "Task 2", IsCompleted = false };
        var task3 = new TaskItem { Name = "Task 3", IsCompleted = false };

        // Act - Add tasks
        source.AddRange(new[] { task1, task2, task3 });

        // Assert - All should be active
        Assert.Equal(3, activeTasks.Count);
        Assert.Empty(completedTasks);

        // Act - Complete one task
        task1.IsCompleted = true;

        // Assert - Should move to completed
        Assert.Equal(2, activeTasks.Count);
        Assert.Single(completedTasks);
        Assert.Contains(task1, completedTasks);
        Assert.DoesNotContain(task1, activeTasks);

        // Act - Complete another task
        task2.IsCompleted = true;

        // Assert - Should move to completed
        Assert.Single(activeTasks);
        Assert.Equal(2, completedTasks.Count);
        Assert.Contains(task2, completedTasks);
        Assert.DoesNotContain(task2, activeTasks);

        // Act - Uncomplete a task
        task1.IsCompleted = false;

        // Assert - Should move back to active
        Assert.Equal(2, activeTasks.Count);
        Assert.Single(completedTasks);
        Assert.Contains(task1, activeTasks);
        Assert.DoesNotContain(task1, completedTasks);

        // Cleanup
        activeSubscription.Dispose();
        completedSubscription.Dispose();
        source.Dispose();
    }

    [Fact]
    public void AutoRefresh_WithFilter_CountsUpdateCorrectly()
    {
        // Arrange
        var source = new SourceList<TaskItem>();
        ReadOnlyObservableCollection<TaskItem> activeTasks = null!;
        ReadOnlyObservableCollection<TaskItem> completedTasks = null!;

        var activeSubscription = source.Connect()
            .AutoRefresh(task => task.IsCompleted)
            .Filter(task => !task.IsCompleted)
            .Bind(out activeTasks);

        var completedSubscription = source.Connect()
            .AutoRefresh(task => task.IsCompleted)
            .Filter(task => task.IsCompleted)
            .Bind(out completedTasks);

        int activeCount = 0;
        int completedCount = 0;

        // Subscribe to the filtered streams to track counts
        var activeCountSubscription = source.Connect()
            .AutoRefresh(task => task.IsCompleted)
            .Filter(task => !task.IsCompleted)
            .Subscribe(_ => activeCount = activeTasks.Count);

        var completedCountSubscription = source.Connect()
            .AutoRefresh(task => task.IsCompleted)
            .Filter(task => task.IsCompleted)
            .Subscribe(_ => completedCount = completedTasks.Count);

        var task1 = new TaskItem { Name = "Task 1", IsCompleted = false };
        var task2 = new TaskItem { Name = "Task 2", IsCompleted = false };
        var task3 = new TaskItem { Name = "Task 3", IsCompleted = false };

        // Act - Add tasks
        source.AddRange(new[] { task1, task2, task3 });

        // Assert - All should be active
        Assert.Equal(3, activeCount);
        Assert.Equal(0, completedCount);

        // Act - Complete one task
        task1.IsCompleted = true;

        // Assert - Counts should update
        Assert.Equal(2, activeCount);
        Assert.Equal(1, completedCount);

        // Act - Complete another task
        task2.IsCompleted = true;

        // Assert - Counts should update
        Assert.Equal(1, activeCount);
        Assert.Equal(2, completedCount);

        // Act - Uncomplete a task
        task1.IsCompleted = false;

        // Assert - Counts should update
        Assert.Equal(2, activeCount);
        Assert.Equal(1, completedCount);

        // Cleanup
        activeSubscription.Dispose();
        completedSubscription.Dispose();
        activeCountSubscription.Dispose();
        completedCountSubscription.Dispose();
        source.Dispose();
    }
}
