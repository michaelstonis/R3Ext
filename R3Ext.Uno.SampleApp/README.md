# R3Ext.Uno.SampleApp

This is a sample application demonstrating R3Ext.Uno features for Uno Platform.

> **Note**: This sample app is **not included in the solution** due to SDK compatibility issues on macOS with Xcode 16+. See the [Known Issues](#known-issues) section for details.

## Requirements

This sample app requires the **Uno.Sdk** workload to be installed for proper multi-platform builds.

### Installing Uno.Sdk

1. Install the Uno Platform templates:
   ```bash
   dotnet new install Uno.Templates
   ```

2. Install the uno-check tool:
   ```bash
   dotnet tool install -g uno.check
   ```

3. Run uno-check to verify your environment:
   ```bash
   uno-check
   ```

For detailed setup instructions, see: https://platform.uno/docs/articles/get-started.html

## Known Issues

### macOS SDK Version Mismatch

On macOS with **Xcode 16+** (macOS Sequoia / SDK 26.0), the Uno.WinUI NuGet package encounters a TFM mismatch:

- **Problem**: Xcode 16+ defaults to `net9.0-maccatalyst26.0`, but Uno.WinUI 6.x only provides build imports for `net9.0-maccatalyst18.0`.
- **Effect**: The Uno XAML source generators do not run, causing `InitializeComponent()` and `x:Name` controls to be missing.
- **Root Cause**: NuGet import conditions check `$(TargetFramework) == 'net9.0-maccatalyst'` but the actual TFM resolves to `net9.0-maccatalyst26.0`.

### Workarounds

1. **Use Uno.Sdk** (Recommended)
   Change the project SDK from `Microsoft.NET.Sdk` to `Uno.Sdk/6.4.195`:
   ```xml
   <Project Sdk="Uno.Sdk/6.4.195">
   ```
   This requires the Uno workload to be installed via `uno-check`.

2. **Use Windows only**
   On Windows, the `net9.0-windows10.0.19041` TFM works correctly.

3. **Use Skia Desktop**
   The Skia Desktop target (`net9.0-desktop`) provides cross-platform support but requires `Uno.Sdk`.

## Supported Platforms

| Platform | Target Framework | Requirements |
|----------|------------------|--------------|
| Windows | `net9.0-windows10.0.19041` | Windows 10/11 |
| Android | `net9.0-android` | Android SDK |
| iOS | `net9.0-ios` | macOS with Xcode |
| Mac Catalyst | `net9.0-maccatalyst18.0` | macOS (requires Uno.Sdk) |

## Building

Once the Uno.Sdk workload is installed and the project SDK is changed:

```bash
# Build for Mac Catalyst
dotnet build -f net9.0-maccatalyst

# Build for Android
dotnet build -f net9.0-android

# Build for iOS (macOS only)
dotnet build -f net9.0-ios

# Build for Windows (Windows only)
dotnet build -f net9.0-windows10.0.19041
```

## Features Demonstrated

- **Activation Lifecycle**: Demonstrates `GetActivation()`, `GetLoadedActivation()`, and related methods
- **Timer Demo**: Shows reactive timer patterns with R3Ext bindings
- **Platform-Specific Entry Points**: Shows proper Uno Platform multi-head architecture

## Project Structure

```
R3Ext.Uno.SampleApp/
├── App.xaml(.cs)           # Application entry point
├── Views/
│   ├── MainWindow.xaml     # Main navigation window
│   ├── ActivationDemoPage.xaml   # Activation lifecycle demo
│   └── TimerDemoPage.xaml  # Timer/binding demo
└── Platforms/
    ├── Android/            # Android entry points
    ├── iOS/                # iOS entry points
    ├── MacCatalyst/        # Mac Catalyst entry points
    └── Windows/            # Windows entry points
```

## Using R3Ext.Uno Library

The sample demonstrates the R3Ext.Uno library which provides:

```csharp
// Get activation lifecycle for any element
var activation = myElement.GetActivation();
activation.Subscribe(_ => Console.WriteLine("Activated!"));

// Loaded-based activation
var loadedActivation = myElement.GetLoadedActivation();

// Disposed when element is unloaded
```

The R3Ext.Uno library (`net9.0`) compiles and tests pass on all platforms. Only the sample app requires the Uno.Sdk workload.
