# Platform Activation & View-ViewModel Binding Architecture

## Executive Summary

This document outlines a modern, extensible architecture for cross-platform view-viewmodel binding and activation lifecycle management. The design prioritizes:

1. **AOT Compatibility** - No reflection, source-generated code
2. **Platform Agnosticism** - Core abstractions work across MAUI, Blazor, Avalonia, Uno
3. **Extensibility** - New platforms can be added via source generators
4. **Performance** - Closure-free patterns, minimal allocations
5. **Flexibility** - Multiple activation strategies per platform

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
│   ├── PageActivation.cs            # Appearing/Disappearing for Pages
│   ├── ViewActivation.cs            # IsVisible changes for Views
│   ├── ElementLoadedActivation.cs   # Loaded/Unloaded alternative
│   └── MauiViewForExtensions.cs     # Source-generated extensions

R3Ext.Blazor/
├── Activation/
│   ├── BlazorActivationProvider.cs
│   ├── ComponentActivation.cs       # OnAfterRender/Dispose lifecycle
│   └── BlazorViewForExtensions.cs

R3Ext.Avalonia/
├── Activation/
│   ├── AvaloniaActivationProvider.cs
│   ├── VisualActivation.cs          # AttachedToVisualTree/DetachedFromVisualTree
│   └── AvaloniaViewForExtensions.cs
```

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
/// Specifies the activation strategy for a platform view.
/// </summary>
public enum ActivationStrategy
{
    /// <summary>Use page Appearing/Disappearing (MAUI) or equivalent.</summary>
    Visibility,
    
    /// <summary>Use Loaded/Unloaded events.</summary>
    Loaded,
    
    /// <summary>Use visual tree attachment (Avalonia).</summary>
    VisualTree,
    
    /// <summary>Use component lifecycle (Blazor).</summary>
    ComponentLifecycle
}
```

### WhenActivated Pattern

```csharp
public static class ActivatableExtensions
{
    /// <summary>
    /// Executes the block when the view is activated, disposing when deactivated.
    /// </summary>
    public static IDisposable WhenActivated(
        this IActivatableView view,
        Action<CompositeDisposable> block)
    {
        var serial = new SerialDisposable();
        
        return view.Activation
            .Subscribe(state =>
            {
                if (state == ActivationState.Activated)
                {
                    var disposables = new CompositeDisposable();
                    block(disposables);
                    serial.Disposable = disposables;
                }
                else
                {
                    serial.Disposable = Disposable.Empty;
                }
            });
    }
    
    /// <summary>
    /// Alternative using Loaded/Unloaded lifecycle.
    /// </summary>
    public static IDisposable WhenLoaded(
        this IActivatableView view,
        Action<CompositeDisposable> block);
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
- [ ] Create base component class `RxComponent<TViewModel>`
- [ ] Add Blazor-specific source generation

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
| AOT Support | Limited | Full |
| Activation Strategies | One per view type | Multiple, user-selectable |
| Platform Extension | Implement `IActivationForViewFetcher` | Implement source generator |
| ViewModel Activation | `ViewModelActivator` class | `IActivatableViewModel` + extensions |
| WhenActivated API | Extension method | Extension method (similar API) |

---

## Open Questions

1. **Should activation be opt-in or automatic?**
   - Option A: Require explicit interface implementation (`IViewFor<T>`)
   - Option B: Auto-generate for all pages/views in project
   - **Recommendation**: Opt-in via interface for explicit control

2. **How to handle activation strategy selection?**
   - Option A: Attribute on class `[ActivationStrategy(Strategy.Loaded)]`
   - Option B: Method parameter `WhenActivated(strategy: ActivationStrategy.Loaded, ...)`
   - Option C: Separate methods `WhenLoaded()`, `WhenActivated()`
   - **Recommendation**: Option C for clarity and simplicity

3. **Should ViewModelActivator be automatic?**
   - When view activates, should we auto-activate the ViewModel?
   - **Recommendation**: Yes, with opt-out capability

4. **Package structure?**
   - Option A: Single `R3Ext` package with conditional compilation
   - Option B: Separate packages (`R3Ext.Maui`, `R3Ext.Blazor`, etc.)
   - **Recommendation**: Option B for tree-shaking and smaller dependencies

---

## Next Steps

1. Review and approve this design document
2. Create feature branch `feature/platform-activation`
3. Implement Phase 1 (Core Abstractions)
4. Implement Phase 2 (MAUI Support) as proof-of-concept
5. Gather feedback and iterate
