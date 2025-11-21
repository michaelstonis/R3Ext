# Phase 4: Advanced Reactive Features

## Status
- Phase 1: ✅ Complete (Core operators, Filter, Transform, Bind)
- Phase 2: ✅ Complete (AddKey, Cast, Combine, TrueForAny/All, QueryWhenChanged)
- Phase 3: ✅ Complete (Sort, Virtualize, Benchmarks)
- Phase 4: 🚧 In Progress

## Objectives
1. **Property Change Tracking**: WhenValueChanged for reactive property monitoring
2. **Advanced Change Control**: Throttle, Debounce, Batch operators for change sets
3. **Time-based Operations**: Expiration, scheduling, time-windowing
4. **Threading & Concurrency**: Scheduler support, thread-safe operations
5. **Sample App Enhancements**: Comprehensive demos of all features

## Priority Features

### 1. WhenValueChanged (High Priority)
**Goal**: Enable reactive property change tracking on cached objects

**Implementation Plan**:
- [ ] Create `WhenValueChanged<TObject, TKey, TProperty>` operator
  - Expression-based property selector: `cache.Connect().WhenValueChanged(x => x.Name)`
  - Emits `PropertyValue<TObject, TProperty>` with old/new values
  - Integrates with INotifyPropertyChanged
  - Supports nested property paths (e.g., `x => x.Address.City`)
- [ ] Add `WhenPropertyChanged<TObject, TKey>` for generic property changes
  - Listens to any property change on objects
  - Returns `PropertyChanged<TObject>` with property name
- [ ] Tests: single property, multiple properties, nested paths, null handling

**API Example**:
```csharp
var nameChanges = cache.Connect()
    .WhenValueChanged(person => person.Name)
    .Subscribe(change => 
        Console.WriteLine($"{change.Sender.Name}: {change.Previous} → {change.Current}"));
```

### 2. Change Set Flow Control (High Priority)
**Goal**: Control the rate and batching of change set emissions

**Implementation Plan**:
- [ ] `Throttle<TObject, TKey>(TimeSpan)` - Emit at most once per interval
  - Discards intermediate changes
  - Useful for high-frequency updates
- [ ] `Debounce<TObject, TKey>(TimeSpan)` - Emit after silence period
  - Waits for quiet period before emitting
  - Useful for search-as-you-type scenarios
- [ ] `Batch<TObject, TKey>(TimeSpan, int?)` - Buffer changes
  - Accumulates changes, emits merged changeset
  - Optional max batch size
- [ ] `Sample<TObject, TKey>(TimeSpan)` - Periodic sampling
  - Emits most recent state at fixed intervals

**API Example**:
```csharp
// Throttle: Limit to 1 update per 500ms
cache.Connect()
    .Throttle(TimeSpan.FromMilliseconds(500))
    .Bind(out var items);

// Batch: Accumulate changes for 100ms
cache.Connect()
    .Batch(TimeSpan.FromMilliseconds(100), maxBatchSize: 50)
    .Subscribe(batch => ProcessChanges(batch));
```

### 3. Time-based Operations (Medium Priority)
**Goal**: Add temporal operators for cache management

**Implementation Plan**:
- [ ] `ExpireAfter<TObject, TKey>(Func<TObject, TimeSpan>)` - Auto-removal
  - Removes items after specified duration
  - Per-item expiration policy
  - Useful for caching scenarios
- [ ] `LimitSizeTo<TObject, TKey>(int, IComparer<TObject>?)` - Size constraints
  - Maintains max cache size
  - Optional eviction policy (LRU, custom comparer)
- [ ] `SkipInitial<TObject, TKey>()` - Skip initial load
  - Ignores first emission
  - Useful for update-only scenarios

**API Example**:
```csharp
// Expire items 5 minutes after creation
cache.Connect()
    .ExpireAfter(item => TimeSpan.FromMinutes(5))
    .Subscribe();

// Maintain max 1000 items, remove oldest
cache.Connect()
    .LimitSizeTo(1000, Comparer<Item>.Create((a, b) => a.Created.CompareTo(b.Created)))
    .Subscribe();
```

### 4. Threading & Scheduler Support (Medium Priority)
**Goal**: Add thread-safe operations and scheduler integration

**Implementation Plan**:
- [ ] `ObserveOn<TObject, TKey>(TimeProvider)` - Change thread context
  - Marshal change sets to specific thread
  - Integration with R3's TimeProvider
- [ ] `SubscribeOn<TObject, TKey>(TimeProvider)` - Subscribe on scheduler
  - Control subscription thread
- [ ] Thread-safe cache operations
  - Review and ensure `SourceCache` thread safety
  - Add locks where needed for concurrent access
- [ ] Tests: concurrent reads/writes, cross-thread notifications

**API Example**:
```csharp
// Observe changes on UI thread (MAUI/WPF)
cache.Connect()
    .ObserveOn(SynchronizationContext.Current)
    .Bind(out var items);
```

### 5. Sample App Enhancements (Low Priority)
**Goal**: Comprehensive demonstration of all R3.DynamicData features

**Implementation Plan**:
- [ ] Add WhenValueChanged demo page
  - Edit person properties, see reactive updates
  - Property change history display
- [ ] Add Throttle/Debounce demo page
  - Visual indicator of emission rate
  - Comparison of different timing strategies
- [ ] Add Expiration demo page
  - Cache with TTL, countdown timers
  - Auto-removal visualization
- [ ] Add Threading demo page
  - Background data loading
  - Cross-thread updates
- [ ] Enhance existing Virtualization demo
  - Add to DynamicDataOperatorsPage.xaml
  - Pagination controls
  - Window size adjustment

### 6. Additional Quality of Life Features
- [ ] `Clone<TObject, TKey>()` - Deep copy cache
- [ ] `IgnoreUpdateWhen<TObject, TKey>(Func<TObject, TObject, bool>)` - Conditional updates
- [ ] `TransformSafe<TObject, TKey, TResult>()` - Transform with error handling
- [ ] `MergeChangeSets<TObject, TKey>()` - Combine multiple change set streams
- [ ] `Replay<TObject, TKey>(int)` - Replay last N changes

## Success Metrics
- WhenValueChanged functional with INotifyPropertyChanged integration
- Throttle/Debounce/Batch operators implemented with tests
- ExpireAfter working in sample scenarios
- Threading support verified with concurrent access tests
- Sample app has demos for all Phase 4 features
- All tests passing (target: 100+ tests total)
- Performance maintained or improved

## Timeline
- Week 1: WhenValueChanged + property tracking tests
- Week 2: Throttle/Debounce/Batch + timing tests
- Week 3: ExpireAfter/LimitSizeTo + scheduler support
- Week 4: Sample app enhancements + final polish

## Out of Scope (Future Phases)
- Persistent cache (disk-backed, SQLite integration)
- Distributed cache (Redis, shared memory)
- Query language (LINQ-to-ChangeSet)
- Custom operators SDK
- Performance profiling dashboard
- Integration with other reactive frameworks (Rx.NET compatibility)

## Notes
- Focus on most-requested features first (WhenValueChanged, Throttle)
- Maintain API consistency with Phase 1-3
- Ensure backward compatibility
- Continue comprehensive testing approach
