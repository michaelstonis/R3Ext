# R3Ext

A .NET 9 class library for extending the R3 reactive library, plus a sample .NET MAUI app to try out the extensions.

## Projects

-   `R3Ext` (Class Library, `net9.0`): Adds helpers for R3. Currently includes `ObservableDebugExtensions.Log()` for quick
    stream logging.
-   `R3Ext.SampleApp` (.NET MAUI): Minimal app wired with `UseR3()` to demonstrate R3 in a UI context. It has a simple
    ticker using R3 and the `Log()` extension.

## Extension Organization

Extensions formerly held in a single file are now grouped for clarity:

-   Async: async coordination helpers
-   Creation: convenience creation operators
-   Filtering / Collection: filtering, buffering, shuffling helpers
-   Timing: time-based operators using `TimeProvider`
-   ErrorHandling: patterns for safe observe / swallow
-   Combine: multi-stream combinators
-   Observer / Signal: observer helpers & reactive signal utilities
-   Command: reactive command abstraction (`RxCommand`)

## Reactive Commands (`RxCommand`)

`RxCommand<TInput,TOutput>` provides a unified reactive `ICommand` + `IObservable<TOutput>`:

-   Supports sync, `Task`, and `IObservable` execution forms
-   Exposes `IsExecuting`, `ThrownExceptions`, result stream, and `CanExecute` propagation
-   Composition via `RxCommand.CreateCombined(childs...)`
-   Background execution helper: `CreateRunInBackground`
-   Integrates with other streams via `InvokeCommand()` extension

### Basic Examples

```csharp
// Synchronous
var save = RxCommand.Create(() => SaveModel());

// Async with CancellationToken
var load = RxCommand.CreateFromTask(async ct => await LoadAsync(ct));

// Parameterized + result
var doubleIt = RxCommand<int,int>.Create(x => x * 2);

// From observable
var oneShot = RxCommand<int,string>.CreateFromObservable(id =>
	Observable.Return($"Item:{id}"));

// Combined
var c1 = RxCommand<int,int>.Create(x => x + 1);
var c2 = RxCommand<int,int>.Create(x => x * 3);
var combined = RxCommand<int,int>.CreateCombined(c1, c2); // emits int[]

// Invoke from another stream
source.InvokeCommand(doubleIt);
```

### Migrating from `ReactiveUICompatibleCommand`

The previous `ReactiveUICompatibleCommand` name was replaced by the shorter neutral `RxCommand`. Factory method shapes
remain the same; just rename the type and static class usages.

### `WhenChanged` Binding Fallback

Bindings generated for chained property paths have a reflective fallback: if a generated entry is missing,
`BindingRegistry` will attach `PropertyChanged` handlers to each owning object in the chain to rewire on intermediate
replacements.

## Prerequisites

-   .NET SDK 9 (verify with `dotnet --version`)
-   .NET MAUI workload (verify with `dotnet workload list` shows `maui`)
-   macOS for building iOS/Mac Catalyst; Android SDK/Xcode as applicable

## Build

```bash
cd R3Ext
 dotnet build R3Ext.sln
```

## Run (quick options)

-   Android emulator:

```bash
cd R3Ext/R3Ext.SampleApp
 dotnet build -t:Run -f net9.0-android
```

-   iOS simulator (requires Xcode):

```bash
cd R3Ext/R3Ext.SampleApp
 dotnet build -t:Run -f net9.0-ios /p:_DeviceName=:v2:udid=auto
```

-   Mac Catalyst:

```bash
cd R3Ext/R3Ext.SampleApp
 dotnet build -t:Run -f net9.0-maccatalyst
```

## Notes

-   MAUI is configured with `UseR3()` via `R3Extensions.Maui`, so time/frame-based operators marshal correctly for UI
    updates.
-   Example extension: `Observable.Log(tag)` wraps `Do` to print events to Debug output.
