# Platform Activation Implementation Checklist

**Branch**: `feature/platform-activation`  
**Started**: November 29, 2025  
**Status**: 🟡 In Progress

---

## Design Decisions (Finalized)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Activation Opt-in | Explicit `IViewFor<T>` implementation | Developer control, AOT-friendly |
| Activation Strategies | Separate methods: `WhenActivated()` + `WhenLoaded()` | Clear API, self-documenting |
| ViewModel Auto-activation | Yes, with opt-out | Reduces boilerplate for 90% case |
| Dependency Injection | `Microsoft.Extensions.DependencyInjection` | Standard .NET pattern |
| Package Structure | Separate packages per platform | Tree-shaking, smaller dependencies |

---

## Phase 1: Core Abstractions (in `R3Ext`)

### 1.1 Directory Structure & Interfaces
- [ ] Create `R3Ext/Activation/` directory
- [ ] Create `IActivatable.cs` - base marker interface with `Observable<ActivationState> Activation`
- [ ] Create `IActivatableView.cs` - extends `IActivatable` for views
- [ ] Create `IActivatableViewModel.cs` - extends `IActivatable` with `ViewModelActivator`
- [ ] Create `IViewFor.cs` - view-viewmodel association with `AutoActivateViewModel` property
- [ ] Create `ActivationState.cs` - enum: `Activated`, `Deactivated`
- [ ] Create `IActivationService.cs` - DI-friendly activation service interface

### 1.2 ViewModel Activation
- [ ] Create `ViewModelActivator.cs` - manages VM activation lifecycle
- [ ] Implement `Activated` and `Deactivated` observables
- [ ] Support explicit `Activate()` / `Deactivate()` methods
- [ ] Handle opt-out scenario for auto-activation

### 1.3 Extension Methods
- [ ] Create `ActivatableViewExtensions.cs`
- [ ] Add `WhenActivated(Action<DisposableBag> block)` - visibility-based
- [ ] Add `WhenLoaded(Action<DisposableBag> block)` - loaded-based
- [ ] Create `ActivatableViewModelExtensions.cs`
- [ ] Add ViewModel-side `WhenActivated` extension

### 1.4 Tests
- [ ] Create `R3Ext.Tests/Activation/` directory
- [ ] Add unit tests for `ViewModelActivator`
- [ ] Add unit tests for `WhenActivated` extensions
- [ ] Add unit tests for `WhenLoaded` extensions
- [ ] Add tests for auto-activation of ViewModels
- [ ] Add tests for opt-out of auto-activation

---

## Phase 2: MAUI Platform Support (new `R3Ext.Maui` project)

### 2.1 Project Setup
- [ ] Create `R3Ext.Maui/` project directory
- [ ] Create `R3Ext.Maui.csproj` targeting `net8.0-android`, `net8.0-ios`, `net8.0-maccatalyst`
- [ ] Add reference to `R3Ext` core project
- [ ] Add `Microsoft.Maui.Controls` dependency

### 2.2 DI Integration
- [ ] Create `ServiceCollectionExtensions.cs`
- [ ] Implement `UseR3Activation(this MauiAppBuilder builder)` extension
- [ ] Register `MauiActivationService` as `IActivationService`
- [ ] Document DI setup in README

### 2.3 Page Activation (Appearing/Disappearing)
- [ ] Create `PageActivationProvider.cs`
- [ ] Implement `GetActivation(Page page)` returning `Observable<ActivationState>`
- [ ] Handle edge cases (rapid navigation, modal pages)
- [ ] Add tests with mock Page

### 2.4 View Activation (IsVisible)
- [ ] Create `ViewActivationProvider.cs`
- [ ] Implement `GetActivation(View view)` returning `Observable<ActivationState>`
- [ ] Handle initial visibility state
- [ ] Add tests

### 2.5 Loaded/Unloaded Support
- [ ] Create `LoadedActivationProvider.cs`
- [ ] Implement Loaded/Unloaded event subscription
- [ ] Wire to `WhenLoaded` extension method
- [ ] Document when to use `WhenLoaded` vs `WhenActivated`

### 2.6 Source Generator Integration
- [ ] Extend source generator or create MAUI-specific generator
- [ ] Generate `Activation` property for `IViewFor<T>` implementations
- [ ] Support `ContentPage`, `ContentView`, `Shell` types
- [ ] Add integration tests

### 2.7 Sample App Integration
- [ ] Update `R3Ext.SampleApp` to reference `R3Ext.Maui`
- [ ] Add `UseR3Activation()` to `MauiProgram.cs`
- [ ] Update existing pages to use `IViewFor<T>`
- [ ] Add `WhenActivated` usage examples
- [ ] Add `WhenLoaded` usage examples
- [ ] Create dedicated activation demo page

---

## Phase 3: Blazor Platform Support (new `R3Ext.Blazor` project)

### 3.1 Project Setup
- [ ] Create `R3Ext.Blazor/` project directory
- [ ] Create `R3Ext.Blazor.csproj` targeting `net8.0`
- [ ] Add reference to `R3Ext` core project
- [ ] Add `Microsoft.AspNetCore.Components` dependency

