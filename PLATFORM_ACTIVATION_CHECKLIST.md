# Platform Activation Implementation Checklist

**Branch**: `feature/platform-activation`  
**Started**: November 29, 2025  
**Status**: 🟡 In Progress

---

## Design Decisions (Finalized)

| Decision                  | Choice                                     | Rationale                            |
| ------------------------- | ------------------------------------------ | ------------------------------------ |
| Activation Opt-in         | Explicit `IViewFor<T>` implementation      | Developer control, AOT-friendly      |
| Activation Triggers       | Platform-agnostic `ActivationTrigger` enum | Flexible terminology                 |
| Activation Methods        | `WhenActivated()` + `WhenAttached()`       | Clear API, self-documenting          |
| ViewModel Auto-activation | Yes, with opt-out                          | Reduces boilerplate for 90% case     |
| Dependency Injection      | `Microsoft.Extensions.DependencyInjection` | Standard .NET pattern, no Splat      |
| Package Structure         | Separate packages per platform             | Tree-shaking, smaller dependencies   |
| Base Classes              | **None** - use extensions + source gen     | Avoid inheritance hierarchies        |
| Target Framework          | .NET 9+                                    | Modern platform baseline             |
| Service Locator           | **None**                                   | DI only, no Splat or custom locators |

---

## Phase 1: Core Abstractions (in `R3Ext`) ✅ COMPLETE

### 1.1 Directory Structure & Interfaces

-   [x] Create `R3Ext/Activation/` directory
-   [x] Create `IActivatable.cs` - base marker interface with `Observable<ActivationState> Activation`
-   [x] Create `IActivatableView.cs` - extends `IActivatable` for views
-   [x] Create `IActivatableViewModel.cs` - extends `IActivatable` with `ViewModelActivator`
-   [x] Create `IViewFor.cs` - view-viewmodel association with `AutoActivateViewModel` property
-   [x] Create `ActivationState.cs` - enum: `Activated`, `Deactivated`
-   [x] Create `ActivationTrigger.cs` - enum: `Visibility`, `Attached`, `Focus` (platform-agnostic)
-   [ ] Create `IActivationService.cs` - DI-friendly activation service interface (deferred to Phase 2)

### 1.2 ViewModel Activation

-   [x] Create `ViewModelActivator.cs` - manages VM activation lifecycle
-   [x] Implement `Activated` and `Deactivated` observables
-   [x] Support explicit `Activate()` / `Deactivate()` methods
-   [x] Handle opt-out scenario for auto-activation
-   [x] Ensure AOT compatibility (no reflection)

### 1.3 Extension Methods

-   [x] Create `ActivatableViewExtensions.cs`
-   [x] Add `WhenActivated(ActivationBlock block)` - visibility-based (uses static observer)
-   [x] Add `WhenAttached(ActivationBlock block)` - hierarchy-based (uses static observer)
-   [x] Create `ActivatableViewModelExtensions.cs`
-   [x] Add ViewModel-side `WhenActivated` extension
-   [x] Use `file` classes for observers to avoid allocations

> **Note**: Changed from `Action<DisposableBag>` to `ActivationBlock(ref DisposableBag)` delegate because `DisposableBag` is a struct and modifications must be visible to caller.

### 1.4 Tests

-   [x] Create `R3Ext.Tests/Activation/` directory
-   [x] Add unit tests for `ViewModelActivator` (4 tests)
-   [x] Add unit tests for `WhenActivated` extensions (10 tests)
-   [x] Add unit tests for `WhenAttached` extensions (included in view tests)
-   [x] Add tests for auto-activation of ViewModels
-   [x] Add tests for opt-out of auto-activation
-   [x] Verify no closure allocations on hot path

> **Commit**: `755f68e` - 19 activation tests passing, 478 total tests pass

---

## Phase 2: MAUI Platform Support (new `R3Ext.Maui` project)

### 2.1 Project Setup

-   [ ] Create `R3Ext.Maui/` project directory
-   [ ] Create `R3Ext.Maui.csproj` targeting `net9.0-android`, `net9.0-ios`, `net9.0-maccatalyst`
-   [ ] Add reference to `R3Ext` core project
-   [ ] Add `Microsoft.Maui.Controls` dependency
-   [ ] Enable AOT compatibility settings

### 2.2 DI Integration

