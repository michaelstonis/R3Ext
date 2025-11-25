# WhenObserved Examples in Sample App

The sample app now includes comprehensive examples demonstrating the new `WhenObserved` operator.

## Files Added

### ViewModel: `WhenObservedViewModel.cs`
Location: `R3Ext.SampleApp/ViewModels/WhenObservedViewModel.cs`

The viewmodel demonstrates three key scenarios:

1. **Simple Observable Property**: Observes `CurrentDocument.IsSaved` (an `Observable<bool>`)
   - Shows how WhenObserved subscribes to an observable property
   - Displays live save status changes

2. **Data Stream Values**: Observes `CurrentStream.DataObservable` (an `Observable<int>`)
   - Demonstrates observing a data stream that emits integer values
   - Shows random value emissions

3. **Nested Observable Chain**: Observes `NestedContainer.Document.IsSaved`
   - Demonstrates WhenObserved with property chains
   - Automatically rewires when intermediate properties change

### Key Features Demonstrated

- **Automatic Subscription Switching**: When you switch to a new document or stream, WhenObserved automatically unsubscribes from the old observable and subscribes to the new one
- **Event Logging**: All operations are logged with timestamps to show the flow of events
- **Interactive Controls**: Buttons to trigger saves, edits, and object switching
- **Proper Disposal**: Uses `DisposableBag` to manage subscription lifecycle

### Test Helper Classes

The viewmodel includes three helper classes:

- **Document**: Has an `IsSaved` observable that can be triggered
- **DataStream**: Has a `DataObservable` that emits integer values
- **Container**: Wraps a Document to demonstrate nested chains

### Page: `WhenObservedPage.xaml` and `WhenObservedPage.xaml.cs`
Location: `R3Ext.SampleApp/Pages/WhenObservedPage.xaml[.cs]`

The XAML page provides:
- Visual display of all three observable scenarios
- Interactive buttons for each example
- Real-time event log showing all operations
- Key points section explaining WhenObserved behavior

## How to Use in Sample App

1. Build and run the sample app
2. Navigate to: **R3 Basics → WhenObserved**
3. Try the following interactions:

### Example 1: Document Save Status
- Click **Trigger Save** - observe status change
- Click **Trigger Edit** - observe unsaved status
- Click **Switch to New Document** - observe subscription switching to new document

### Example 2: Data Stream
- Click **Emit Random Value** - see new random values
- Click **Switch to New Stream** - observe subscription switching to new stream
- Click **Emit Random Value** again - values come from the new stream

### Example 3: Nested Chain
- Click **Switch Nested Document** - observe nested observable switching
- Watch the event log to see the sequence of operations

### Event Log
- Shows timestamped events for all operations
- Displays document switching, observable value changes, etc.
- Click **Clear** to reset the log

## Code Patterns

### Using WhenObserved

```csharp
// Observe a property that returns IObservable<T>
this.WhenObserved(x => x.CurrentDocument.IsSaved)
    .Subscribe(isSaved => ObservedDocumentStatus = $"Document is {(isSaved ? "saved" : "NOT saved")}")
    .AddTo(ref _disposables);

// Observe nested observable properties
this.WhenObserved(x => x.NestedContainer.Document.IsSaved)
    .Subscribe(isSaved => ObservedNestedValue = $"Nested document {(isSaved ? "saved" : "NOT saved")}")
    .AddTo(ref _disposables);
```

### Creating Observable Properties

```csharp
public sealed class Document : ObservableObject, IDisposable
{
    private readonly Subject<bool> _isSavedSubject = new();
    
    public Observable<bool> IsSaved => _isSavedSubject.AsObservable();
    
    public void TriggerSave(bool saved)
    {
        _isSavedSubject.OnNext(saved);
    }
    
    public void Dispose()
    {
        _isSavedSubject?.Dispose();
    }
}
```

## Comparison with WhenChanged

| Feature | WhenChanged | WhenObserved |
|---------|-------------|--------------|
| **Observes** | Property values | Properties returning IObservable<T> |
| **Use Case** | Track property changes | Subscribe to observable properties |
| **Switching** | Rewires on property change | Rewires on observable property change |
| **Output** | Observable\<TValue\> | Observable\<TReturn\> |
| **Similar To** | ReactiveUI WhenAnyValue | ReactiveUI WhenAnyObservable |

## Integration with AppShell

The WhenObserved page is registered in `AppShell.xaml` under the "R3 Basics" flyout menu, positioned right after "Deep Binding":

```xml
<ShellContent Title="WhenObserved"
              ContentTemplate="{DataTemplate local:WhenObservedPage}"
              Route="WhenObservedPage" />
```