### 3.2 DI Integration
- [ ] Create `ServiceCollectionExtensions.cs`
- [ ] Implement `AddR3Activation(this IServiceCollection services)` extension

### 3.3 Component Base Classes
- [ ] Create `RxComponentBase.cs` - base component with activation
- [ ] Create `RxComponentBase<TViewModel>.cs` - generic version with `IViewFor<T>`
- [ ] Implement `OnAfterRender(firstRender: true)` → Activated
- [ ] Implement `Dispose()` → Deactivated
- [ ] Handle `OnParametersSet` for ViewModel changes
- [ ] Support auto-activation of ViewModel

### 3.4 Tests & Samples
- [ ] Create `R3Ext.Blazor.Tests` project
- [ ] Add component lifecycle tests
- [ ] Create sample Blazor app (optional)

---

## Phase 4: Avalonia Platform Support (new `R3Ext.Avalonia` project)

### 4.1 Project Setup
- [ ] Create `R3Ext.Avalonia/` project directory
- [ ] Create `R3Ext.Avalonia.csproj` targeting `net8.0`
- [ ] Add reference to `R3Ext` core project
- [ ] Add Avalonia dependencies

### 4.2 Visual Tree Activation
- [ ] Create `VisualActivationProvider.cs`
- [ ] Implement `AttachedToVisualTree` → Activated
- [ ] Implement `DetachedFromVisualTree` → Deactivated
- [ ] Handle visual tree edge cases

### 4.3 DI Integration
- [ ] Create `ServiceCollectionExtensions.cs`
- [ ] Support Avalonia's DI patterns

### 4.4 Tests & Samples
- [ ] Create `R3Ext.Avalonia.Tests` project
- [ ] Add visual tree lifecycle tests
- [ ] Create sample Avalonia app (optional)

---

## Phase 5: Documentation & Polish

### 5.1 API Documentation
- [ ] Add XML docs to all public APIs
- [ ] Create API reference document
- [ ] Add code examples to XML docs

### 5.2 User Guides
- [ ] Write "Getting Started with Activation" guide
- [ ] Write MAUI-specific guide
- [ ] Write Blazor-specific guide
- [ ] Write Avalonia-specific guide
- [ ] Document migration from ReactiveUI

### 5.3 Performance
- [ ] Benchmark activation overhead
- [ ] Optimize hot paths (static lambdas, pooling)
- [ ] Compare with ReactiveUI performance

### 5.4 Package Publishing
- [ ] Configure NuGet package metadata for each project
- [ ] Add package icons and descriptions
- [ ] Create release workflow
- [ ] Publish preview packages

---

## Progress Log

| Date | Item | Status | Notes |
|------|------|--------|-------|
| 2025-11-29 | Design Document | ✅ Complete | Created PlatformActivationDesign.md |
| 2025-11-29 | Feature Branch | ✅ Complete | Created feature/platform-activation |
| 2025-11-29 | Design Decisions | ✅ Finalized | Opt-in, separate methods, auto-activation, MS DI, separate packages |

---

## Package Structure

```
R3Ext/                          # Core abstractions (platform-agnostic)
├── Activation/
│   ├── IActivatable.cs
│   ├── IActivatableView.cs
│   ├── IActivatableViewModel.cs
│   ├── IViewFor.cs
│   ├── IActivationService.cs
│   ├── ActivationState.cs
│   ├── ViewModelActivator.cs
│   └── ActivatableExtensions.cs

R3Ext.Maui/                     # MAUI platform package
├── R3Ext.Maui.csproj
├── MauiActivationService.cs
├── PageActivationProvider.cs
├── ViewActivationProvider.cs
├── LoadedActivationProvider.cs
└── ServiceCollectionExtensions.cs

R3Ext.Blazor/                   # Blazor platform package
├── R3Ext.Blazor.csproj
├── RxComponentBase.cs
├── BlazorActivationService.cs
└── ServiceCollectionExtensions.cs

R3Ext.Avalonia/                 # Avalonia platform package
├── R3Ext.Avalonia.csproj
├── VisualActivationProvider.cs
├── AvaloniaActivationService.cs
└── ServiceCollectionExtensions.cs
```

---

## Dependencies

| Package | Version | Used By |
|---------|---------|---------|
| R3 | Existing | All |
| R3Ext | Existing | All platform packages |
| Microsoft.Maui.Controls | 8.0+ | R3Ext.Maui |
| Microsoft.AspNetCore.Components | 8.0+ | R3Ext.Blazor |
| Avalonia | 11.0+ | R3Ext.Avalonia |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0+ | All |

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| MAUI version compatibility | Medium | Test with MAUI 8.0, 9.0 |
| Source generator complexity | High | Start with manual implementation, then generate |
| Breaking existing code | Low | Additive API, no breaking changes to R3Ext |
| Performance overhead | Medium | Benchmark early, use static lambdas |
| DI container variations | Low | Depend only on abstractions |