-   [ ] Create `ServiceCollectionExtensions.cs`
-   [ ] Implement `UseR3Activation(this MauiAppBuilder builder)` extension
-   [ ] Register `MauiActivationService` as `IActivationService`
-   [ ] **No Splat or service locator usage**
-   [ ] Document DI setup in README

### 2.3 Page Activation (Visibility trigger)

-   [ ] Create `PageActivationExtensions.cs`
-   [ ] Implement `GetActivation(this Page page)` returning `Observable<ActivationState>`
-   [ ] Use Appearing/Disappearing events
-   [ ] Handle edge cases (rapid navigation, modal pages)
-   [ ] Add tests with mock Page

### 2.4 View Activation (Visibility trigger)

-   [ ] Create `ViewActivationExtensions.cs`
-   [ ] Implement `GetActivation(this View view)` returning `Observable<ActivationState>`
-   [ ] Use IsVisible property changes
-   [ ] Handle initial visibility state
-   [ ] Add tests

### 2.5 Attached/Detached Support (Attached trigger)

-   [ ] Create `LoadedActivationExtensions.cs`
-   [ ] Implement Loaded/Unloaded event subscription
-   [ ] Wire to `WhenAttached` extension method
-   [ ] Document when to use `WhenAttached` vs `WhenActivated`

### 2.6 Source Generator Integration

-   [ ] Extend source generator or create MAUI-specific generator
-   [ ] Generate `Activation` property for `IViewFor<T>` implementations
-   [ ] Support `ContentPage`, `ContentView`, `Shell` types
-   [ ] Ensure AOT compatibility (no reflection in generated code)
-   [ ] Add integration tests

### 2.7 Sample App Integration

-   [ ] Update `R3Ext.SampleApp` to reference `R3Ext.Maui`
-   [ ] Update to .NET 9 target frameworks
-   [ ] Add `UseR3Activation()` to `MauiProgram.cs`
-   [ ] Update existing pages to use `IViewFor<T>`
-   [ ] Add `WhenActivated` usage examples
-   [ ] Add `WhenAttached` usage examples
-   [ ] Create dedicated activation demo page

---

## Phase 3: Blazor Platform Support (new `R3Ext.Blazor` project)

### 3.1 Project Setup

-   [ ] Create `R3Ext.Blazor/` project directory
-   [ ] Create `R3Ext.Blazor.csproj` targeting `net9.0`
-   [ ] Add reference to `R3Ext` core project
-   [ ] Add `Microsoft.AspNetCore.Components` dependency
-   [ ] Enable AOT compatibility settings

### 3.2 DI Integration

-   [ ] Create `ServiceCollectionExtensions.cs`
-   [ ] Implement `AddR3Activation(this IServiceCollection services)` extension
-   [ ] **No Splat or service locator usage**

### 3.3 Component Activation (via extensions + source generation)

-   [ ] Create `ComponentActivationExtensions.cs` - **not a base class**
-   [ ] Implement activation observable for components via source generator
-   [ ] Map `OnAfterRender(firstRender: true)` → Activated
-   [ ] Map `Dispose()` → Deactivated
-   [ ] Handle `OnParametersSet` for ViewModel changes
-   [ ] Support auto-activation of ViewModel

### 3.4 Tests & Samples

-   [ ] Create `R3Ext.Blazor.Tests` project
-   [ ] Add component lifecycle tests
-   [ ] Create sample Blazor app (optional)

---

## Phase 4: Avalonia Platform Support (new `R3Ext.Avalonia` project)

### 4.1 Project Setup

-   [ ] Create `R3Ext.Avalonia/` project directory
-   [ ] Create `R3Ext.Avalonia.csproj` targeting `net9.0`
-   [ ] Add reference to `R3Ext` core project
-   [ ] Add Avalonia dependencies
-   [ ] Enable AOT compatibility settings

### 4.2 Visual Tree Activation (via extensions)

-   [ ] Create `VisualActivationExtensions.cs` - **not a base class**
-   [ ] Implement `AttachedToVisualTree` → Activated (`WhenAttached`)
-   [ ] Implement `DetachedFromVisualTree` → Deactivated
-   [ ] Handle visual tree edge cases

### 4.3 DI Integration

-   [ ] Create `ServiceCollectionExtensions.cs`
-   [ ] Support Avalonia's DI patterns
-   [ ] **No Splat or service locator usage**

### 4.4 Tests & Samples

