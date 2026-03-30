# Platform Activation Implementation Checklist

**Branch**: `feature/platform-activation`  
**Started**: November 29, 2025  
**Status**: ✅ Phase 5 Complete (Uno Platform Support)

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
| **Activation Provider**   | **Registry pattern in core**               | Platform-agnostic, extensible        |

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

-   [x] Create `R3Ext.Maui/` project directory
-   [x] Create `R3Ext.Maui.csproj` targeting `net9.0-android`, `net9.0-ios`, `net9.0-maccatalyst`
-   [x] Add reference to `R3Ext` core project
-   [x] Add `Microsoft.Maui.Controls` dependency
-   [x] Enable AOT compatibility settings

### 2.2 DI Integration

-   [x] Create `MauiAppBuilderExtensions.cs` (renamed from ServiceCollectionExtensions)
-   [x] Implement `UseR3Activation(this MauiAppBuilder builder)` extension
-   [ ] Register `MauiActivationService` as `IActivationService` (deferred - not needed for current API)
-   [x] **No Splat or service locator usage**
-   [x] Document DI setup in README (Platform Activation section added)

### 2.3 Page Activation (Visibility trigger)

-   [x] Create `PageActivationExtensions.cs`
-   [x] Implement `GetActivation(this Page page)` returning `Observable<ActivationState>`
-   [x] Use Appearing/Disappearing events
-   [ ] Handle edge cases (rapid navigation, modal pages) - deferred to integration testing
-   [ ] Add tests with mock Page

### 2.4 View Activation (Visibility trigger)

-   [x] Create `ViewActivationExtensions.cs`
-   [x] Implement `GetActivation(this View view)` returning `Observable<ActivationState>`
-   [x] Use IsVisible property changes
-   [x] Handle initial visibility state
-   [x] Add tests (41 tests in R3Ext.Maui.Tests)

### 2.5 Attached/Detached Support (Attached trigger)

-   [x] Loaded/Unloaded in `PageActivationExtensions.cs` and `ViewActivationExtensions.cs`
-   [x] Implement Loaded/Unloaded event subscription via `GetLoadedActivation()`
-   [x] Wire to `WhenAttached` extension method via `MauiActivatableViewExtensions`
-   [x] Document when to use `WhenAttached` vs `WhenActivated` (ActivationGuide.md)

### 2.6 Source Generator Integration (Required for Full IViewFor Support)

> **Updated**: Source generator is now COMPLETE. The `IViewFor<TViewModel>` pattern gets automatic:
>
> 1. **ViewModel ↔ BindingContext sync** - When ViewModel is set, BindingContext updates (and vice versa)
> 2. **DI resolution** - If ViewModel is null, resolve from `IServiceProvider`
> 3. **Activation property** - Generate the `Activation` observable from lifecycle events

#### 2.6.1 ViewModel-BindingContext Synchronization

-   [x] Generate `ViewModel` property that syncs with `BindingContext`
-   [x] Handle two-way sync (setting either updates the other)
-   [x] Handle type mismatches gracefully (BindingContext set to wrong type)
-   [x] Fire property change notifications for ViewModel

#### 2.6.2 DI Integration

