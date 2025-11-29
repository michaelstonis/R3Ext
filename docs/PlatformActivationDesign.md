# Platform Activation & View-ViewModel Binding Architecture

## Executive Summary

This document outlines a modern, extensible architecture for cross-platform view-viewmodel binding and activation lifecycle management. The design prioritizes:

1. **AOT Compatibility** - No reflection, source-generated code, no service locators
2. **Platform Agnosticism** - Core abstractions work across MAUI, Blazor, Avalonia, Uno
3. **Extensibility** - New platforms can be added via source generators and extension methods
4. **Performance** - Closure-free patterns, minimal allocations, static lambdas
5. **Flexibility** - Multiple activation strategies per platform
6. **No Base Classes** - Use source generation and extensions, not inheritance

**Target Framework**: .NET 9+

---

## Research Analysis

### ReactiveUI's Current Approach

**Strengths:**
- `IViewFor<TViewModel>` pattern for view-viewmodel association
- `IActivatableView` / `IActivatableViewModel` separation
- `WhenActivated` extension method for lifecycle-scoped disposables
- Platform-specific `IActivationForViewFetcher` implementations

**Weaknesses:**
- Heavy use of reflection (`GetAffinityForView`, service locator pattern)
- Not AOT-compatible without workarounds
- Single activation strategy per view type (no flexibility)
- Service locator anti-pattern (`Locator.Current.GetServices`)

### Platform Lifecycle Events

| Platform | Activation Events | Deactivation Events | Notes |
|----------|------------------|---------------------|-------|
| **MAUI Page** | `Appearing` | `Disappearing` | Navigation-based lifecycle |
| **MAUI View** | `IsVisible` changes | `IsVisible` changes | Property-based activation |
| **MAUI (alternative)** | `Loaded` | `Unloaded` | DOM-like lifecycle |
| **Blazor** | `OnAfterRender(firstRender: true)` | `Dispose()` | Component lifecycle |
| **Avalonia** | `AttachedToVisualTree` | `DetachedFromVisualTree` | Visual tree lifecycle |
| **Uno/WinUI** | `Loading`, `Loaded` | `Unloaded` | XAML element lifecycle |

### Modern Patterns to Adopt

1. **Source Generation over Reflection** - Generate activation bindings at compile time
2. **Interface Markers with Default Implementation** - Allow easy opt-in without boilerplate
3. **Observable Activation Streams** - Expose `Observable<ActivationState>` rather than callbacks
4. **Scoped Disposables** - Automatic cleanup tied to deactivation
5. **Multiple Activation Strategies** - Allow choosing `Appearing/Disappearing` vs `Loaded/Unloaded`
6. **Extensions over Base Classes** - Use extension methods and source generation, avoid inheritance hierarchies
7. **No Service Locators** - Standard .NET DI only, no Splat or custom locators

---

## Proposed Architecture

### Core Abstractions (Platform-Agnostic)

```
R3Ext/
├── Activation/
│   ├── IActivatable.cs              # Marker interface for activatable objects
│   ├── IActivatableView.cs          # View that can be activated/deactivated
│   ├── IViewFor.cs                  # View-ViewModel association
│   ├── ActivationState.cs           # Enum: Activated, Deactivated
│   ├── ActivationContext.cs         # Context passed during activation
│   ├── ActivatableExtensions.cs     # WhenActivated, WhenLoaded extensions
│   └── ViewModelActivator.cs        # ViewModel-side activation support
```

### Platform-Specific Implementations

```
R3Ext.Maui/
├── Activation/
│   ├── MauiActivationProvider.cs    # Registers MAUI-specific activation
│   ├── PageActivationExtensions.cs  # Extensions for Page lifecycle
│   ├── ViewActivationExtensions.cs  # Extensions for View lifecycle
│   └── ActivationSourceGenerator/   # Source generator for IViewFor<T>

R3Ext.Blazor/
├── Activation/
│   ├── BlazorActivationProvider.cs
│   ├── ComponentActivationExtensions.cs  # Extensions for component lifecycle
│   └── ActivationSourceGenerator/   # Source generator for components

R3Ext.Avalonia/
├── Activation/
│   ├── AvaloniaActivationProvider.cs
│   ├── VisualActivationExtensions.cs  # Extensions for visual tree
│   └── ActivationSourceGenerator/   # Source generator for controls
```

> **Note**: All platform packages use extension methods and source generation.
> No base classes are provided - users implement interfaces on their own types.

### Key Interfaces

