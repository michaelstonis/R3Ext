# Performance Optimization Checklist - R3Ext Bindings & Source Generator

**Branch**: `feature/performance-optimizations`  
**Started**: November 29, 2025  
**Status**: ✅ COMPLETE

---

## Analysis: Struct Conversions

Reviewed the following classes for potential struct conversion:

| Class | Location | Verdict | Reason |
|-------|----------|---------|--------|
| `OneWayEntry` | BindingRegistry.cs | ❌ No | Contains `Func<>` delegate, stored in List |
| `TwoWayEntry` | BindingRegistry.cs | ❌ No | Contains `Func<>` delegate, stored in List |
| `WhenEntry` | BindingRegistry.cs | ❌ No | Contains `Func<>` delegate, stored in List |
| `WhenObservedEntry` | BindingRegistry.cs | ❌ No | Contains `Func<>` delegate, stored in List |
| `PropertySegment` | BindingGenerator.cs | ❌ No | 3 strings + 7 bools (~40 bytes), stored in List, copied on access |
| `InvocationModel` | BindingGenerator.cs | ❌ No | 20+ fields, too large, contains reference types |
| `FilteredInvocation` | BindingGenerator.cs | ❌ No | Contains string references |

**Conclusion:** No good struct candidates. Classes with delegate fields or stored in collections don't benefit from struct conversion.

---

## Analysis: Initial Capacity Optimizations

| Item | Impact | Verdict |
|------|--------|---------|
| Dictionary initial capacity | Low | ❌ Skip - One-time startup cost, negligible |
| List initial capacity | Low | ❌ Skip - Compile-time only, few items |
| HashSet initial capacity | Low | ❌ Skip - Compile-time only |

**Conclusion:** Initial capacity optimizations are micro-optimizations with negligible real-world impact for this codebase. Lists are small (< 100 items) and dictionaries are populated once at startup.

---

## 1. BindingRegistry String Optimizations

- [x] **1.1** `Bindings/BindingRegistry.cs` - Use `string.Concat` for key construction (avoids intermediate allocations)
- [x] **1.2** `Bindings/BindingRegistry.cs` - Optimize `SplitTypePath` to avoid `typePart` allocation in Release mode

---

## 2. InternalLeaf Classes - PropertyChangedEventArgs Caching

- [x] **2.1** `Bindings/InternalLeaf.cs` - Use `PropertyEventArgsCache` for `InternalLeaf`, `InternalMid`, `InternalRoot` classes

---

## 3. Generated Code Quality Review

The generated code patterns affect runtime performance. These were reviewed and found to already be optimal:

- [x] **3.1** Review `ThreadLocal<bool>` usage - ✅ Properly disposed via `__builder.Add(__updating)`
- [x] **3.2** Verify static lambda patterns - ✅ Already uses `.Subscribe(this, static (_, s) => { ... })` pattern
- [x] **3.3** Review `Disposable.CreateBuilder()` usage - ✅ Optimal disposal patterns with sealed state classes

**Finding:** The source generator already emits closure-free code using R3's static lambda patterns.

---

## 4. FrozenDictionary for Lookup (Deferred)

- [~] **4.1** `UiBindingMetadata.cs` - FrozenDictionary would require .NET 8+, source generator targets netstandard2.0
- [~] **4.2** `BindingRegistry.cs` - FrozenDictionary requires a "freeze" point, current pattern adds/reads dynamically at startup

**Decision:** Deferred - complexity outweighs benefit. Current `ImmutableDictionary` and `Dictionary` are adequate.

---

## Progress Log

| Item | Status | Notes |
|------|--------|-------|
| 1.1 | ✅ Complete | Changed `+` concatenation to `string.Concat(fromPath, "\|", toPath)` |
| 1.2 | ✅ Complete | Added `#if DEBUG` to only allocate typePart for logging in debug mode |
| 2.1 | ✅ Complete | All 3 classes now use `PropertyEventArgsCache.GetPropertyChanged()` |
| 3.1-3.3 | ✅ Already Optimal | Generated code verified to use closure-free patterns |
| 4.1-4.2 | Deferred | Complexity vs benefit analysis - not worth the change |

---

## Summary

**Optimizations Applied:**
1. String allocation reduction in BindingRegistry (string.Concat, conditional typePart)
2. PropertyEventArgsCache usage in InternalLeaf test types

**Already Optimal (No Changes Needed):**
- Generated binding code uses static lambdas with state tuples
- Sealed state classes properly implement IDisposable
- ThreadLocal disposed correctly via Disposable.CreateBuilder

**Not Applicable:**
- Struct conversions (delegates and collections make this counterproductive)
- Initial capacity (negligible for startup/compile-time operations)
- FrozenDictionary (netstandard2.0 constraint, dynamic population pattern)