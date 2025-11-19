<div align="center">

# R3Ext

**Extensions and Utilities for the R3 Reactive Library**

[![NuGet](https://img.shields.io/nuget/v/R3Ext.svg)](https://www.nuget.org/packages/R3Ext/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/download)

_Bringing the power of ReactiveUI and ReactiveMarbles patterns to R3_

[Features](#features) •
[Installation](#installation) •
[Quick Start](#quick-start) •
[Documentation](#documentation) •
[Contributing](#contributing)

</div>

---

## Overview

R3Ext is a comprehensive extension library for [R3 (Reactive Extensions)](https://github.com/Cysharp/R3), providing ReactiveUI-inspired reactive commands, MVVM data binding with source generators, and a rich set of operators ported from ReactiveMarbles and System.Reactive. Built for .NET 9+ with full AOT compatibility and zero reflection.

### Why R3Ext?

-   **🚀 Performance**: Built on R3's high-performance foundation with zero-allocation patterns
-   **🔧 Source Generators**: Compile-time binding generation for type-safe, AOT-compatible MVVM
-   **📱 MAUI Ready**: First-class .NET MAUI support with automatic UI context marshaling
-   **🎯 ReactiveUI Compatible**: Familiar patterns for developers migrating from ReactiveUI
-   **⚡ Zero Reflection**: Full native AOT support with trimming-safe implementations

---

## Features

### 🎮 Reactive Commands

Unified `ICommand` + `IObservable<T>` implementation with ReactiveUI-compatible API:

```csharp
// Simple synchronous command
var saveCommand = RxCommand.Create(() => SaveData());

// Async with cancellation support
var loadCommand = RxCommand.CreateFromTask(async ct => await LoadDataAsync(ct));

// Parameterized commands
var deleteCommand = RxCommand<int, bool>.CreateFromTask(
    async (id, ct) => await DeleteItemAsync(id, ct),
    canExecute: isLoggedIn.AsObservable()
);

// Monitor execution state
deleteCommand.IsExecuting.Subscribe(busy => UpdateUI(busy));
deleteCommand.ThrownExceptions.Subscribe(ex => ShowError(ex));

// Combine multiple commands
var saveAll = RxCommand<Unit, Unit[]>.CreateCombined(save1, save2, save3);
```

### 🔗 MVVM Data Bindings

Source-generated, compile-time safe property bindings with automatic change tracking:

```csharp
// One-way binding with automatic rewiring on intermediate property changes
viewModel.WhenChanged(vm => vm.User.Profile.DisplayName)
    .Subscribe(name => label.Text = name);

// Two-way binding with converters
host.BindTwoWay(
    h => h.SelectedItem.Price,
    target => target.Text,
    hostToTarget: price => $"${price:F2}",
    targetToHost: text => decimal.Parse(text.TrimStart('$'))
);

// One-way binding with conversion
source.BindOneWay(
    s => s.Quantity,
    target => target.Text,
    qty => qty > 0 ? qty.ToString() : "Out of Stock"
);
```

**Key Features:**

-   Automatic property path tracking with full chain rewiring
-   Support for nested property changes (e.g., `vm => vm.User.Profile.Name`)
-   UnsafeAccessor for non-public member access (NET8.0+)
-   Generated at compile-time with zero runtime overhead

### 📦 Extension Operators

Comprehensive operator library organized by category:

#### Async Operations

```csharp
// Async coordination
await observable.FirstAsync(cancellationToken);
await observable.LastAsync(cancellationToken);
observable.Using(resource, selector);
```

#### Creation & Filtering

```csharp
// Advanced creation
Observable.FromArray(items, scheduler);
Observable.While(condition, source);

// Collection operations
source.Shuffle(random);
source.PartitionBySize(3);
source.BufferWithThreshold(threshold, maxSize);
```

#### Timing & Scheduling

```csharp
// Time-based operations with TimeProvider
source.Throttle(TimeSpan.FromMilliseconds(300), timeProvider);
source.Timeout(TimeSpan.FromSeconds(5), timeProvider);
Observable.Interval(TimeSpan.FromSeconds(1), timeProvider);
```

#### Error Handling

```csharp
// Safe observation patterns
source.ObserveSafe(
    onNext: x => Process(x),
    onError: ex => LogError(ex),
    onCompleted: () => Cleanup()
);

// Swallow and continue
source.SwallowCancellations();
```

#### Combining Streams

```csharp
// Multi-stream coordination
Observable.CombineLatestValuesAreAllTrue(stream1, stream2, stream3);
source1.WithLatestFrom(source2, source3, (a, b, c) => new { a, b, c });
```

### 🎭 Interaction Workflows

ReactiveUI-style Interaction pattern for view/viewmodel communication:

```csharp
public class ViewModel
{
    public Interaction<string, bool> ConfirmDelete { get; } = new();

    async Task DeleteAsync()
    {
        bool confirmed = await ConfirmDelete.Handle("Delete this item?");
        if (confirmed) await DeleteItemAsync();
    }
}

// In the view
viewModel.ConfirmDelete.RegisterHandler(async interaction =>
{
    bool result = await DisplayAlert("Confirm", interaction.Input, "Yes", "No");
    interaction.SetOutput(result);
});
```

### 🎨 Signal Utilities

Reactive signal helpers for boolean state management:

```csharp
// Convert any observable to a signal-style boolean
var hasItems = itemsObservable.AsSignal(seed: false, predicate: items => items.Count > 0);

// Boolean stream utilities
var allTrue = Observable.CombineLatestValuesAreAllTrue(isValid, isConnected, isReady);
```

---

## Installation

### NuGet Packages

```bash
# Core library with extensions and commands
dotnet add package R3Ext

# Source generator for MVVM bindings
dotnet add package R3Ext.Bindings.SourceGenerator

# .NET MAUI integration (optional)
dotnet add package R3Ext.Bindings.MauiTargets
```

### Requirements

-   **.NET 9.0** or later
-   **C# 12** or later
-   **R3** 1.3.0+

---

## Quick Start

### Basic Setup

```csharp
using R3;
using R3Ext;

// Create a reactive property
var name = new ReactiveProperty<string>("John");

// Observe changes
name.Subscribe(x => Console.WriteLine($"Name changed to: {x}"));

// Use extension operators
name
    .Throttle(TimeSpan.FromMilliseconds(300))
    .DistinctUntilChanged()
    .Subscribe(x => SaveToDatabase(x));
```

### MAUI Integration

```csharp
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseR3(); // Enables R3 with MAUI-aware scheduling

        return builder.Build();
    }
}
```

### ViewModel with Commands

```csharp
public class MainViewModel : RxObject
{
    public ReactiveProperty<string> SearchText { get; } = new("");
    public ReactiveProperty<bool> IsLoading { get; } = new(false);
    public ReadOnlyReactiveProperty<bool> CanSearch { get; }

    public RxCommand<Unit, Unit> SearchCommand { get; }

    public MainViewModel()
    {
        // Derive CanSearch from SearchText
        CanSearch = SearchText
            .Select(text => !string.IsNullOrWhiteSpace(text))
            .ToReadOnlyReactiveProperty();

        // Create command with CanExecute binding
        SearchCommand = RxCommand.CreateFromTask(
            async ct => await PerformSearchAsync(ct),
            canExecute: CanSearch.AsObservable()
        );

        // Track execution state
        SearchCommand.IsExecuting.Subscribe(loading => IsLoading.Value = loading);

        // Handle errors
        SearchCommand.ThrownExceptions.Subscribe(ex => ShowError(ex));
    }

    private async Task PerformSearchAsync(CancellationToken ct)
    {
        var results = await SearchApiAsync(SearchText.Value, ct);
        // Update UI...
    }
}
```

---

## Documentation

### Extension Categories

| Category           | Description               | Key Operators                                     |
| ------------------ | ------------------------- | ------------------------------------------------- |
| **Async**          | Async coordination        | `FirstAsync`, `LastAsync`, `Using`                |
| **Creation**       | Observable factories      | `FromArray`, `While`                              |
| **Filtering**      | Stream filtering          | `Shuffle`, `PartitionBySize`                      |
| **Collection**     | Buffering & batching      | `BufferWithThreshold`, `PartitionByPredicate`     |
| **Timing**         | Time-based operations     | `Throttle`, `Timeout`, `Interval`                 |
| **Error Handling** | Safe observation          | `ObserveSafe`, `SwallowCancellations`             |
| **Combining**      | Multi-stream coordination | `WithLatestFrom`, `CombineLatestValuesAreAllTrue` |
| **Commands**       | Reactive commands         | `RxCommand`, `InvokeCommand`                      |
| **Signals**        | Boolean state utilities   | `AsSignal`, `AsBool`                              |

### Source Generator Usage

The binding generator automatically discovers `WhenChanged`, `BindOneWay`, and `BindTwoWay` calls:

```csharp
// Automatically generates compile-time safe binding code
public void SetupBindings()
{
    // Generator detects this pattern and creates efficient binding
    this.WhenChanged(vm => vm.User.Profile.Email)
        .Subscribe(email => emailLabel.Text = email);

    // Two-way bindings also auto-generated
    this.BindTwoWay(
        vm => vm.Settings.Volume,
        view => view.VolumeSlider.Value
    );
}
```

**Generator Features:**

-   Compile-time validation of property paths
-   Zero runtime reflection or IL generation
-   Automatic rewiring on intermediate property replacements
-   Support for nullable chains with null propagation
-   Works with internal/private members via UnsafeAccessor

---

## Project Structure

```
R3Ext/
├── R3Ext/                              # Core library
│   ├── AsyncExtensions.cs              # Async operators
│   ├── CreationExtensions.cs           # Observable factories
│   ├── FilteringExtensions.cs          # Filtering operators
│   ├── TimingExtensions.cs             # Time-based operators
│   ├── ErrorHandlingExtensions.cs      # Error handling
│   ├── CombineExtensions.cs            # Combining operators
│   ├── RxCommand.cs                    # Reactive commands
│   ├── RxObject.cs                     # MVVM base class
│   ├── Interactions/                   # Interaction pattern
│   └── BindingRegistry.cs              # Runtime binding support
│
├── R3Ext.Bindings.SourceGenerator/     # Compile-time binding generator
│   ├── BindingGeneratorV2.cs           # Main generator logic
│   └── UiBindingMetadata.cs            # MAUI/UI metadata
│
├── R3Ext.Bindings.MauiTargets/         # MAUI integration
│   └── GenerateUiBindingTargetsTask.cs # MSBuild task
│
├── R3Ext.Tests/                        # Comprehensive test suite
└── R3Ext.SampleApp/                    # .NET MAUI sample app
```

---

## Building from Source

### Prerequisites

-   .NET 9 SDK ([Download](https://dotnet.microsoft.com/download/dotnet/9.0))
-   .NET MAUI workload: `dotnet workload install maui`

### Build

```bash
git clone https://github.com/michaelstonis/R3Ext.git
cd R3Ext
dotnet restore
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Run Sample App

```bash
# Android
dotnet build R3Ext.SampleApp -t:Run -f net9.0-android

# iOS Simulator
dotnet build R3Ext.SampleApp -t:Run -f net9.0-ios

# Mac Catalyst
dotnet build R3Ext.SampleApp -t:Run -f net9.0-maccatalyst
```

---

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

### Development Guidelines

1. Follow existing code style and conventions
2. Add tests for new features
3. Update documentation for API changes
4. Ensure all tests pass before submitting PR

---

## Attribution

This project is a port and adaptation of patterns and components from several excellent reactive programming libraries:

-   **[ReactiveUI](https://github.com/reactiveui/ReactiveUI)** - MVVM framework for reactive programming
    -   Inspiration for `RxCommand`, `Interaction` patterns, and MVVM bindings
    -   Original concepts for view-model interaction workflows
-   **[ReactiveMarbles](https://github.com/reactivemarbles)** - Community-driven reactive extensions
    -   Operator implementations and extension patterns
    -   Advanced observable composition techniques
-   **[R3](https://github.com/Cysharp/R3)** - High-performance reactive extensions
    -   Foundation library providing core reactive primitives
    -   Performance-optimized observable implementation

R3Ext brings these proven patterns to the R3 ecosystem while maintaining compatibility with modern .NET features like AOT compilation and source generators.

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## Acknowledgments

Special thanks to:

-   The [ReactiveUI team](https://github.com/reactiveui) for pioneering reactive MVVM patterns in .NET
-   The [ReactiveMarbles community](https://github.com/reactivemarbles) for their extensive operator libraries
-   [Yoshifumi Kawai (neuecc)](https://github.com/neuecc) and the [Cysharp team](https://github.com/Cysharp) for creating R3
-   The [.NET Community](https://dotnet.microsoft.com/platform/community) for continued support and contributions

---

<div align="center">

**[⬆ Back to Top](#r3ext)**

Made with ❤️ for the Reactive Programming Community

</div>
