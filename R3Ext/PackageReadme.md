# R3Ext

Extensions and utilities for the [R3 reactive library](https://github.com/Cysharp/R3) including ReactiveUI-compatible
components.

## Features

### RxCommand

Reactive command implementation with:

- Synchronous and asynchronous execution
- Observable result streams
- CanExecute gating
- IsExecuting tracking
- Exception capture
- Command composition

### RxObject & RxRecord

Reactive base classes for ViewModels:

- PropertyChanging/PropertyChanged events
- Reactive Changing/Changed observable streams
- RaiseAndSetIfChanged helper
- Notification suppression and delay
- Works with class (RxObject) and record (RxRecord) types

### Extension Methods

- Command mixins (InvokeCommand with projection)
- Async coordination helpers
- Creation operators
- Timing operators using TimeProvider
- Error handling patterns
- Collection operators

### Source Generators

- Data binding code generation
- Property change notification helpers

## Quick Start

```csharp
// Reactive ViewModel
public class MyViewModel : RxObject
{
    private string _name = "";
    public string Name
    {
        get => _name;
        set => RaiseAndSetIfChanged(ref _name, value);
    }

    public RxCommand<Unit, Unit> SaveCommand { get; }

    public MyViewModel()
    {
        SaveCommand = RxCommand.CreateFromTask(async ct =>
        {
            await SaveAsync(ct);
            return Unit.Default;
        });
    }
}

// Use reactive streams
vm.Changed
    .Where(e => e.PropertyName == nameof(MyViewModel.Name))
    .Subscribe(e => Console.WriteLine($"Name changed to: {vm.Name}"));
```

## Documentation

See the [GitHub repository](https://github.com/michaelstonis/R3Ext) for full documentation and examples.

## License

MIT
