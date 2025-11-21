# Phase 2 Operator Benchmark Baseline

## Status
Benchmarks currently running (started with `dotnet run -c Release --filter "*Phase2*"`).

## Configuration
- **Framework**: .NET 9.0
- **BenchmarkDotNet**: 0.13.12
- **Job**: SimpleJob (3 warmup, 5 iterations)
- **Diagnostics**: MemoryDiagnoser enabled
- **Parameters**: ItemCount = 100, 1000, 10000

## Benchmark Suite

### 1. AddKey_ListToCache
Measures throughput of converting list changesets to keyed cache changesets.
- **Operation**: AddRange → AddKey conversion → Clear
- **Metric**: Time per operation, allocations

### 2. Combine_Or_TwoCaches  
Measures union merge performance of two pre-populated caches.
- **Operation**: Or operator with subsequent AddOrUpdate calls
- **Metric**: Time per operation, allocations

### 3. Combine_And_TwoCaches
Measures intersection performance with overlapping items.
- **Operation**: And operator with overlapping item additions
- **Metric**: Time per operation, allocations

### 4. TrueForAny_BooleanAggregate
Measures recomputation latency for boolean aggregate with 100 items.
- **Operation**: TrueForAny predicate evaluation after cache update
- **Metric**: Time per operation, allocations

### 5. TrueForAll_BooleanAggregate
Measures ALL predicate evaluation performance.
- **Operation**: TrueForAll with condition violation (negative value)
- **Metric**: Time per operation, allocations

### 6. QueryWhenChanged_Snapshots
Measures snapshot emission overhead with multiple operations.
- **Operation**: Multiple AddOrUpdate + Remove operations with snapshot capture
- **Metric**: Time per operation, allocations

### 7. ToCollection_MaterializeList
Measures collection materialization cost with batch operations.
- **Operation**: AddOrUpdate bulk + Remove 25% of items
- **Metric**: Time per operation, allocations

### 8. Cast_TypeConversion
Measures type projection overhead.
- **Operation**: Pre-populate cache + Cast<TestItem, int, string> + AddOrUpdate
- **Metric**: Time per operation, allocations

### 9. ToObservableOptional_SingleKeyTracking
Measures Optional<T> emission for single-key tracking.
- **Operation**: Pre-populate + track specific key + Remove + AddOrUpdate
- **Metric**: Time per operation, allocations

## Results
_Will be populated once benchmarks complete. Expected runtime: 5-10 minutes._

### Expected Output Format
```
| Method                              | ItemCount |      Mean |    Error |   StdDev | Allocated |
|------------------------------------ |---------- |----------:|---------:|---------:|----------:|
| AddKey_ListToCache                  | 100       |  XX.XX us | X.XX us  | X.XX us  |  XX.XX KB |
| AddKey_ListToCache                  | 1000      | XXX.XX us | X.XX us  | X.XX us  | XXX.XX KB |
| AddKey_ListToCache                  | 10000     | XXX.XX ms | X.XX ms  | X.XX ms  |   X.XX MB |
...
```

## Analysis Notes
Once results are available:
1. Identify allocations hotspots (target: < 10 KB for 100 items)
2. Compare relative performance across item counts (should scale linearly)
3. Flag any O(n²) behaviors (check 10x increase from 1k→10k)
4. Compare against DynamicData upstream benchmarks if available

## Next Steps
- [ ] Wait for benchmark completion
- [ ] Record results in this document
- [ ] Analyze allocation patterns
- [ ] Identify optimization candidates (CombineInternal, TrueForAny/All)
- [ ] Use results to guide Phase 3 performance work
