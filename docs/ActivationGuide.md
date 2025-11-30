# View and ViewModel Activation Guide

This guide explains the activation system in R3Ext, which manages the lifecycle of views and view models in reactive applications.

## Overview

R3Ext provides two distinct activation patterns that correspond to different lifecycle events:

| Method | Trigger | Use Case |
|--------|---------|----------|
| `WhenActivated` | Visibility (Appearing/Disappearing) | Start/stop work when page is visible |
| `WhenAttached` | Loaded/Unloaded | Setup/teardown when added to visual tree |

## Quick Start

### MAUI Setup

```csharp
// In MauiProgram.cs
public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    builder
        .UseMauiApp<App>()
        .UseR3Activation();  // Register R3Ext activation support
    
    // Register your ViewModels
    builder.Services.AddTransient<MyViewModel>();
    
    return builder.Build();
}
```

### Implementing IViewFor&lt;T&gt;

```csharp
public partial class MyPage : ContentPage, IViewFor<MyViewModel>
{
    public MyPage()
    {
        InitializeComponent();
        
        // Initialize the view-viewmodel infrastructure
        // This syncs ViewModel with BindingContext and sets up DI resolution
        this.InitializeViewFor();
    }
    
    // Implement the interface
    public MyViewModel? ViewModel { get; set; }
}
```

The source generator automatically provides:
- ViewModel ↔ BindingContext synchronization
- The `Activation` observable property
- The `InitializeViewFor()` helper method

---

## WhenActivated vs WhenAttached

### WhenActivated (Visibility-Based)

Use `WhenActivated` for work that should:
- Start when the page becomes **visible** to the user
- Stop when the page is **hidden** (navigated away, covered by modal, etc.)
- Restart when the page becomes visible again

**MAUI Events**: `Page.Appearing` / `Page.Disappearing`

```csharp
public partial class DashboardPage : ContentPage, IViewFor<DashboardViewModel>
{
    public DashboardPage()
    {
        InitializeComponent();
        this.InitializeViewFor();
        
        // This runs each time the page becomes visible
        this.WhenActivated((ref DisposableBag d) =>
        {
            // Start polling for updates
            Observable.Interval(TimeSpan.FromSeconds(30))
                .Subscribe(_ => ViewModel?.RefreshData())
                .AddTo(ref d);  // Automatically stopped when page disappears
            
            // Set up bindings that only matter when visible
            ViewModel?.StatusMessage
                .Subscribe(msg => statusLabel.Text = msg)
                .AddTo(ref d);
        });
    }
    
    public DashboardViewModel? ViewModel { get; set; }
}
```

### WhenAttached (Load-Based)

Use `WhenAttached` for work that should:
- Start when the view is **loaded** into the visual tree
- Stop when the view is **unloaded** (removed from parent, page disposed)
- Only run once per load cycle (not affected by visibility changes)

**MAUI Events**: `Loaded` / `Unloaded`

```csharp
public partial class VideoPlayerView : ContentView, IViewFor<VideoPlayerViewModel>
{
    public VideoPlayerView()
    {
        InitializeComponent();
        this.InitializeViewFor();
        
        // This runs when the view is added to the visual tree
        this.WhenAttached((ref DisposableBag d) =>
        {
            // Initialize hardware resources
            ViewModel?.InitializePlayer()
                .Subscribe()
                .AddTo(ref d);
            
            // Cleanup happens automatically when unloaded
            Disposable.Create(() => ViewModel?.ReleaseResources())
                .AddTo(ref d);
        });
    }
    
    public VideoPlayerViewModel? ViewModel { get; set; }
}
```

### Decision Matrix

| Scenario | Use |
|----------|-----|
| Real-time data polling | `WhenActivated` |
| Timer-based UI updates | `WhenActivated` |
| Network subscriptions (live feeds) | `WhenActivated` |
| Hardware resource initialization | `WhenAttached` |
| One-time data loading | `WhenAttached` |
| Event handler registration | `WhenAttached` |
| Animation setup | `WhenActivated` |
| Sensor monitoring | `WhenActivated` |

### Combining Both

You can use both patterns on the same view for different purposes:

```csharp
public partial class CameraPage : ContentPage, IViewFor<CameraViewModel>
{
    public CameraPage()
    {
        InitializeComponent();
        this.InitializeViewFor();
        
        // One-time setup when loaded
        this.WhenAttached((ref DisposableBag d) =>
        {
            ViewModel?.RequestCameraPermissions()
                .Subscribe()
                .AddTo(ref d);
        });
        
        // Start/stop camera feed based on visibility
        this.WhenActivated((ref DisposableBag d) =>
        {
            ViewModel?.StartPreview()
                .Subscribe()
                .AddTo(ref d);
                
            Disposable.Create(() => ViewModel?.StopPreview())
                .AddTo(ref d);
        });
    }
    
    public CameraViewModel? ViewModel { get; set; }
}
```

---

## ViewModel Activation

ViewModels can also participate in the activation lifecycle by implementing `IActivatableViewModel` and/or `IAttachableViewModel`.

### IActivatableViewModel (Visibility-Based)

```csharp
public class DashboardViewModel : IActivatableViewModel, IDisposable
{
    private DisposableBag _disposables;
    
    public ViewModelActivator Activator { get; } = new();
    
    public DashboardViewModel()
    {
        // This runs when the associated view becomes visible
        this.WhenActivated((ref DisposableBag d) =>
        {
            // Start background work
            Observable.Interval(TimeSpan.FromSeconds(5))
                .Subscribe(_ => RefreshStats())
                .AddTo(ref d);
        }).AddTo(ref _disposables);
    }
    
    public void Dispose() => _disposables.Dispose();
}
```

### IAttachableViewModel (Load-Based)

```csharp
public class MediaPlayerViewModel : IAttachableViewModel, IDisposable
{
    private DisposableBag _disposables;
    
    public ViewModelAttacher Attacher { get; } = new();
    
    public MediaPlayerViewModel()
    {
        // This runs when the associated view is loaded
        this.WhenAttached((ref DisposableBag d) =>
        {
            // Initialize media engine
            InitializeMediaEngine();
            
            Disposable.Create(() => DisposeMediaEngine())
                .AddTo(ref d);
        }).AddTo(ref _disposables);
    }
    
    public void Dispose() => _disposables.Dispose();
}
```

### Implementing Both

A ViewModel can implement both interfaces to respond to different lifecycle events:

```csharp
public class StreamingViewModel : IActivatableViewModel, IAttachableViewModel, IDisposable
{
    private DisposableBag _disposables;
    
    public ViewModelActivator Activator { get; } = new();
    public ViewModelAttacher Attacher { get; } = new();
    
    public StreamingViewModel()
    {
        // One-time connection setup
        this.WhenAttached((ref DisposableBag d) =>
        {
            ConnectToServer()
                .Subscribe()
                .AddTo(ref d);
        }).AddTo(ref _disposables);
        
        // Start/stop streaming based on visibility
        this.WhenActivated((ref DisposableBag d) =>
        {
            StartStreaming();
            Disposable.Create(() => PauseStreaming())
                .AddTo(ref d);
        }).AddTo(ref _disposables);
    }
    
    public void Dispose() => _disposables.Dispose();
}
```

### Auto-Activation

When `IViewFor<T>.AutoActivateViewModel` is `true` (the default), the view automatically:

1. **WhenActivated**: Activates the ViewModel's `Activator` when the view appears
2. **WhenAttached**: Attaches the ViewModel's `Attacher` when the view loads

This means you typically don't need to manually call `Activator.Activate()` or `Attacher.Attach()`.

To opt out:

```csharp
public partial class ManualPage : ContentPage, IViewFor<MyViewModel>
{
    // Override to disable auto-activation
    public bool AutoActivateViewModel => false;
    
    public MyViewModel? ViewModel { get; set; }
}
```

---

## Self-Managing Lifecycle

The activation subscriptions are **self-managing** - you don't need to track the returned `IDisposable`:

```csharp
public partial class SimplePage : ContentPage, IViewFor<SimpleViewModel>
{
    public SimplePage()
    {
        InitializeComponent();
        this.InitializeViewFor();
        
        // No need to store the return value!
        // Cleanup happens automatically when the element is removed
        this.WhenActivated((ref DisposableBag d) =>
        {
            // Your subscriptions here
        });
        
        this.WhenAttached((ref DisposableBag d) =>
        {
            // Your subscriptions here
        });
    }
    
    public SimpleViewModel? ViewModel { get; set; }
}
```

