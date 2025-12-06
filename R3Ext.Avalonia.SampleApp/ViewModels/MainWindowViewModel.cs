// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace R3Ext.Avalonia.SampleApp.ViewModels;

/// <summary>
/// Main window ViewModel that handles navigation between demo pages.
/// Implements INotifyPropertyChanged for Avalonia binding support.
/// </summary>
public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    // Lazy-created views to demonstrate activation lifecycle
    private readonly Lazy<Views.ActivationDemoView> _activationDemoView = new(() => new Views.ActivationDemoView());
    private readonly Lazy<Views.TimerDemoView> _timerDemoView = new(() => new Views.TimerDemoView());

    private object? _currentView;
    private bool _isActivationDemoSelected = true;
    private bool _isTimerDemoSelected;

    public MainWindowViewModel()
    {
        // Create navigation commands
        NavigateToActivationDemoCommand = new RelayCommand(() =>
        {
            CurrentView = _activationDemoView.Value;
            IsActivationDemoSelected = true;
            IsTimerDemoSelected = false;
        });

        NavigateToTimerDemoCommand = new RelayCommand(() =>
        {
            CurrentView = _timerDemoView.Value;
            IsActivationDemoSelected = false;
            IsTimerDemoSelected = true;
        });

        // Start with Activation Demo
        CurrentView = _activationDemoView.Value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public object? CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public bool IsActivationDemoSelected
    {
        get => _isActivationDemoSelected;
        set => SetProperty(ref _isActivationDemoSelected, value);
    }

    public bool IsTimerDemoSelected
    {
        get => _isTimerDemoSelected;
        set => SetProperty(ref _isTimerDemoSelected, value);
    }

    public ICommand NavigateToActivationDemoCommand { get; }

    public ICommand NavigateToTimerDemoCommand { get; }

    public void Dispose()
    {
        // Nothing to dispose for now
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// Simple relay command implementation for navigation.
/// </summary>
internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke() ?? true;
    }

    public void Execute(object? parameter)
    {
        _execute();
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
