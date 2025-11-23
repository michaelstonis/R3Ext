# Phase 3: Performance, Virtualization & Polish

## Status

-   Phase 1: ✅ Complete (Core operators, Filter, Transform, Bind)
-   Phase 2: ✅ Complete (AddKey, Cast, Combine, TrueForAny/All, QueryWhenChanged)
-   Phase 3: 🚧 In Progress

## Objectives

1. **Sort + Bind Pipeline**: Stable sorting with incremental updates
2. **Virtualization**: Windowed cache views for large datasets
3. **Performance**: Benchmark and optimize hot paths
4. **Polish**: XML docs, reduce warnings, improve API ergonomics

## Priority Features

### 1. Sort + Bind (High Priority)

**Goal**: Provide stable, efficient sorting for cache and list observables with proper order maintenance during updates.

**Implementation Plan**:

-   [ ] Create `Sort<TObject, TKey>` operator for cache observables
    -   Accepts `IComparer<TObject>` or `Func<TObject, TComparable>`
    -   Maintains sorted order through Add/Update/Remove
    -   Uses stable sort algorithm
    -   Emits positional changes (index-aware)
-   [ ] Integrate with existing Bind operator for ordered binding
-   [ ] Add `SortedBind` convenience method
-   [ ] Support dynamic re-sorting via Observable<IComparer<T>>
-   [ ] Tests: order stability, update positioning, remove/add sequencing

**Performance Targets**:

-   O(log n) insertion/removal using binary search
-   < 5ms for 10,000 item re-sort
-   Minimal allocations (reuse buffers)

### 2. Virtualization Operators (High Priority)

**Goal**: Enable efficient rendering of large datasets by exposing only visible window.

**Implementation Plan**:

-   [ ] Create `Virtualize<TObject, TKey>` operator
    -   Accepts `IVirtualizationRequest` (StartIndex, Count)
    -   Emits windowed change sets
    -   Tracks full dataset internally, exposes slice
-   [ ] Add `VirtualRequest` record type
    -   StartIndex, Size properties
    -   Support for dynamic window changes
-   [ ] Create `Page<TObject, TKey>` operator for pagination
    -   PageSize, PageNumber parameters
    -   Observable<int> for page changes
-   [ ] Tests: window sliding, boundary conditions, out-of-range requests

**API Example**:

```csharp
var virtualRequest = new Subject<VirtualRequest>();
var windowed = cache.Connect()
    .Sort(p => p.Name)
    .Virtualize(virtualRequest.AsObservable())
    .Bind(out var visibleItems);

virtualRequest.OnNext(new VirtualRequest(0, 50)); // First 50
virtualRequest.OnNext(new VirtualRequest(50, 50)); // Next 50
```

### 3. Performance Optimization (Medium Priority)

**Benchmark Suite**:

-   [ ] Add R3Ext.Benchmarks project for Phase 2 operators:
    -   `AddKey_Throughput`: 10k list items → keyed cache
    -   `Combine_MergeSpeed`: 5 caches, 1k items each, Or/And/Except
    -   `TrueForAny_Latency`: 1k items, boolean aggregate recompute
    -   `QueryWhenChanged_Overhead`: snapshot emission frequency
    -   `ToCollection_Allocation`: memory profiling

**Optimization Targets**:

-   [ ] Reduce allocations in `CombineInternal` (pool change lists)
-   [ ] Cache recomputation optimization in TrueForAny/All (dirty flag)
-   [ ] AddKey batching (process ranges without intermediate lists)
-   [ ] ChangeSet capacity hints (pre-size when count known)

**Tools**:

-   BenchmarkDotNet for micro-benchmarks
-   dotMemory for allocation profiling
-   Visual Studio Profiler for hot path analysis

### 4. XML Documentation (Low Priority)

-   [ ] Document all Phase 2 public operators:
    -   AddKey, Cast, ToObservableOptional
    -   Combine (And/Or/Except/Xor)
    -   TrueForAny, TrueForAll
    -   QueryWhenChanged, ToCollection, IQuery
-   [ ] Add usage examples to XML comments
-   [ ] Document performance characteristics (Big-O)
-   [ ] Suppress SA1591 warnings for internal types

### 5. Additional Enhancements

-   [ ] **Throttle/Debounce for ChangeSet**: Rate-limit change emissions
-   [ ] **Batch operator**: Buffer changes, emit in chunks
-   [ ] **AutoRefresh improvements**: Reduce timer overhead
-   [ ] **Sorted Group support**: GroupBy with per-group sorting
-   [ ] **Distinct with projection**: DistinctValues with key selector + equality

## Success Metrics

-   All Phase 2 tests passing (✅ 58/58)
-   Benchmark suite established with baseline numbers
-   Sort operator functional with tests (10+ test cases)
-   Virtualize operator functional with sample app integration
-   < 100 build warnings (down from 210+)
-   XML doc coverage > 80% for public APIs

## Timeline

-   Week 1: Sort operator + tests + benchmarks
-   Week 2: Virtualize operator + pagination + sample integration
-   Week 3: Performance optimization + profiling
-   Week 4: Documentation + polish + final review

## Out of Scope (Future Phases)

-   WhenValueChanged (property change tracking)
-   Connect() overloads with scheduler support
-   Multi-threaded change processing
-   Persistent cache (disk-backed)
-   Time-based expiration operators
