# ReactiveCommand Compatibility Layer - Usage Examples

This document provides usage examples for the ReactiveUI-compatible ReactiveCommand implementation for R3.

## Overview

`ReactiveUICompatibleCommand<TInput, TOutput>` is a drop-in replacement for ReactiveUI's `ReactiveCommand` that works with R3's reactive extensions. It provides full API compatibility with ReactiveUI while leveraging R3's performance optimizations.

## Basic Usage

### Creating a Simple Command

```csharp
using R3Ext;

// Parameterless command (Unit-based)
var command = ReactiveUICompatibleCommand.Create(() =>
{
    Console.WriteLine("Command executed!");
});

// Execute the command
await command.Execute().FirstAsync();
```

### Creating a Generic Command

```csharp
// Command with input and output types
var doubleCommand = ReactiveUICompatibleCommand<int, int>.Create(
    x => x * 2
);

// Execute with parameter
var result = await doubleCommand.Execute(21).FirstAsync();
// result = 42
```

## Async Commands

### Using CreateFromTask

```csharp
// Async command without cancellation token
var loadDataCommand = ReactiveUICompatibleCommand.CreateFromTask(async () =>
{
    var data = await LoadDataFromApiAsync();
    ProcessData(data);
});

// Async command with cancellation token support
var downloadCommand = ReactiveUICompatibleCommand<string, byte[]>.CreateFromTask(
    async (url, ct) =>
    {
        using var client = new HttpClient();
        return await client.GetByteArrayAsync(url, ct);
    }
);
```

## CanExecute Support

### Dynamic Executability

```csharp
var isEnabled = new ReactiveProperty<bool>(true);

var command = ReactiveUICompatibleCommand.Create(
    () => Console.WriteLine("Executed!"),
    canExecute: isEnabled
);

// Command can only execute when isEnabled is true
isEnabled.Value = false;
// Command.Execute() will check canExecute before running

// ICommand integration
var iCommand = (ICommand)command;
iCommand.CanExecuteChanged += (s, e) => Console.WriteLine("CanExecute changed!");
```

### Complex CanExecute Logic

```csharp
var userName = new ReactiveProperty<string>("");
var password = new ReactiveProperty<string>("");

var canLogin = userName
    .CombineLatest(password, (u, p) => !string.IsNullOrEmpty(u) && p.Length >= 8)
    .DistinctUntilChanged();

var loginCommand = ReactiveUICompatibleCommand.CreateFromTask(
    async () => await AuthService.LoginAsync(userName.Value, password.Value),
    canExecute: canLogin
);
```

## Observing Command State

### IsExecuting

```csharp
var command = ReactiveUICompatibleCommand.CreateFromTask(async () =>
{
    await Task.Delay(2000); // Simulate long operation
});

// Subscribe to execution state
command.IsExecuting.Subscribe(isExecuting =>
{
    if (isExecuting)
        ShowLoadingSpinner();
    else
        HideLoadingSpinner();
});

await command.Execute().FirstAsync();
```

### ThrownExceptions

```csharp
var command = ReactiveUICompatibleCommand.CreateFromTask(async () =>
{
    if (Random.Shared.Next(2) == 0)
        throw new InvalidOperationException("Random error!");
    
    await Task.Delay(100);
});

// Handle all exceptions from the command
command.ThrownExceptions.Subscribe(ex =>
{
    ShowErrorMessage($"Command failed: {ex.Message}");
    LogError(ex);
});

// Subscribe to execution results
command.AsObservable().Subscribe(
    _ => Console.WriteLine("Command succeeded!")
);
```

## InvokeCommand Extension

### Triggering Commands from Observables

```csharp
// Execute command whenever button is clicked
var buttonClicks = Observable.FromEvent<EventHandler, EventArgs>(
    h => (s, e) => h(e),
    h => button.Click += h,
    h => button.Click -= h
);

var command = ReactiveUICompatibleCommand<EventArgs, Unit>.Create(_ =>
{
    Console.WriteLine("Button clicked!");
    return Unit.Default;
});

using var subscription = buttonClicks
    .AsSystemObservable()
    .InvokeCommand(command);
```

### Data-Driven Command Execution

```csharp
var searchTerms = new Subject<string>();

var searchCommand = ReactiveUICompatibleCommand<string, SearchResult[]>.CreateFromTask(
    async (term, ct) => await SearchService.SearchAsync(term, ct)
);

// Execute search whenever searchTerms emits
using var subscription = searchTerms
    .AsSystemObservable()
    .Throttle(TimeSpan.FromMilliseconds(300))
    .DistinctUntilChanged()
    .InvokeCommand(searchCommand);

// Observe results
searchCommand.AsObservable().Subscribe(results =>
{
    DisplaySearchResults(results);
});
```

## Combined Commands

### Executing Multiple Commands in Parallel

