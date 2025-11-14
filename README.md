# R3Ext

A .NET 9 class library for extending the R3 reactive library, plus a sample .NET MAUI app to try out the extensions.

## Projects
- `R3Ext` (Class Library, `net9.0`): Adds helpers for R3. Currently includes `ObservableDebugExtensions.Log()` for quick stream logging.
- `R3Ext.SampleApp` (.NET MAUI): Minimal app wired with `UseR3()` to demonstrate R3 in a UI context. It has a simple ticker using R3 and the `Log()` extension.

## Prerequisites
- .NET SDK 9 (verify with `dotnet --version`)
- .NET MAUI workload (verify with `dotnet workload list` shows `maui`)
- macOS for building iOS/Mac Catalyst; Android SDK/Xcode as applicable

## Build
```bash
cd R3Ext
 dotnet build R3Ext.sln
```

## Run (quick options)
- Android emulator:
```bash
cd R3Ext/R3Ext.SampleApp
 dotnet build -t:Run -f net9.0-android
```
- iOS simulator (requires Xcode):
```bash
cd R3Ext/R3Ext.SampleApp
 dotnet build -t:Run -f net9.0-ios /p:_DeviceName=:v2:udid=auto
```
- Mac Catalyst:
```bash
cd R3Ext/R3Ext.SampleApp
 dotnet build -t:Run -f net9.0-maccatalyst
```

## Notes
- MAUI is configured with `UseR3()` via `R3Extensions.Maui`, so time/frame-based operators marshal correctly for UI updates.
- Example extension: `Observable.Log(tag)` wraps `Do` to print events to Debug output.