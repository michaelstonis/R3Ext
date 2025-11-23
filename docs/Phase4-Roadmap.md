# Phase 4: Advanced Operators & Async Support

## Status

-   Phase 1: ✅ Complete (Core operators, Filter, Transform, Bind)
-   Phase 2: ✅ Complete (AddKey, Cast, Combine, TrueForAny/All, QueryWhenChanged)
-   Phase 3: ✅ Complete (Sort, Virtualize, Benchmarks)
-   Phase 4: 🚧 In Progress

## Objectives

1. **Dynamic Filtering**: FilterOnObservable with per-item reactive predicates
2. **Async Transforms**: TransformAsync with task/cancel overloads
3. **ChangeSet Merging**: MergeChangeSets for union of multiple streams
4. **Size Management**: LimitSizeTo for hard size caps on live lists
5. **Time-based Eviction**: ExpireAfter for list-level time-based removal
6. **Advanced Async**: TransformAsyncMany and variants

## Priority Features

### 1. FilterOnObservable (High Priority)

**Goal**: Dynamic filtering based on per-item observable predicates

**Implementation Plan**:

-   [ ] Create `FilterOnObservable<T>` for list observables
    -   Accepts `Func<T, Observable<bool>>` per-item predicate factory
    -   Subscribes to each item's boolean observable
    -   Dynamically includes/excludes based on emissions
    -   Handles add/remove of items with proper subscription management
-   [ ] Support buffer/throttle variants if needed
-   [ ] Tests: dynamic inclusion/exclusion, item lifecycle, multiple predicates

**API Example**:

```csharp
// Filter items based on their IsActive property changes
list.Connect()
    .FilterOnObservable(item => item.IsActiveChanged)
    .Bind(out var activeItems);
```

### 2. TransformAsync (High Priority)

**Goal**: Asynchronous transformation with task/cancellation support

**Implementation Plan**:

-   [ ] `TransformAsync<TSource, TDestination>` for list observables
    -   Accepts `Func<TSource, Task<TDestination>>` or `Func<TSource, CancellationToken, Task<TDestination>>`
    -   Preserves ordering during async operations
    -   Supports replace semantics on completion
    -   Handles cancellation when items are removed
-   [ ] `TransformAsync<TSource, TKey, TDestination>` for cache observables
-   [ ] Parallel vs sequential execution options
-   [ ] Tests: ordering, cancellation, error handling, concurrent transforms

**API Example**:

```csharp
// Transform with async API call
cache.Connect()
    .TransformAsync(async (item, ct) =>
    {
        var enriched = await api.EnrichAsync(item, ct);
        return enriched;
    })
    .Bind(out var enrichedItems);
```

### 3. MergeChangeSets (High Priority)

**Goal**: Union/merge multiple IChangeSet streams without logical semantics

**Implementation Plan**:

-   [ ] `MergeChangeSets<T>` for list observables
    -   Merges multiple `Observable<IChangeSet<T>>` streams
    -   Emits union of all changes (distinct membership)
    -   Different from And/Or/Except/Xor logical operators
    -   Straight union diff emission behavior
-   [ ] `MergeChangeSets<TObject, TKey>` for cache observables
    -   Keyed variant for cache merging
-   [ ] Optional ordering/priority support
-   [ ] Tests: multiple sources, overlapping changes, dynamic sources

**API Example**:

```csharp
// Merge multiple change streams
var merged = Observable.MergeChangeSets(
    source1.Connect(),
    source2.Connect(),
    source3.Connect()
);
```

### 4. LimitSizeTo (Medium Priority)

**Goal**: Hard size cap on live lists with automatic trimming

**Implementation Plan**:

-   [ ] `LimitSizeTo<T>(int)` for list observables
    -   Maintains max list size
    -   Trims excess items on additions
    -   Different from Top (virtual window)
    -   Different from ToObservableChangeSet limitSizeTo (applies to live streams)
-   [ ] Optional eviction strategy (FIFO, LIFO, custom comparer)
-   [ ] Efficient trimming (removes from end by default)
-   [ ] Tests: overflow handling, eviction strategies, edge cases

**API Example**:

```csharp
// Keep only most recent 100 items
list.Connect()
    .LimitSizeTo(100)
    .Bind(out var recentItems);

// Custom eviction (remove oldest by timestamp)
list.Connect()
    .LimitSizeTo(100, Comparer<Item>.Create((a, b) => a.Timestamp.CompareTo(b.Timestamp)))
    .Bind(out var items);
```

### 5. ExpireAfter (Medium Priority)

**Goal**: Time-based eviction for items in live list streams

**Implementation Plan**:

-   [ ] `ExpireAfter<T>(Func<T, TimeSpan>)` for list observables
    -   Time-based eviction for items already in stream
    -   Different from ToObservableChangeSet expiry (applies to existing list items)
    -   Per-item timeout policy
    -   Automatic removal after duration
-   [ ] Uses R3's TimeProvider for testability
-   [ ] Efficient timer management (batch expirations)
-   [ ] Tests: expiration timing, cancellation on removal, multiple items

**API Example**:

```csharp
// Remove items 5 minutes after they're added
list.Connect()
    .ExpireAfter(item => TimeSpan.FromMinutes(5))
    .Bind(out var items);

// Variable expiration based on item type
list.Connect()
    .ExpireAfter(item => item.IsHighPriority
        ? TimeSpan.FromHours(1)
        : TimeSpan.FromMinutes(10))
    .Bind(out var items);
```

### 6. TransformAsyncMany (Low Priority)

**Goal**: Async many-to-many transformation with advanced options

**Implementation Plan**:

-   [ ] `TransformAsyncMany<TSource, TDestination>` for list observables
    -   Async selector returning `Task<IEnumerable<TDestination>>`
    -   Parallel vs sequential execution options
    -   Flattens results like TransformMany
    -   Cancellation support
-   [ ] Tests: ordering, concurrency, cancellation, error handling

**API Example**:

```csharp
// Async fetch related items
list.Connect()
    .TransformAsyncMany(async (person, ct) =>
        await api.GetFriendsAsync(person.Id, ct))
    .Bind(out var allFriends);
```

### 7. Reverse Optimization (Low Priority)

**Goal**: Optimize Reverse operator for incremental changes

**Current State**: Reverse emits Clear + AddRange diffs (simplified)
**Target**: Emit minimal incremental changes for efficiency

**Implementation Plan**:

-   [ ] Track reversed indices
-   [ ] Emit Add/Remove/Replace with correct reversed indices
    -   Add at index i → Add at (count - i - 1)
    -   Remove at index i → Remove at (count - i - 1)
-   [ ] Handle AddRange/RemoveRange efficiently
-   [ ] Tests: verify correctness of reversed indices

## Success Metrics

-   FilterOnObservable with dynamic predicate support
-   TransformAsync with proper cancellation and ordering
-   MergeChangeSets for union of multiple streams
-   LimitSizeTo with configurable eviction strategies
-   ExpireAfter for time-based list item removal
-   TransformAsyncMany with parallel/sequential options
-   Reverse optimization for incremental changes
-   All tests passing (target: 120+ tests total)
-   Performance maintained or improved

## Timeline

-   Week 1: FilterOnObservable + TransformAsync
-   Week 2: MergeChangeSets + LimitSizeTo
-   Week 3: ExpireAfter + TransformAsyncMany
-   Week 4: Reverse optimization + final polish

## Implementation Notes

-   ✅ WhenValueChanged completed (9/9 tests) - bonus feature from previous plan
-   All operators should integrate with R3's Observable<T> and TimeProvider
-   Focus on list operators first (more commonly used)
-   Cache variants can follow list implementations
-   Maintain backward compatibility with existing operators

## Out of Scope (Future Phases)

-   Persistent cache (disk-backed, SQLite integration)
-   Distributed cache (Redis, shared memory)
-   Query language (LINQ-to-ChangeSet)
-   Custom operators SDK
-   Performance profiling dashboard
-   Integration with other reactive frameworks (Rx.NET compatibility)
-   ObserveOn/SubscribeOn threading operators (R3 handles this differently)

## Notes

-   Focus on most-requested features first (WhenValueChanged, Throttle)
-   Maintain API consistency with Phase 1-3
-   Ensure backward compatibility
-   Continue comprehensive testing approach