-   [x] Generate `InitializeViewFor()` method that accepts `IServiceProvider` (or uses app's services)
-   [x] Auto-resolve ViewModel from DI if not explicitly set
-   [x] Support property injection pattern (ViewModel assigned after DI resolution)
-   [x] Use `Application.Current?.Handler?.MauiContext?.Services` as fallback

#### 2.6.3 Activation Property Generation

-   [x] Generate `Activation` property for `IViewFor<T>` implementations
-   [x] **Platform-agnostic**: Uses `ViewActivation.GetActivation()` from registry
-   [x] No MAUI-specific code in generator (no `Page`, `View`, `ContentPage` references)
-   [x] Platforms register providers via `ActivationProviderRegistry.Register()`
-   [x] Ensure AOT compatibility (no reflection, uses MetadataName for interface lookup)

#### 2.6.4 Source Generator Tasks

-   [x] Added to `R3Ext.Bindings.SourceGenerator` project (ViewForGenerator.cs)
-   [x] Detect `IViewFor<T>` implementations using MetadataName lookup
-   [x] Generate partial class with ViewModel/BindingContext sync
-   [x] Generate Activation property with explicit interface implementation
-   [x] Generate InitializeViewFor() helper for DI resolution
-   [x] **Platform-agnostic**: Generator has no platform-specific dependencies
-   [x] Add integration tests for source generator (14 tests in ViewForGeneratorIntegrationTests.cs)
-   [x] Document generated code pattern (in IViewForSourceGeneratorDesign.md)

#### 2.6.5 Platform-Agnostic Activation Registry (NEW)

-   [x] Create `R3Ext/Activation/ViewActivation.cs` with:
    -   [x] `ActivationProviderRegistry.Register(ActivationProvider)` - platform registration
    -   [x] `ViewActivation.GetActivation(this IActivatableView)` - extension method
    -   [x] `ActivationProviderRegistry.Clear()` - for testing
-   [x] Thread-safe provider registration with lock
-   [x] First provider returning non-null wins
-   [x] Clear error when no provider registered

#### 2.6.6 MAUI Provider Registration

-   [x] Update `MauiAppBuilderExtensions.UseR3Activation()` to register MAUI provider
-   [x] Provider handles `Page` → `PageActivationExtensions.GetActivation()`
-   [x] Provider handles `View` → `ViewActivationExtensions.GetActivation()`
-   [x] Idempotent registration (safe to call multiple times)

### 2.7 Sample App Integration

-   [x] Update `R3Ext.SampleApp` to reference `R3Ext.Maui`
-   [x] Already on .NET 9 target frameworks
-   [x] Add `UseR3Activation()` to `MauiProgram.cs`
-   [x] ActivationDemoPage uses source-generated `IViewFor<T>` infrastructure
-   [x] Add `WhenActivated` usage examples (in ActivationDemoPage)
-   [x] Add `WhenAttached` usage examples (in ActivationDemoPage)
-   [x] Create dedicated activation demo page (`ActivationDemoPage.xaml/cs`)

> **Commits**:
>
> -   `6062aed` - R3Ext.Maui created with Page/View activation, Loaded/Unloaded support, DI integration
> -   (pending) - Added R3Ext.Maui.Tests (41 tests), ActivationDemoPage with WhenActivated + WhenAttached examples
> -   (pending) - Added ViewForGenerator source generator for IViewFor<T> infrastructure

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

## Phase 4: Avalonia Platform Support (new `R3Ext.Avalonia` project) ✅ COMPLETE

### 4.1 Project Setup

-   [x] Create `R3Ext.Avalonia/` project directory
-   [x] Create `R3Ext.Avalonia.csproj` targeting `net9.0`
-   [x] Add reference to `R3Ext` core project
-   [x] Add Avalonia dependencies
-   [x] Enable AOT compatibility settings

### 4.2 Visual Tree Activation (via extensions)

-   [x] Create `VisualActivationExtensions.cs` - **not a base class**
-   [x] Implement `AttachedToVisualTree` → Activated (`WhenActivated`)
-   [x] Implement `DetachedFromVisualTree` → Deactivated
-   [x] Handle visual tree edge cases
-   [x] Implement `GetVisibilityActivation()` for IsVisible changes

### 4.3 DI Integration

-   [x] Create `AvaloniaAppBuilderExtensions.cs`
-   [x] Implement `UseR3Activation(this AppBuilder)` extension
-   [x] Register Avalonia activation provider
-   [x] **No Splat or service locator usage**

### 4.4 Tests & Samples

-   [x] Create `R3Ext.Avalonia.Tests` project (26 tests)
-   [x] Add visual tree lifecycle tests
-   [x] Add visibility activation tests
-   [x] Add WhenActivated/WhenAttached tests
-   [x] Create sample Avalonia app (R3Ext.Avalonia.SampleApp)

> **Commits**:
>
> -   `eaa47d4` - R3Ext.Avalonia with Visual activation, AppBuilder extensions, provider registration
> -   `eaa47d4` - R3Ext.Avalonia.Tests with 26 tests for activation lifecycle
> -   `eaa47d4` - R3Ext.Avalonia.SampleApp with shared ViewModels, R3Ext.Bindings.AvaloniaTargets

---

## Phase 5: Uno Platform Support (new `R3Ext.Uno` project) ✅ COMPLETE

> **Key Learning from MAUI/Avalonia**: The binding targets shim (MSBuild task) is REQUIRED because
> source generators run in isolation and cannot see x:Name fields generated by the platform's XAML generator.

### 5.1 Project Setup

-   [x] Create `R3Ext.Uno/` project directory
-   [x] Create `R3Ext.Uno.csproj` targeting `net9.0`
-   [x] Add reference to `R3Ext` core project
-   [x] Add `Uno.WinUI` dependencies (for Microsoft.UI.Xaml namespace)
-   [x] Enable AOT compatibility settings

### 5.2 FrameworkElement Activation (via extensions)

-   [x] Create `FrameworkElementActivationExtensions.cs` - **not a base class**
-   [x] Implement `Loaded` → Activated (`WhenActivated`)
-   [x] Implement `Unloaded` → Deactivated
-   [x] Handle lifecycle edge cases
-   [x] Implement `GetVisibilityActivation()` for Visibility property changes

### 5.3 DI Integration

-   [x] Create `UnoHostBuilderExtensions.cs`
-   [x] Implement `UseR3Activation(this IHostBuilder)` extension
-   [x] Implement `AddR3Activation(this IServiceCollection)` extension
-   [x] Register Uno activation provider via `ActivationProviderRegistry.Register()`
-   [x] **No Splat or service locator usage**

### 5.4 Binding Targets Shim (R3Ext.Bindings.UnoTargets)

> **Why needed**: Uno's XamlCodeGenerator produces `XamlCodeGenerator_*.cs` files with x:Name backing fields.
> Our binding generator can't see these during compilation.

-   [x] Create `R3Ext.Bindings.UnoTargets/` project directory
-   [x] Create MSBuild task targeting `netstandard2.0`
-   [x] Scan `XamlCodeGenerator_*.cs` generated files for field declarations
-   [x] Extract x:Name → control type mappings
-   [x] Handle `Microsoft.UI.Xaml.Controls` namespace mappings
-   [x] Produce `R3Ext.BindingTargets.json` for source generator consumption
-   [x] Create `.props` and `.targets` files for auto-import

### 5.5 Tests

-   [x] Create `R3Ext.Uno.Tests` project
-   [x] Add FrameworkElement lifecycle tests (Loaded/Unloaded)
-   [x] Add provider registration tests
-   [x] All 7 tests passing

### 5.6 Sample App

-   [x] Create `R3Ext.Uno.SampleApp/` project
-   [x] Reference shared ViewModels from `R3Ext.SampleApp.ViewModels`
-   [x] Create ActivationDemoPage with WhenActivated examples
-   [x] Create TimerDemoPage with WhenAttached examples
-   [x] Configure for Windows target (WebAssembly requires Uno SDK)

> **Commits**:
>
> -   (pending) - R3Ext.Uno with FrameworkElement activation, provider registration, DI extensions
> -   (pending) - R3Ext.Bindings.UnoTargets with MSBuild task for x:Name metadata extraction
> -   (pending) - R3Ext.Uno.Tests with 7 tests
> -   (pending) - R3Ext.Uno.SampleApp with shared ViewModels

> **Uno Platform Specifics**:
>
> -   Namespace: `Microsoft.UI.Xaml` (WinUI 3 compatible)
> -   Lifecycle: `Loaded`/`Unloaded` events on `FrameworkElement`
> -   Binding: `DataContext` property (not `BindingContext`)
> -   Generated code: `XamlCodeGenerator_*.cs` with pattern `__that.fieldName = element;`

---

## Phase 6: Documentation & Polish

### 6.1 API Documentation

-   [ ] Add XML docs to all public APIs
-   [ ] Create API reference document
-   [ ] Add code examples to XML docs

### 6.2 User Guides

-   [x] Write "Getting Started with Activation" guide (ActivationGuide.md)
-   [x] Write MAUI-specific guide (included in ActivationGuide.md)
-   [ ] Write Blazor-specific guide
-   [ ] Write Avalonia-specific guide
-   [ ] Write Uno Platform-specific guide
-   [ ] Document migration from ReactiveUI

### 6.3 Performance

-   [ ] Benchmark activation overhead
-   [ ] Optimize hot paths (static lambdas, pooling)
-   [ ] Compare with ReactiveUI performance

### 6.4 Package Publishing

-   [ ] Configure NuGet package metadata for each project
-   [ ] Add package icons and descriptions
-   [ ] Create release workflow
-   [ ] Publish preview packages

---

## Progress Log

| Date       | Item                         | Status       | Notes                                                                |
| ---------- | ---------------------------- | ------------ | -------------------------------------------------------------------- |
| 2025-11-29 | Design Document              | ✅ Complete  | Created PlatformActivationDesign.md                                  |
| 2025-11-29 | Feature Branch               | ✅ Complete  | Created feature/platform-activation                                  |
| 2025-11-29 | Design Decisions             | ✅ Finalized | Opt-in, separate methods, auto-activation, MS DI, separate packages  |
| 2025-11-29 | Phase 1 Core                 | ✅ Complete  | Commit 755f68e - 19 tests, ActivationBlock delegate with ref param   |
| 2025-11-29 | Phase 2 MAUI                 | ✅ Complete  | Commit 6062aed - Page/View activation, Loaded, DI integration        |
| 2025-11-30 | ViewForGenerator             | ✅ Complete  | Platform-agnostic source generator, registry pattern                 |
| 2025-11-30 | Activation Provider Registry | ✅ Complete  | `ViewActivation.cs` with `ActivationProviderRegistry`                |
| 2025-11-30 | MAUI Provider                | ✅ Complete  | `UseR3Activation()` registers MAUI-specific activation provider      |
| 2025-11-30 | IAttachableViewModel         | ✅ Complete  | ViewModelAttacher, AttachableViewModelExtensions for VM WhenAttached |
| 2025-11-30 | ActivationGuide.md           | ✅ Complete  | Comprehensive docs: WhenActivated vs WhenAttached, MAUI setup        |
| 2025-11-30 | Source Gen Integration Tests | ✅ Complete  | 14 tests in ViewForGeneratorIntegrationTests.cs                      |
| 2025-11-30 | README Updates               | ✅ Complete  | Platform Activation section, UseR3Activation() setup, examples       |
| 2025-11-30 | R3Ext.Maui.Tests             | ✅ Complete  | 55 MAUI activation tests passing                                     |
| 2025-11-30 | Phase 4 Avalonia             | ✅ Complete  | Commit eaa47d4 - Avalonia activation, 26 tests, sample app           |
| 2025-12-06 | Phase 5 Uno Platform         | ✅ Complete  | R3Ext.Uno, R3Ext.Bindings.UnoTargets, 7 tests, sample app            |

---

## Package Structure

```
R3Ext/                          # Core abstractions (net9.0, platform-agnostic)
├── Activation/
│   ├── IActivatable.cs
│   ├── IActivatableView.cs
│   ├── IActivatableViewModel.cs
│   ├── IAttachableViewModel.cs      # VM attachment lifecycle
│   ├── IViewFor.cs
│   ├── ActivationState.cs
│   ├── ActivationTrigger.cs         # Platform-agnostic: Visibility, Attached, Focus
│   ├── ViewModelActivator.cs
│   ├── ViewModelAttacher.cs         # Manages VM attachment state
│   ├── ViewActivation.cs            # Activation provider registry
│   ├── ActivatableViewExtensions.cs # WhenActivated, WhenAttached for views
│   ├── ActivatableViewModelExtensions.cs
│   └── AttachableViewModelExtensions.cs  # WhenAttached for VMs

R3Ext.Maui/                     # MAUI platform package (net9.0-*)
├── R3Ext.Maui.csproj
├── MauiAppBuilderExtensions.cs      # UseR3Activation() for DI setup
├── MauiActivatableViewExtensions.cs # WhenActivated, WhenAttached MAUI impl
├── PageActivationExtensions.cs      # Page Appearing/Disappearing
├── ViewActivationExtensions.cs      # View IsVisible changes
└── Internal/
    ├── MauiActivationState.cs       # WhenActivated state management
    ├── MauiActivationObserver.cs    # Window-based activation
    ├── MauiAttachmentState.cs       # WhenAttached state management
    └── MauiAttachmentObserver.cs    # Unloaded-based cleanup

R3Ext.Maui.Tests/               # MAUI activation tests (55 tests)
├── MauiActivationFixture.cs         # Test fixture for provider registration
└── ViewForGeneratorIntegrationTests.cs  # Source generator tests

R3Ext.Blazor/                   # Blazor platform package (net9.0)
├── R3Ext.Blazor.csproj
├── BlazorActivationService.cs
├── ComponentActivationExtensions.cs  # Extensions, NOT base classes
└── ServiceCollectionExtensions.cs

R3Ext.Avalonia/                 # Avalonia platform package (net9.0)
├── R3Ext.Avalonia.csproj
├── VisualActivationExtensions.cs    # AttachedToVisualTree/Detached activation
├── AvaloniaActivationProviders.cs   # Provider registration
├── AvaloniaAppBuilderExtensions.cs  # UseR3Activation() for DI setup
└── Internal/
    └── (activation state management)

R3Ext.Uno/                      # Uno Platform package (net9.0)
├── R3Ext.Uno.csproj
├── FrameworkElementActivationExtensions.cs  # Loaded/Unloaded activation
├── UnoActivationProviders.cs        # Provider registration
├── UnoHostBuilderExtensions.cs      # UseR3Activation() for DI setup
└── ServiceCollectionExtensions.cs   # AddR3Activation() for DI

R3Ext.Bindings.UnoTargets/      # Uno binding targets shim
├── R3Ext.Bindings.UnoTargets.csproj
├── GenerateUnoUiBindingTargetsTask.cs  # MSBuild task
├── GenerateUiBindingTargets.targets
└── build/
    └── R3Ext.Bindings.UnoTargets.props
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
| Uno.WinUI                                             | 5.4+     | R3Ext.Uno             |
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