-   [ ] Create `R3Ext.Avalonia.Tests` project
-   [ ] Add visual tree lifecycle tests
-   [ ] Create sample Avalonia app (optional)

---

## Phase 5: Documentation & Polish

### 5.1 API Documentation

-   [ ] Add XML docs to all public APIs
-   [ ] Create API reference document
-   [ ] Add code examples to XML docs

### 5.2 User Guides

-   [ ] Write "Getting Started with Activation" guide
-   [ ] Write MAUI-specific guide
-   [ ] Write Blazor-specific guide
-   [ ] Write Avalonia-specific guide
-   [ ] Document migration from ReactiveUI

### 5.3 Performance

-   [ ] Benchmark activation overhead
-   [ ] Optimize hot paths (static lambdas, pooling)
-   [ ] Compare with ReactiveUI performance

### 5.4 Package Publishing

-   [ ] Configure NuGet package metadata for each project
-   [ ] Add package icons and descriptions
-   [ ] Create release workflow
-   [ ] Publish preview packages

---

## Progress Log

| Date       | Item             | Status       | Notes                                                               |
| ---------- | ---------------- | ------------ | ------------------------------------------------------------------- |
| 2025-11-29 | Design Document  | ✅ Complete  | Created PlatformActivationDesign.md                                 |
| 2025-11-29 | Feature Branch   | ✅ Complete  | Created feature/platform-activation                                 |
| 2025-11-29 | Design Decisions | ✅ Finalized | Opt-in, separate methods, auto-activation, MS DI, separate packages |
| 2025-11-29 | Phase 1 Core     | ✅ Complete  | Commit 755f68e - 19 tests, ActivationBlock delegate with ref param  |

---

## Package Structure

```
R3Ext/                          # Core abstractions (net9.0, platform-agnostic)
├── Activation/
│   ├── IActivatable.cs
│   ├── IActivatableView.cs
│   ├── IActivatableViewModel.cs
│   ├── IViewFor.cs
│   ├── IActivationService.cs
│   ├── ActivationState.cs
│   ├── ActivationTrigger.cs         # Platform-agnostic: Visibility, Attached, Focus
│   ├── ViewModelActivator.cs
│   └── ActivatableExtensions.cs     # WhenActivated, WhenAttached

R3Ext.Maui/                     # MAUI platform package (net9.0-*)
├── R3Ext.Maui.csproj
├── MauiActivationService.cs
├── PageActivationExtensions.cs      # Extensions, not providers
├── ViewActivationExtensions.cs
├── LoadedActivationExtensions.cs
└── ServiceCollectionExtensions.cs

R3Ext.Blazor/                   # Blazor platform package (net9.0)
├── R3Ext.Blazor.csproj
├── BlazorActivationService.cs
├── ComponentActivationExtensions.cs  # Extensions, NOT base classes
└── ServiceCollectionExtensions.cs

R3Ext.Avalonia/                 # Avalonia platform package (net9.0)
├── R3Ext.Avalonia.csproj
├── VisualActivationExtensions.cs     # Extensions, not providers
├── AvaloniaActivationService.cs
└── ServiceCollectionExtensions.cs
```

> **Key Principle**: No base classes. All platforms use extension methods and source generation.

---

## Dependencies

| Package                                               | Version  | Used By               |
| ----------------------------------------------------- | -------- | --------------------- |
| R3                                                    | Existing | All                   |
| R3Ext                                                 | Existing | All platform packages |
| Microsoft.Maui.Controls                               | 9.0+     | R3Ext.Maui            |
| Microsoft.AspNetCore.Components                       | 9.0+     | R3Ext.Blazor          |
| Avalonia                                              | 11.0+    | R3Ext.Avalonia        |
| Microsoft.Extensions.DependencyInjection.Abstractions | 9.0+     | All                   |

---

## Risk Assessment

| Risk                        | Impact | Mitigation                                      |
| --------------------------- | ------ | ----------------------------------------------- |
| MAUI version compatibility  | Medium | Test with MAUI 9.0                              |
| Source generator complexity | High   | Start with manual implementation, then generate |
| Breaking existing code      | Low    | Additive API, no breaking changes to R3Ext      |
| Performance overhead        | Medium | Benchmark early, use static lambdas             |
| DI container variations     | Low    | Depend only on abstractions                     |
| AOT compatibility           | High   | Test with NativeAOT, avoid reflection           |
| No base classes constraint  | Medium | Source generator must handle all boilerplate    |
