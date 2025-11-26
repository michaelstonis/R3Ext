<div align="center">

<img src="logo.svg" alt="R3Ext Logo" width="200" />

# R3Ext

**Production-Ready Reactive Extensions for Modern .NET**

[![NuGet](https://img.shields.io/nuget/v/R3Ext.svg)](https://www.nuget.org/packages/R3Ext/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/download)

_Enterprise-grade reactive programming with ReactiveUI patterns, DynamicData collections, and compile-time safe bindings_

[Features](#features) •
[Installation](#installation) •
[Quick Start](#quick-start) •
[Documentation](#documentation) •
[Examples](#examples) •
[Contributing](#contributing)

</div>

---

## Overview

R3Ext is a production-ready reactive programming library built on [R3](https://github.com/Cysharp/R3), combining the best of ReactiveUI, DynamicData, and System.Reactive into a modern, high-performance package. With source-generated bindings, reactive collections, and comprehensive operators, R3Ext makes building reactive applications fast, type-safe, and maintainable.

### Why Choose R3Ext?

-   **🚀 High Performance**: Built on R3's zero-allocation foundation with optimized hot paths
-   **🔧 Source Generators**: Compile-time binding generation eliminates runtime reflection
-   **📊 Reactive Collections**: Full DynamicData port for observable collections with caching, filtering, and transformations
-   **📱 MAUI First-Class**: Seamless .NET MAUI integration with automatic UI thread marshaling
-   **🎯 Battle-Tested Patterns**: ReactiveUI-compatible APIs proven in production applications
-   **⚡ Native AOT Ready**: Full trimming and AOT compatibility for minimal deployment sizes
-   **🔍 Type Safety**: Compile-time verification of property paths and binding expressions

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

Source-generated, compile-time safe property bindings with intelligent change tracking:

```csharp
// WhenChanged - Monitor any property chain with automatic rewiring
viewModel.WhenChanged(vm => vm.User.Profile.DisplayName)
    .Subscribe(name => label.Text = name);

// WhenObserved - Observe nested observables with automatic switching
viewModel.WhenObserved(vm => vm.CurrentStream.DataObservable)
    .Subscribe(value => UpdateChart(value));

// Two-way binding with type-safe converters
host.BindTwoWay(
    h => h.SelectedItem.Price,
    target => target.Text,
    hostToTarget: price => $"${price:F2}",
    targetToHost: text => decimal.Parse(text.TrimStart('$'))
);

// One-way binding with inline transformation
source.BindOneWay(
    s => s.Quantity,
    target => target.Text,
    qty => qty > 0 ? qty.ToString() : "Out of Stock"
);
```

**Key Features:**

-   **WhenChanged**: Tracks property chains with INPC detection and fallback to polling
-   **WhenObserved**: Automatically switches subscriptions when parent observables change
-   **Intelligent Monitoring**: Uses `INotifyPropertyChanged` when available, falls back to `EveryValueChanged`
-   **Automatic Rewiring**: Handles intermediate property replacements transparently
-   **UnsafeAccessor**: Access internal/private members without reflection (NET8.0+)
-   **Zero Runtime Cost**: All binding code generated at compile-time

### 📊 Reactive Collections (R3.DynamicData)

High-performance observable collections with rich transformation operators, ported from DynamicData:

```csharp
// Create observable cache with key-based access
var cache = new SourceCache<Person, int>(p => p.Id);

// Observe changes with automatic caching
cache.Connect()
    .Filter(p => p.IsActive)
    .Sort(SortExpressionComparer<Person>.Ascending(p => p.Name))
    .Transform(p => new PersonViewModel(p))
    .Bind(out var items)  // Bind to ObservableCollection
    .Subscribe();

// Observable list for ordered collections
var list = new SourceList<string>();
list.Connect()
    .AutoRefresh(s => s.Length)  // Re-evaluate on property changes
    .Filter(s => s.StartsWith("A"))
    .Subscribe(changeSet => HandleChanges(changeSet));

// Advanced operators
cache.Connect()
    .TransformMany(p => p.Orders)       // Flatten child collections
    .Group(o => o.Status)                // Group by property
    .DistinctValues(o => o.CustomerId)   // Track unique values
    .Subscribe();
```

**Operators:**

| Category | Operators |
|----------|-----------|
| **Filtering** | `Filter`, `FilterOnObservable`, `AutoRefresh` |
| **Transformation** | `Transform`, `TransformMany`, `TransformAsync` |
| **Sorting** | `Sort`, `SortAsync` |
| **Grouping** | `Group`, `GroupWithImmutableState`, `GroupOn` |
| **Aggregation** | `Count`, `Sum`, `Avg`, `Min`, `Max` |
| **Change Tracking** | `DistinctValues`, `MergeChangeSet`, `Clone` |
| **Binding** | `Bind`, `ObserveOn`, `SubscribeMany` |

**Performance Features:**

-   Optimized change sets minimize allocations
-   Incremental updates reduce processing overhead
-   Virtual change sets support for large collections
-   Efficient key-based lookups in caches

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

# Source generator for MVVM bindings (required for bindings)
dotnet add package R3Ext.Bindings.SourceGenerator

# Reactive collections (DynamicData port)
dotnet add package R3.DynamicData

# .NET MAUI integration (optional, for MAUI apps)
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
│   ├── Extensions/                     # Extension operators
│   │   ├── AsyncExtensions.cs          # Async coordination
│   │   ├── CreationExtensions.cs       # Observable factories
│   │   ├── FilteringExtensions.cs      # Filtering operators
│   │   ├── TimingExtensions.cs         # Time-based operators
│   │   ├── ErrorHandlingExtensions.cs  # Error handling
│   │   └── CombineExtensions.cs        # Combining operators
│   ├── Commands/                       # Reactive commands
│   │   └── RxCommand.cs                # ICommand + IObservable
│   ├── Bindings/                       # MVVM bindings
│   │   ├── GeneratedBindingStubs.cs    # Binding API surface
│   │   └── BindingRegistry.cs          # Runtime support
│   ├── Interactions/                   # Interaction pattern
│   │   └── Interaction.cs              # View-ViewModel communication
│   └── RxObject.cs                     # MVVM base class
│
├── R3.DynamicData/                     # Reactive collections (NEW!)
│   ├── List/                           # Observable list operators
│   ├── Cache/                          # Observable cache operators
│   ├── Operators/                      # Transformation operators
│   └── Binding/                        # Collection binding
│
├── R3Ext.Bindings.SourceGenerator/     # Compile-time binding generator
│   ├── BindingGenerator.cs             # WhenChanged/WhenObserved generation
│   └── UiBindingMetadata.cs            # MAUI UI element metadata
│
├── R3Ext.Bindings.MauiTargets/         # MAUI integration
│   └── GenerateUiBindingTargetsTask.cs # MSBuild task for UI bindings
│
├── R3Ext.Tests/                        # Core library tests
├── R3.DynamicData.Tests/               # DynamicData tests (NEW!)
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

## Examples

### Complete MVVM Example

```csharp
public class ShoppingCartViewModel : RxObject
{
    private readonly SourceCache<Product, int> _productsCache;
    private readonly ReadOnlyObservableCollection<ProductViewModel> _items;

    public ReadOnlyObservableCollection<ProductViewModel> Items => _items;
    public ReactiveProperty<string> SearchText { get; } = new("");
    public ReadOnlyReactiveProperty<decimal> TotalPrice { get; }
    public RxCommand<Unit, Unit> CheckoutCommand { get; }

    public ShoppingCartViewModel(IProductService productService)
    {
        _productsCache = new SourceCache<Product, int>(p => p.Id);

        // Observable collection with filtering and transformation
        _productsCache.Connect()
            .Filter(this.WhenChanged(x => x.SearchText.Value)
                .Select(search => new Func<Product, bool>(p => 
                    string.IsNullOrEmpty(search) || 
                    p.Name.Contains(search, StringComparison.OrdinalIgnoreCase))))
            .Transform(p => new ProductViewModel(p))
            .Bind(out _items)
            .Subscribe();

        // Derived total price
        TotalPrice = _productsCache.Connect()
            .AutoRefresh(p => p.Quantity)
            .Select(_ => _productsCache.Items.Sum(p => p.Price * p.Quantity))
            .ToReadOnlyReactiveProperty();

        // Command with async execution
        CheckoutCommand = RxCommand.CreateFromTask(
            async ct => await productService.CheckoutAsync(_productsCache.Items, ct),
            canExecute: TotalPrice.Select(total => total > 0)
        );

        CheckoutCommand.ThrownExceptions
            .Subscribe(ex => ShowError($"Checkout failed: {ex.Message}"));
    }
}
```

### WhenObserved for Observable Chains

```csharp
public class StreamMonitorViewModel : RxObject
{
    public ReactiveProperty<DataStream> CurrentStream { get; } = new();
    public ReactiveProperty<string> StatusText { get; } = new("");

    public StreamMonitorViewModel()
    {
        // Automatically switches to new stream's observable when CurrentStream changes
        this.WhenObserved(vm => vm.CurrentStream.Value.DataObservable)
            .Subscribe(data => StatusText.Value = $"Received: {data}");

        // Works with nested observable properties
        this.WhenObserved(vm => vm.CurrentDocument.Value.AutoSaveProgress)
            .Subscribe(progress => UpdateProgressBar(progress));
    }

    public void SwitchToStream(DataStream newStream)
    {
        // WhenObserved automatically unsubscribes from old stream
        // and subscribes to new stream's DataObservable
        CurrentStream.Value = newStream;
    }
}
```

### Interaction Pattern

```csharp
public class DocumentViewModel : RxObject
{
    public Interaction<string, bool> ConfirmSave { get; } = new();
    public RxCommand<Unit, Unit> SaveCommand { get; }

    public DocumentViewModel()
    {
        SaveCommand = RxCommand.CreateFromTask(async ct =>
        {
            if (HasUnsavedChanges)
            {
                bool confirmed = await ConfirmSave.Handle("Save changes?");
                if (!confirmed) return;
            }
            await SaveDocumentAsync(ct);
        });
    }
}

// In the view
public class DocumentView
{
    public DocumentView(DocumentViewModel viewModel)
    {
        viewModel.ConfirmSave.RegisterHandler(async interaction =>
        {
            bool result = await DisplayAlert("Confirm", interaction.Input, "Yes", "No");
            interaction.SetOutput(result);
        });
    }
}
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

R3Ext is built on the shoulders of giants, bringing together proven patterns from the reactive programming ecosystem:

-   **[R3](https://github.com/Cysharp/R3)** by Yoshifumi Kawai (neuecc) and Cysharp
    -   High-performance reactive foundation with zero-allocation design
    -   Core observable primitives and scheduling infrastructure
    -   Native AOT and trimming support

-   **[ReactiveUI](https://github.com/reactiveui/ReactiveUI)** by the ReactiveUI team
    -   `RxCommand` pattern for reactive commands
    -   `Interaction` workflow for view-viewmodel communication
    -   MVVM binding concepts and `WhenChanged` operator inspiration

-   **[DynamicData](https://github.com/reactivemarbles/DynamicData)** by Roland Pheasant and ReactiveMarbles
    -   Complete port of observable collections to R3
    -   Cache and list operators for reactive collections
    -   Change set optimization and transformation pipelines

-   **[ReactiveMarbles](https://github.com/reactivemarbles)** - Community-driven reactive extensions
    -   Extension operator implementations
    -   Advanced observable composition patterns

R3Ext combines these battle-tested patterns with modern .NET features (source generators, AOT compilation, unsafe accessor) to deliver a production-ready reactive programming experience.

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## Acknowledgments

Special thanks to:

-   [Yoshifumi Kawai (neuecc)](https://github.com/neuecc) and the [Cysharp team](https://github.com/Cysharp) for R3's exceptional performance foundation
-   The [ReactiveUI team](https://github.com/reactiveui) for pioneering reactive MVVM patterns in .NET
-   [Roland Pheasant](https://github.com/RolandPheasant) for DynamicData's innovative observable collection patterns
-   The [ReactiveMarbles community](https://github.com/reactivemarbles) for comprehensive reactive operator libraries
-   The [.NET Community](https://dotnet.microsoft.com/platform/community) for continuous support and feedback

---

<div align="center">

**[⬆ Back to Top](#r3ext)**

Made with ❤️ for the Reactive Programming Community

</div>