```csharp
/// <summary>
/// Marker interface for objects that support activation lifecycle.
/// </summary>
public interface IActivatable
{
    /// <summary>
    /// Observable stream of activation state changes.
    /// </summary>
    Observable<ActivationState> Activation { get; }
}

/// <summary>
/// Represents a view that can be activated and deactivated.
/// </summary>
public interface IActivatableView : IActivatable
{
}

/// <summary>
/// Associates a view with its view model.
/// </summary>
public interface IViewFor<TViewModel> : IActivatableView
    where TViewModel : class
{
    /// <summary>
    /// Gets or sets the view model for this view.
    /// </summary>
    TViewModel? ViewModel { get; set; }
}

/// <summary>
/// Activation state enumeration.
/// </summary>
public enum ActivationState
{
    /// <summary>View/ViewModel has become active.</summary>
    Activated,
    
    /// <summary>View/ViewModel has become inactive.</summary>
    Deactivated
}

/// <summary>
/// Specifies the activation trigger for lifecycle management.
/// These are platform-agnostic concepts that map to platform-specific events.
/// </summary>
public enum ActivationTrigger
{
    /// <summary>
    /// Triggered when the view becomes visible/hidden.
    /// Maps to: MAUI Page.Appearing/Disappearing, Avalonia IsVisible, Blazor render state.
    /// </summary>
    Visibility,
    
    /// <summary>
    /// Triggered when the view is attached/detached from the UI hierarchy.
    /// Maps to: MAUI Loaded/Unloaded, Avalonia AttachedToVisualTree, Blazor OnAfterRender/Dispose.
    /// </summary>
    Attached,
    
    /// <summary>
    /// Triggered based on focus/interaction state.
    /// Maps to: MAUI Window.Activated, Avalonia Window.Activated, Blazor focus events.
    /// </summary>
    Focus
}
```

### WhenActivated Pattern

```csharp
public static class ActivatableExtensions
{
    /// <summary>
    /// Executes the block when the view is activated, disposing when deactivated.
    /// AOT-compatible implementation using static lambdas where possible.
    /// </summary>
    public static IDisposable WhenActivated(
        this IActivatableView view,
        Action<DisposableBag> block)
    {
        var serial = new SerialDisposableCore();
        
        return view.Activation
            .Subscribe(new ActivationObserver(serial, block));
    }
    
    /// <summary>
    /// Alternative using Attached/Detached lifecycle (Loaded/Unloaded events).
    /// </summary>
    public static IDisposable WhenAttached(
        this IActivatableView view,
        Action<DisposableBag> block);
}

// AOT-friendly observer implementation (no closure allocation on hot path)
file sealed class ActivationObserver(
    SerialDisposableCore serial,
    Action<DisposableBag> block) : Observer<ActivationState>
{
    protected override void OnNextCore(ActivationState state)
    {
        if (state == ActivationState.Activated)
        {
            var bag = new DisposableBag();
            block(bag);
            serial.Disposable = bag;
        }
        else
        {
            serial.Disposable = null;
        }
    }
    
    protected override void OnErrorResumeCore(Exception error) { }
    protected override void OnCompletedCore(Result result) { }
}
```

### Source Generator Approach

Instead of reflection-based fetchers, we'll use source generation:

```csharp
// User writes:
public partial class MyPage : ContentPage, IViewFor<MyViewModel>
{
    public MyViewModel? ViewModel { get; set; }
}

// Generator produces:
partial class MyPage : IActivatableView
{
    private Observable<ActivationState>? _activation;
    
    public Observable<ActivationState> Activation => 
        _activation ??= CreateActivation();
    
    private Observable<ActivationState> CreateActivation()
    {
        var appearing = Observable.FromEvent<EventHandler, ActivationState>(
            h => { void Handler(object? s, EventArgs e) => h(ActivationState.Activated); return Handler; },
            x => this.Appearing += x,
            x => this.Appearing -= x);
            
        var disappearing = Observable.FromEvent<EventHandler, ActivationState>(
            h => { void Handler(object? s, EventArgs e) => h(ActivationState.Deactivated); return Handler; },
            x => this.Disappearing += x,
            x => this.Disappearing -= x);
            
        return appearing.Merge(disappearing).DistinctUntilChanged();
    }
}
```

---

## Implementation Phases

### Phase 1: Core Abstractions
- [ ] Create `R3Ext/Activation/` directory structure
- [ ] Define `IActivatable`, `IActivatableView`, `IViewFor<T>` interfaces
- [ ] Define `ActivationState` enum and `ActivationStrategy` enum
- [ ] Implement `WhenActivated` and `WhenLoaded` extension methods
- [ ] Add `ViewModelActivator` for ViewModel-side activation

