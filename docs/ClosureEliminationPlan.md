# Closure Elimination Plan for R3.DynamicData

## Overview
This document outlines the strategy to eliminate closures in the R3.DynamicData codebase by leveraging R3's closure-free overloads: `Observable.Create<T, TState>`, `Observable.Select<T, TResult, TState>`, and `Observable.Subscribe<T, TState>`.

## R3 Closure-Free Patterns

### Pattern 1: Observable.Create with State
```csharp
// Before (with closure):
Observable.Create<T>(observer =>
{
    var localState = capturedVariable;
    return source.Subscribe(x => observer.OnNext(Process(x, localState)));
});

// After (closure-free):
Observable.Create<T, TState>(
    state: capturedVariable,
    subscribe: static (observer, state) =>
    {
        return source.Subscribe((observer, state), static (x, tuple) => 
            tuple.observer.OnNext(Process(x, tuple.state)));
    });
```

### Pattern 2: Select with State
```csharp
// Before (with closure):
source.Select(x => Transform(x, capturedVariable))

// After (closure-free):
source.Select(capturedVariable, static (x, state) => Transform(x, state))
```

### Pattern 3: Subscribe with State
```csharp
// Before (with closure):
source.Subscribe(x => DoSomething(x, capturedVariable))

// After (closure-free):
source.Subscribe(capturedVariable, static (x, state) => DoSomething(x, state))
```

## Implementation Phases

### Phase 1: High-Priority Core Operators (Week 1)
**Target**: Simple closures in frequently-used operators

#### 1.1 FilterOnObservable.cs
- **Line 26-127**: Subscribe with 4-variable closure (trackedItems, includedItems, predicateSelector, observer)
- **Line 177**: Nested Subscribe (tracked, item, includedItems, observer)
- **Impact**: Core filtering operation used heavily
- **State Struct**:
```csharp
readonly struct FilterOnObservableState
{
    public readonly Dictionary<T, TrackedItem> TrackedItems;
    public readonly List<T> IncludedItems;
    public readonly Func<T, Observable<bool>> PredicateSelector;
    public readonly Observer<IChangeSet<T>> Observer;
}
```

#### 1.2 ObservableListEx.Virtualize.cs
- **Line 29-52**: Subscribe capturing window state
- **Line 56-70**: Subscribe capturing request state
- **Line 245**: Select with pageSize closure
- **Impact**: Virtualization is performance-critical for large lists
- **State Struct**:
```csharp
readonly struct VirtualizeState<T>
{
    public readonly List<T> FullList;
    public readonly IVirtualRequest PreviousWindow;
    public readonly IVirtualRequest CurrentWindow;
    public readonly Observer<IChangeSet<T>> Observer;
}
```

#### 1.3 Internal/Filter.cs
- **Line 29-45**: Subscribe with 3-variable closure (slots, filtered, observer)
- **Impact**: Core filtering, very common operation
- **State Struct**:
```csharp
readonly struct FilterState<T>
{
    public readonly List<FilterSlot<T>> Slots;
    public readonly ChangeAwareList<T> Filtered;
    public readonly Observer<IChangeSet<T>> Observer;
}
```

#### 1.4 Internal/Transform.cs
- **Line 21-39**: Subscribe with 2-variable closure (list, observer)
- **Impact**: Transform is one of the most common operations
- **State Struct**:
```csharp
readonly struct TransformState<TSource, TDestination>
{
    public readonly ChangeAwareList<TDestination> List;
    public readonly Observer<IChangeSet<TDestination>> Observer;
}
```

#### 1.5 Internal/RemoveIndex.cs
- **Line 18-80**: Subscribe with single observer closure
- **Impact**: Index stripping operation
- **Simple**: Single-variable state

#### 1.6 Internal/DisposeMany.cs
- **Line 22-37**: Subscribe with 2-variable closure (current, observer)
- **Impact**: Automatic disposal tracking
- **State Struct**:
```csharp
readonly struct DisposeManyState<T>
{
    public readonly List<T> Current;
    public readonly Observer<IChangeSet<T>> Observer;
}
```

### Phase 2: Medium-Priority Aggregate Operators (Week 2)
**Target**: Statistical and aggregate operations

#### 2.1 ObservableListAggregates.cs - Count
- **Line 16-48**: Subscribe with ref int count
- **Solution**: Use `RefInt` wrapper struct
```csharp
sealed class RefInt
{
    public int Value;
}
```