The cleanup logic:
- **WhenActivated**: Disposes when `Window` property becomes `null`
- **WhenAttached**: Disposes when `Unloaded` event fires

---

## Late-Bound ViewModels

The activation system supports ViewModels that are set after the view is created:

```csharp
public partial class DetailPage : ContentPage, IViewFor<DetailViewModel>
{
    public DetailPage()
    {
        InitializeComponent();
        this.InitializeViewFor();
        
        // WhenActivated will pick up the ViewModel when it's set
        this.WhenActivated((ref DisposableBag d) =>
        {
            // ViewModel might be null on first activation
            if (ViewModel is null) return;
            
            ViewModel.Title
                .Subscribe(t => titleLabel.Text = t)
                .AddTo(ref d);
        });
    }
    
    public DetailViewModel? ViewModel { get; set; }
}

// Later, when navigating:
var page = serviceProvider.GetRequiredService<DetailPage>();
page.ViewModel = new DetailViewModel(itemId);  // Set before or after Appearing
await Navigation.PushAsync(page);
```

---

## Dependency Injection Integration

### Registering ViewModels

```csharp
// In MauiProgram.cs
builder.Services.AddTransient<HomeViewModel>();
builder.Services.AddScoped<UserProfileViewModel>();
builder.Services.AddSingleton<SettingsViewModel>();
```

### Automatic Resolution

If `ViewModel` is null when `InitializeViewFor()` is called, it attempts to resolve from DI:

```csharp
public partial class HomePage : ContentPage, IViewFor<HomeViewModel>
{
    public HomePage()
    {
        InitializeComponent();
        
        // If ViewModel is null, InitializeViewFor will resolve from DI
        this.InitializeViewFor();
        
        // ViewModel is now set (if registered in DI)
    }
    
    public HomeViewModel? ViewModel { get; set; }
}
```

### Constructor Injection

The preferred pattern is explicit constructor injection:

```csharp
public partial class ProfilePage : ContentPage, IViewFor<ProfileViewModel>
{
    public ProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        this.InitializeViewFor();
    }
    
    public ProfileViewModel? ViewModel { get; set; }
}

// Registration
builder.Services.AddTransient<ProfilePage>();
builder.Services.AddTransient<ProfileViewModel>();
```

---

## Best Practices

### ✅ Do

- Use `WhenActivated` for visibility-sensitive work (polling, animations, sensors)
- Use `WhenAttached` for one-time setup and resource management
- Keep activation blocks focused and small
- Use `AddTo(ref d)` to register cleanup
- Implement `IDisposable` on ViewModels that use activation

### ❌ Don't

- Don't store the `IDisposable` from `WhenActivated`/`WhenAttached` unless needed for early disposal
- Don't put heavy initialization in `WhenActivated` (use `WhenAttached` instead)
- Don't forget to call `InitializeViewFor()` in your view constructor
- Don't create long-lived subscriptions outside of activation blocks

### Performance Tips

1. **Static lambdas**: Use `static` keyword when possible to avoid closures
2. **DisposableBag**: The `ref` parameter ensures efficient struct usage
3. **Avoid allocations**: The activation system uses pooled observers internally

```csharp
// Good - no closure allocation
this.WhenActivated(static (ref DisposableBag d) =>
{
    // Use d.AddTo() for cleanup
});
```

---

## Platform Event Mapping

| Platform | WhenActivated Trigger | WhenAttached Trigger |
|----------|----------------------|---------------------|
| MAUI Page | Appearing/Disappearing | Loaded/Unloaded |
| MAUI View | IsVisible changes | Loaded/Unloaded |
| Blazor | (coming soon) | OnAfterRender/Dispose |
| Avalonia | (coming soon) | AttachedToVisualTree/DetachedFromVisualTree |

---

## Troubleshooting

### Activation not firing

1. Ensure `UseR3Activation()` is called in `MauiProgram.cs`
2. Verify the view implements `IViewFor<TViewModel>`
3. Check that `InitializeViewFor()` was called

### ViewModel not activated

1. Confirm ViewModel implements `IActivatableViewModel` or `IAttachableViewModel`
2. Verify `AutoActivateViewModel` is `true` (default)
3. Ensure ViewModel is set before the view appears

### Memory leaks

1. Always use `AddTo(ref disposables)` inside activation blocks
2. Implement `IDisposable` on ViewModels
3. Don't create subscriptions outside of activation blocks
