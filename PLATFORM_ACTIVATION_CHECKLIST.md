# Platform Activation Implementation Checklist

**Branch**: `feature/platform-activation`  
**Started**: November 29, 2025  
**Status**: 🟡 In Progress

---

## Phase 1: Core Abstractions

### 1.1 Directory Structure & Interfaces
- [ ] Create `R3Ext/Activation/` directory
- [ ] Create `IActivatable.cs` - base marker interface
- [ ] Create `IActivatableView.cs` - view-specific activation
- [ ] Create `IActivatableViewModel.cs` - viewmodel activation support
- [ ] Create `IViewFor.cs` - view-viewmodel association interface
- [ ] Create `ActivationState.cs` - enum for activation states

### 1.2 Activation Context & Builder
- [ ] Create `ActivationContext.cs` - context passed during activation
- [ ] Create `ViewModelActivator.cs` - manages VM activation lifecycle
- [ ] Create `ActivationScope.cs` - scoped disposable container

### 1.3 Extension Methods
- [ ] Create `ActivatableViewExtensions.cs` - WhenActivated extensions
- [ ] Create `ActivatableViewModelExtensions.cs` - VM activation extensions
- [ ] Add `WhenActivated(Action<CompositeDisposable> block)` overload
- [ ] Add `WhenActivated(Func<IEnumerable<IDisposable>> block)` overload
- [ ] Add `WhenActivated(Action<Action<IDisposable>> block)` overload

### 1.4 Tests
- [ ] Create `R3Ext.Tests/Activation/` directory
- [ ] Add unit tests for `ViewModelActivator`
- [ ] Add unit tests for `WhenActivated` extensions
- [ ] Add tests for activation/deactivation cycle

---

## Phase 2: MAUI Platform Support

### 2.1 Project Setup
- [ ] Decide: Add to R3Ext or create R3Ext.Maui project
- [ ] Add MAUI-specific target framework if needed
- [ ] Configure conditional compilation symbols

### 2.2 Page Activation (Appearing/Disappearing)
- [ ] Create `MauiPageActivation.cs` - Page lifecycle observable
- [ ] Implement `GetActivation(Page page)` returning `Observable<ActivationState>`
- [ ] Handle edge cases (rapid navigation, modal pages)
- [ ] Add tests with mock Page

### 2.3 View Activation (IsVisible)
- [ ] Create `MauiViewActivation.cs` - View visibility observable
- [ ] Implement `GetActivation(View view)` returning `Observable<ActivationState>`
- [ ] Handle initial visibility state
- [ ] Add tests

### 2.4 Loaded/Unloaded Alternative
- [ ] Create `MauiLoadedActivation.cs` - Loaded/Unloaded events
- [ ] Implement `WhenLoaded` extension for alternative lifecycle
- [ ] Document when to use Loaded vs Appearing
- [ ] Add tests

### 2.5 Source Generator for IViewFor
- [ ] Extend `BindingGenerator.cs` or create new generator
- [ ] Generate `Activation` property implementation for `IViewFor<T>` classes
- [ ] Generate for `ContentPage`, `ContentView`, `Shell` etc.
- [ ] Add integration tests

### 2.6 Sample App Integration
- [ ] Update existing pages to use `IViewFor<T>`
- [ ] Add `WhenActivated` usage examples
- [ ] Add `WhenLoaded` usage examples
- [ ] Create activation demo page

---

## Phase 3: Blazor Platform Support

### 3.1 Project Setup
- [ ] Create `R3Ext.Blazor` project
- [ ] Add Blazor dependencies
- [ ] Configure for .NET 8+

### 3.2 Component Lifecycle Activation
- [ ] Create `RxComponentBase.cs` - base component with activation
- [ ] Create `RxComponentBase<TViewModel>.cs` - typed version
- [ ] Implement `OnAfterRender` → Activated
- [ ] Implement `Dispose` → Deactivated
- [ ] Handle `OnParametersSet` for VM changes

### 3.3 Extension Methods
- [ ] Create `BlazorActivationExtensions.cs`
- [ ] Port `WhenActivated` pattern for Blazor
- [ ] Add Blazor-specific helpers

### 3.4 Tests & Samples
- [ ] Create Blazor test project
- [ ] Add component lifecycle tests
- [ ] Create sample Blazor app

---

## Phase 4: Avalonia Platform Support

### 4.1 Project Setup
- [ ] Create `R3Ext.Avalonia` project
- [ ] Add Avalonia dependencies
- [ ] Configure for .NET 8+

### 4.2 Visual Tree Activation
- [ ] Create `AvaloniaVisualActivation.cs`
- [ ] Implement `AttachedToVisualTree` → Activated
- [ ] Implement `DetachedFromVisualTree` → Deactivated
- [ ] Handle visual tree edge cases

### 4.3 Source Generator
- [ ] Create Avalonia-specific generator
- [ ] Generate for `UserControl`, `Window`, etc.
- [ ] Add tests

### 4.4 Tests & Samples
- [ ] Create Avalonia test project
- [ ] Add visual tree lifecycle tests
- [ ] Create sample Avalonia app

---

## Phase 5: Documentation & Polish

### 5.1 API Documentation
- [ ] Add XML docs to all public APIs
- [ ] Create API reference document
- [ ] Add code examples to docs

### 5.2 User Guide
- [ ] Write "Getting Started with Activation" guide
- [ ] Write platform-specific guides
- [ ] Document migration from ReactiveUI

### 5.3 Performance
- [ ] Benchmark activation overhead
- [ ] Optimize hot paths
- [ ] Compare with ReactiveUI performance

### 5.4 Package Publishing
- [ ] Configure NuGet packages
- [ ] Add package descriptions
- [ ] Publish preview packages

---

## Progress Log

| Date | Item | Status | Notes |
|------|------|--------|-------|
| 2025-11-29 | Design Document | ✅ Complete | Created PlatformActivationDesign.md |
| 2025-11-29 | Feature Branch | ✅ Complete | Created feature/platform-activation |

---

## Dependencies

- R3 library (existing)
- R3Ext core (existing)
- Microsoft.Maui.Controls (Phase 2)
- Microsoft.AspNetCore.Components (Phase 3)
- Avalonia (Phase 4)

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| MAUI version compatibility | Medium | Test with multiple MAUI versions |
| Source generator complexity | High | Start with manual implementation, then generate |
| Breaking existing code | Low | Additive API, no breaking changes |
| Performance overhead | Medium | Benchmark early, optimize as needed |