### Phase 2: MAUI Platform Support
- [ ] Create `R3Ext.Maui` project (or add to existing SampleApp temporarily)
- [ ] Implement `MauiPageActivation` using Appearing/Disappearing
- [ ] Implement `MauiViewActivation` using IsVisible property changes  
- [ ] Implement `MauiLoadedActivation` using Loaded/Unloaded events
- [ ] Create source generator for `IViewFor<T>` on MAUI types
- [ ] Add integration tests with SampleApp

### Phase 3: Blazor Platform Support
- [ ] Create `R3Ext.Blazor` project structure
- [ ] Implement `BlazorComponentActivation` using lifecycle methods
- [ ] Create source generator for `IViewFor<T>` on Blazor components
- [ ] Add extension methods for component activation

### Phase 4: Avalonia Platform Support
- [ ] Create `R3Ext.Avalonia` project structure
- [ ] Implement `AvaloniaVisualActivation` using visual tree events
- [ ] Create source generator for Avalonia controls

### Phase 5: Extensibility & Documentation
- [ ] Document how to add new platform support
- [ ] Create platform registration pattern
- [ ] Add comprehensive samples for each platform
- [ ] Performance benchmarks

---

## Comparison: ReactiveUI vs Proposed

| Aspect | ReactiveUI | Proposed R3Ext |
|--------|-----------|----------------|
| View-VM Association | `IViewFor<T>` interface | `IViewFor<T>` interface (same) |
| Activation Discovery | Reflection + Service Locator | Source Generation |
| Dependency Injection | Splat (custom DI) | `Microsoft.Extensions.DependencyInjection` |
| Service Locator | Yes (Splat) | **None** - DI only |
| AOT Support | Limited | Full |
| Activation Triggers | One per view type | Multiple via `WhenActivated()` / `WhenAttached()` |
| Platform Extension | Implement `IActivationForViewFetcher` | Separate NuGet packages |
| ViewModel Activation | `ViewModelActivator` class | Auto-activation with opt-out |
| WhenActivated API | Extension method | Extension method (similar API) |
| Package Structure | Monolithic + platform packages | Separate packages per platform |
| Base Classes | `ReactiveContentPage`, etc. | **None** - extensions + source gen |
| Target Framework | .NET 6+ | .NET 9+ |

---

## Design Decisions

### 1. Opt-in Activation via Explicit Interface Implementation

**Decision**: Require explicit `IViewFor<T>` interface implementation for activation support.

**Rationale**:
- Explicit opt-in gives developers full control over which views participate in activation
- Reduces magic and unexpected behavior
- Better for AOT scenarios where implicit discovery is problematic
- Aligns with modern .NET patterns (explicit > implicit)

**Usage**:
```csharp
// Explicit opt-in - only views implementing IViewFor<T> get activation support
public partial class MyPage : ContentPage, IViewFor<MyViewModel>
{
    public MyViewModel? ViewModel { get; set; }
}
```

### 2. Separate Methods for Activation Strategies

**Decision**: Use distinct methods `WhenActivated()` and `WhenLoaded()` for different lifecycle strategies.

**Rationale**:
- Clear, self-documenting API
- No magic strings or enums to remember
- IntelliSense-friendly discovery
- Each method has clear semantics

**API**:
```csharp
public static class ActivatableExtensions
{
    /// <summary>
    /// Executes block when view becomes visible (Appearing/Disappearing).
    /// Ideal for: pausing/resuming subscriptions, analytics, visibility-based logic.
    /// Uses ActivationTrigger.Visibility semantics.
    /// </summary>
    public static IDisposable WhenActivated(
        this IActivatableView view,
        Action<DisposableBag> block);
    
    /// <summary>
    /// Executes block when view is attached to UI hierarchy (Loaded/Unloaded).
    /// Ideal for: one-time setup, resource allocation, element measurement.
    /// Uses ActivationTrigger.Attached semantics.
    /// </summary>
    public static IDisposable WhenAttached(
        this IActivatableView view,
        Action<DisposableBag> block);
}
```

### 3. Automatic ViewModel Activation with Opt-out

**Decision**: When a view activates, automatically activate its ViewModel. Provide opt-out mechanism.

**Rationale**:
- Most common use case is synchronized view/viewmodel activation
- Reduces boilerplate for the 90% case
- Opt-out available for advanced scenarios

**Implementation**:
```csharp
public interface IViewFor<TViewModel> : IActivatableView
    where TViewModel : class
{
    TViewModel? ViewModel { get; set; }
    
    /// <summary>
    /// When true (default), ViewModel is automatically activated/deactivated with view.
    /// Set to false to manage ViewModel activation manually.
    /// </summary>
    bool AutoActivateViewModel => true;
}
```

### 4. Dependency Injection Integration