#### 2.2 ObservableListAggregates.cs - Max
- **Line 209-295**: Subscribe with complex state (itemValues, valueCounts, hasValue, currentMax)
- **State Struct**:
```csharp
readonly struct MaxAggregateState<TSource, TProperty>
{
    public readonly Dictionary<TSource, TProperty> ItemValues;
    public readonly Dictionary<TProperty, int> ValueCounts;
    public readonly RefBool HasValue;
    public readonly RefValue<TProperty> CurrentMax;
    public readonly Func<TSource, TProperty> Selector;
    public readonly Observer<TProperty> Observer;
}
```

#### 2.3 ObservableListAggregates.cs - Min
- **Line 388-478**: Similar to Max pattern
- **Reuse**: Same state struct pattern as Max

#### 2.4 ObservableListAggregates.cs - Avg
- **Line 515-618**: Subscribe with sum/count state
- **State Struct**:
```csharp
readonly struct AvgAggregateState<TSource, TProperty>
{
    public readonly Dictionary<TSource, TProperty> ItemValues;
    public readonly RefValue<TProperty> Sum;
    public readonly RefInt Count;
    public readonly Observer<double> Observer;
}
```

#### 2.5 ObservableListAggregates.cs - StdDev
- **Line 627-688**: Subscribe with sum/sumSquares/count state
- **State Struct**: Similar to Avg with additional RefValue<double> SumSquares

### Phase 3: Medium-Priority Internal Operators (Week 3)
**Target**: Complex internal operators

#### 3.1 Internal/SubscribeMany.cs (List)
- **Line 29-36**: SubscribeMany with counter/selector/gate
- **Thread Safety**: Needs lock coordination
- **State Struct**:
```csharp
readonly struct SubscribeManyState<T, TDestination>
{
    public readonly RefInt Counter;
    public readonly Func<T, Observable<TDestination>> Selector;
    public readonly object Gate;
    public readonly Observer<TDestination> Observer;
}
```

#### 3.2 Internal/Combiner.cs
- **Line 40-48**: Subscribe with sourceLists/observer
- **Loop Index**: Careful with captured loop variables
- **State Struct**: Array of source lists + observer

#### 3.3 Internal/BufferIf.cs
- **Line 27-38**: Pause Subscribe
- **Line 40-55**: Resume Subscribe
- **Line 36**: Timer Subscribe
- **Line 58-67**: Update Subscribe
- **Complexity**: Multiple mutable state variables
- **State Struct**:
```csharp
sealed class BufferIfState<T>
{
    public bool Paused;
    public ChangeSet<T> Buffer;
    public readonly SerialDisposable TimeoutSubscriber;
    public readonly Subject<bool> TimeoutSubject;
    public readonly Observer<IChangeSet<T>> Observer;
}
```

#### 3.4 Internal/TransformMany.cs
- **Line 35-52**: Subscribe with parent/child tracking
- **State Struct**:
```csharp
readonly struct TransformManyState<TSource, TDestination>
{
    public readonly List<TransformManyParent<TSource, TDestination>> Parents;
    public readonly ChangeAwareList<TDestination> Result;
    public readonly Observer<IChangeSet<TDestination>> Observer;
}
```

#### 3.5 Internal/DistinctValues.cs
- **Line 26-105**: Subscribe with distinct/counts tracking
- **State Struct**:
```csharp
readonly struct DistinctValuesState<T, TValue>
{
    public readonly ChangeAwareList<TValue> Distinct;
    public readonly Dictionary<TValue, int> Counts;
    public readonly Func<T, TValue> Selector;
    public readonly IEqualityComparer<TValue> Comparer;
    public readonly Observer<IChangeSet<TValue>> Observer;
}
```

### Phase 4: Async Operations (Week 4)
**Target**: Async/await patterns

#### 4.1 ObservableListEx.TransformAsync.cs
- **Line 46-144**: Subscribe with async transformation state
- **Complexity**: Involves Task management, CancellationToken
- **Consideration**: May need different approach due to async
- **State Struct**:
```csharp
sealed class TransformAsyncState<TSource, TDestination>
{
    public readonly Func<TSource, CancellationToken, Task<TDestination>> TransformFactory;
    public readonly Dictionary<TSource, (Task<TDestination> Task, CancellationTokenSource Cts)> Transformations;
    public readonly List<(TSource Source, TDestination Destination)> CompletedItems;
    public readonly Observer<IChangeSet<TDestination>> Observer;
    public readonly object Gate;
}
```

### Phase 5: Simple Select/Where Transformations (Week 5)
**Target**: Low-hanging fruit transformations

#### 5.1 Simple Select closures
- **BufferIf.cs Line 36**: Timer Subscribe with single variable
- **Virtualize.cs Line 245**: Select with pageSize
- **ToObservableChangeSet.cs Line 27**: Select with buffer

#### 5.2 Bind operations
- **ObservableListEx.cs Line 75**: Subscribe with target IList
- **Impact**: Data binding is frequently used