```csharp
var command1 = ReactiveUICompatibleCommand<int, int>.Create(x => x * 2);
var command2 = ReactiveUICompatibleCommand<int, int>.Create(x => x * 3);
var command3 = ReactiveUICompatibleCommand<int, int>.Create(x => x * 4);

var combinedCommand = ReactiveUICompatibleCommand<int, int>.CreateCombined(
    command1, command2, command3
);

// Execute all commands concurrently
var results = await combinedCommand.Execute(5).FirstAsync();
// results = [10, 15, 20]

// Combined command can only execute when ALL child commands can execute
var canExecute1 = new ReactiveProperty<bool>(true);
var canExecute2 = new ReactiveProperty<bool>(false);

var cmd1 = ReactiveUICompatibleCommand<int, int>.Create(x => x, canExecute1);
var cmd2 = ReactiveUICompatibleCommand<int, int>.Create(x => x, canExecute2);

var combined = ReactiveUICompatibleCommand<int, int>.CreateCombined(cmd1, cmd2);

// combined.CanExecute emits false because cmd2 cannot execute
combined.CanExecute.Subscribe(can => Console.WriteLine($"Can execute: {can}"));
// Output: "Can execute: False"
```

## Background Execution

### Running on ThreadPool

```csharp
var heavyComputeCommand = ReactiveUICompatibleCommand<int, int>.CreateRunInBackground(
    x =>
    {
        // This runs on a background thread
        Thread.Sleep(1000);
        return x * x;
    }
);

// Results are marshaled back to the calling context
var result = await heavyComputeCommand.Execute(42).FirstAsync();
// result = 1764
```

## Observable-Based Commands

### Using CreateFromObservable

```csharp
// Using R3 Observable
var command = ReactiveUICompatibleCommand<string, string>.CreateFromR3Observable(
    input => Observable.Return(input.ToUpper())
);

// Using System.IObservable
var sysCommand = ReactiveUICompatibleCommand<int, int>.CreateFromObservable(
    x => System.Reactive.Linq.Observable.Return(x * 2)
);
```

## XAML/MAUI Integration

### View Model Example

```csharp
public class MyViewModel : IDisposable
{
    private readonly DisposableBag _disposables = new();
    
    public ReactiveProperty<string> SearchText { get; } = new("");
    public ReactiveProperty<bool> IsSearching { get; } = new(false);
    public ReactiveUICompatibleCommand<Unit, Unit> SearchCommand { get; }
    public ObservableCollection<SearchResult> Results { get; } = new();

    public MyViewModel(ISearchService searchService)
    {
        var canSearch = SearchText
            .Select(text => !string.IsNullOrWhiteSpace(text))
            .CombineLatest(IsSearching.Select(x => !x), (hasText, notSearching) => hasText && notSearching);

        SearchCommand = ReactiveUICompatibleCommand.CreateFromTask(
            async () =>
            {
                IsSearching.Value = true;
                try
                {
                    var results = await searchService.SearchAsync(SearchText.Value);
                    Results.Clear();
                    foreach (var result in results)
                        Results.Add(result);
                }
                finally
                {
                    IsSearching.Value = false;
                }
            },
            canExecute: canSearch
        );

        SearchCommand.ThrownExceptions
            .Subscribe(ex => ShowError(ex.Message))
            .AddTo(ref _disposables);

        SearchCommand.AddTo(ref _disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        SearchText.Dispose();
        IsSearching.Dispose();
        SearchCommand.Dispose();
    }
}
```

### XAML Binding (WPF/Avalonia)

```xml
<Window>
    <Window.DataContext>
        <local:MyViewModel />
    </Window.DataContext>
    
    <StackPanel>
        <TextBox Text="{Binding SearchText.Value, UpdateSourceTrigger=PropertyChanged}" />
        <Button Content="Search" Command="{Binding SearchCommand}" />
        <ProgressBar IsIndeterminate="True" 
                     Visibility="{Binding IsSearching.Value, Converter={StaticResource BoolToVisibility}}" />
        <ListBox ItemsSource="{Binding Results}" />
    </StackPanel>
</Window>
```

## Disposal

Always dispose commands when done:

```csharp
var command = ReactiveUICompatibleCommand.Create(() => { });

// Use in a using block
using (command)
{
    await command.Execute().FirstAsync();
}

// Or manually
command.Dispose();

// Or with DisposableBag
var disposables = new DisposableBag();
command.AddTo(ref disposables);
// Later...
disposables.Dispose();
```

## Migration from ReactiveUI

For existing ReactiveUI code, simply replace:

```csharp
// Old ReactiveUI code
using ReactiveUI;
var command = ReactiveCommand.Create(() => { });

// New R3Ext code
using R3Ext;
var command = ReactiveUICompatibleCommand.Create(() => { });
```

The API is intentionally identical for drop-in compatibility!