**Decision**: Design for modern .NET DI (`Microsoft.Extensions.DependencyInjection`) compatibility.

**Rationale**:
- Standard .NET pattern, not a custom service locator
- Works with MAUI's built-in DI container
- Supports scoped services and lifetime management
- Easy testing with mock services

**DI Integration**:
```csharp
// Service registration in MauiProgram.cs
public static class MauiActivationExtensions
{
    public static MauiAppBuilder UseR3Activation(this MauiAppBuilder builder)
    {
        // Register activation services
        builder.Services.AddSingleton<IActivationService, MauiActivationService>();
        
        // Optional: auto-register ViewModels
        builder.Services.AddTransient<MyViewModel>();
        
        return builder;
    }
}

// ViewModel injection in views
public partial class MyPage : ContentPage, IViewFor<MyViewModel>
{
    public MyPage(MyViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }
    
    public MyViewModel? ViewModel { get; set; }
}
```

### 5. Separate Platform Packages

**Decision**: Create separate NuGet packages per platform.

**Packages**:
- `R3Ext` - Core abstractions (interfaces, base extensions)
- `R3Ext.Maui` - MAUI-specific activation providers
- `R3Ext.Blazor` - Blazor-specific activation providers  
- `R3Ext.Avalonia` - Avalonia-specific activation providers
- `R3Ext.Uno` - Uno Platform-specific activation providers

**Rationale**:
- Tree-shaking: only include what you need
- Smaller package sizes
- Platform-specific dependencies don't pollute other platforms
- Independent versioning and release cycles
- Clear separation of concerns

**Project Structure**:
```
R3Ext/                          # Core abstractions (net9.0)
├── Activation/
│   ├── IActivatable.cs
│   ├── IActivatableView.cs
│   ├── IViewFor.cs
│   ├── ActivationTrigger.cs
│   └── ActivatableExtensions.cs

R3Ext.Maui/                     # MAUI platform package (net9.0-*)
├── R3Ext.Maui.csproj          # References R3Ext, Microsoft.Maui.*
├── MauiActivationService.cs
├── PageActivationExtensions.cs
├── ViewActivationExtensions.cs
└── ServiceCollectionExtensions.cs

R3Ext.Blazor/                   # Blazor platform package (net9.0)
├── R3Ext.Blazor.csproj
├── BlazorActivationService.cs
├── ComponentActivationExtensions.cs  # Extensions, not base classes
└── ServiceCollectionExtensions.cs
```

> **Design Principle**: No base classes are provided. All functionality is delivered
> through extension methods and source generation, keeping user code inheritance-free.

---

## Updated Architecture

### Core Package (`R3Ext`)

The core package contains only platform-agnostic interfaces and extensions:

```csharp
// No platform dependencies - pure abstractions
// Target: net9.0
namespace R3Ext.Activation;

public interface IActivatable
{
    Observable<ActivationState> Activation { get; }
}

public interface IActivatableView : IActivatable
{
}

public interface IActivatableViewModel : IActivatable
{
    ViewModelActivator Activator { get; }
}

public interface IViewFor<TViewModel> : IActivatableView
    where TViewModel : class
{
    TViewModel? ViewModel { get; set; }
    bool AutoActivateViewModel => true;
}

/// <summary>
/// Platform-agnostic activation triggers.
/// Each platform maps these to appropriate native events.
/// </summary>
public enum ActivationTrigger
{
    Visibility,  // Visible/Hidden state changes
    Attached,    // Attached/Detached from UI hierarchy
    Focus        // Focus/Activated state changes
}

public interface IActivationService
{
    IDisposable RegisterView(IActivatableView view);
}
```

### Platform Package (`R3Ext.Maui`)

```csharp
namespace R3Ext.Maui;

public static class MauiActivationExtensions
{
    public static MauiAppBuilder UseR3Activation(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton<IActivationService, MauiActivationService>();
        return builder;
    }
}

public class MauiActivationService : IActivationService
{
    public IDisposable RegisterView(IActivatableView view)
    {
        return view switch
        {
            Page page => RegisterPageActivation(page),
            View v => RegisterViewActivation(v),
            _ => Disposable.Empty
        };
    }
}
```

---

## Next Steps

1. ✅ Design decisions finalized
2. ✅ Feature branch created (`feature/platform-activation`)
3. Implement Phase 1 (Core Abstractions in `R3Ext`)
4. Create `R3Ext.Maui` project for Phase 2
5. Integrate with SampleApp for validation
3. Implement Phase 1 (Core Abstractions)
4. Implement Phase 2 (MAUI Support) as proof-of-concept
5. Gather feedback and iterate