### Phase 6: Cache Operators (Week 6)
**Target**: Similar patterns in Cache subdirectory

#### 6.1 Cache/Internal/SubscribeMany.cs
- **Line 24-115**: Subscribe with subscriptions dictionary
- **Thread Safety**: Lock coordination required
- **Pattern**: Similar to List SubscribeMany

#### 6.2 Cache/ObservableCacheEx.Phase2.cs
- **TrueForAny Line 433+**: Subscribe with item states
- **TrueForAll Line 479+**: Subscribe with item states
- **Complex**: Nested subscriptions and recomputation logic

## Implementation Strategy

### Code Structure
1. **Create Utility Types** (in R3.DynamicData/Utilities/):
   - `RefInt.cs`: Mutable int wrapper
   - `RefBool.cs`: Mutable bool wrapper
   - `RefValue<T>.cs`: Generic mutable value wrapper
   - State struct definitions for each operator

2. **Conversion Pattern**:
   ```csharp
   // Step 1: Define state struct
   readonly struct OperatorState<T>
   {
       public readonly Type1 Field1;
       public readonly Type2 Field2;
       
       public OperatorState(Type1 field1, Type2 field2)
       {
           Field1 = field1;
           Field2 = field2;
       }
   }
   
   // Step 2: Create state instance
   var state = new OperatorState<T>(var1, var2);
   
   // Step 3: Use closure-free overload
   return Observable.Create<T, OperatorState<T>>(
       state,
       static (observer, state) =>
       {
           // Use state.Field1, state.Field2 instead of captured variables
           return source.Subscribe(
               (observer, state),
               static (value, tuple) =>
               {
                   // Process using tuple.state.Field1, etc.
               });
       });
   ```

### Testing Strategy
1. **Existing Tests**: All existing tests must continue to pass
2. **Performance Tests**: Add benchmark comparisons
3. **Allocation Tests**: Verify reduction in allocations
4. **Memory Tests**: Confirm GC pressure reduction

### Commit Strategy
- **One file per commit**: Each operator file gets its own commit
- **Commit Message Format**:
  ```
  perf(DynamicData): eliminate closures in [OperatorName]
  
  - Convert Subscribe/Select to closure-free overloads
  - Add [StateName] struct for state management
  - Reduces allocations in [scenario]
  
  Addresses #[issue-number]
  ```

### Git Workflow
```bash
# After each file conversion:
git add [file]
git commit -m "perf(DynamicData): eliminate closures in [operator]"

# Run tests
dotnet test R3Ext.sln

# If tests pass, continue to next file
# If tests fail, fix and amend commit
```

## Performance Expectations

### Allocation Reduction
- **Closures**: Each closure creates a display class instance
- **Expected Savings**: 1 allocation per Subscribe call
- **High-Traffic Operators**: Filter, Transform, Select - potentially thousands of allocations eliminated

### GC Pressure
- **Current**: Closure objects create Gen0 garbage
- **After**: State structs are stack-allocated (if small) or long-lived
- **Benefit**: Reduced GC pauses, especially in Gen0

### Benchmarks to Add
1. **Filter Operations**: 10,000 items with frequent predicate changes
2. **Transform Operations**: 10,000 items with transformations
3. **Virtualization**: Scrolling through 100,000 items
4. **Aggregates**: Real-time sum/count on streaming data

## Risk Mitigation

### Potential Issues
1. **Increased Complexity**: State structs add boilerplate
   - **Mitigation**: Document patterns clearly
   
2. **Static Method Debugging**: Harder to step through
   - **Mitigation**: Add XML docs and inline comments
   
3. **State Struct Size**: Large structs may hurt performance
   - **Mitigation**: Measure and use classes if structs exceed 16 bytes
   
4. **Breaking Changes**: None - all changes are internal
   - **Benefit**: Public API remains unchanged

## Success Criteria
1. ✅ All existing tests pass
2. ✅ No public API changes
3. ✅ Measurable allocation reduction (>50% in hot paths)
4. ✅ No performance regression
5. ✅ Code maintains readability
6. ✅ Documentation updated

## Timeline
- **Week 1**: Phase 1 - Core operators (6 files)
- **Week 2**: Phase 2 - Aggregates (5 operators in 1 file)
- **Week 3**: Phase 3 - Internal operators (5 files)
- **Week 4**: Phase 4 - Async operations (1 file)
- **Week 5**: Phase 5 - Simple transformations (3 files)
- **Week 6**: Phase 6 - Cache operators (2 files)
- **Total**: ~22 files, ~50+ closure elimination sites

## References
- R3 Documentation: Closure-free patterns
- R3Ext/CreationExtensions.cs: Example implementation
- R3Ext.Bindings.SourceGenerator: Uses similar patterns for bindings
