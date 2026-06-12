# Upstream Changes Review

> **Status as of 2026-03-30**
> This review was created on 2026-03-30 and covers the period from the initial migration (November 2025) through March 2026.
> Items are being addressed in the current sprint — see the checklist below for progress.
> For the current parity state of each library (synced versions, component mapping, known gaps), see [docs/LibraryParity.md](LibraryParity.md).

**Review Date**: 2026-03-30  
**Review Window**: Late November 2025 → March 2026  
**Upstream Sources**:
- [reactivemarbles/DynamicData](https://github.com/reactivemarbles/DynamicData) — latest: **9.4.31** (2026-03-08)
- [reactiveui/ReactiveUI](https://github.com/reactiveui/ReactiveUI) — latest: **23.1.8** (2026-02-28)
- [Cysharp/R3](https://github.com/Cysharp/R3) — latest: **1.3.0** (2025-02-15) ✅ already on latest

---

## Legend

- **Priority**: 🔴 High · 🟡 Medium · 🔵 Low
- **Source**: [DD] DynamicData · [RxUI] ReactiveUI
- **Type**: Bug Fix · New Operator · Enhancement · Performance · Breaking Change

---

## Section 1 — R3Ext.DynamicData Port: Bug Fixes

These are bugs fixed in DynamicData that may have equivalent issues in our ported operators. Each item should be investigated and fixed if the same defect exists locally.

- [x] 🔴 **[DD 9.4.31 #1017] ToObservableChangeSet — Deadlock Fix**  
  _Type: Bug Fix_  
  DynamicData rewrote `.ToObservableChangeSet()` for both Cache and List variants to eliminate a deadlocking issue. Our `ToObservableChangeSet` (implemented in `List/ObservableListEx.cs`) should be audited against the upstream rewrite to determine if the same deadlock scenario is reproducible.  
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1017  
  _(fixed: audited against upstream rewrite; applied thread-safety improvements to `ToObservableChangeSet` to eliminate the same lock-inversion scenario.)_

- [x] 🔴 **[DD 9.4.31 #1063] List Filter — Refresh Change Support and Ordering Preservation**  
  _Type: Bug Fix_  
  The static list `.Filter()` operator was rewritten to properly support `Refresh` changeset reasons and to preserve item ordering for downstream consumers. Audit `R3Ext.DynamicData/List/Internal/Filter.cs` against these requirements and add Refresh-specific tests.  
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1063  
  _(fixed: `List/Internal/Filter.cs` updated to handle `Refresh` change reasons and maintain stable ordering; Refresh-specific tests added.)_

- [x] 🟡 **[DD 9.4.31 #1013] Cache Filter — Bogus Overload Removal**  
  _Type: Bug Fix_  
  DynamicData removed a `.Filter()` overload that contained a logic error causing all items to always be filtered out. Verify that `ObservableCacheEx.Filter.cs` does not contain an equivalent overload with this defect. Also review #1048 (cache Filter operator modernization) for any additional correctness issues to adopt.  
  _Upstream PRs_: https://github.com/reactivemarbles/DynamicData/pull/1013, https://github.com/reactivemarbles/DynamicData/pull/1048  
  _(audited: not affected — only FilterCacheInternal exists, which requires a predicate. Refresh handling is correct: re-evaluates and emits Refresh/Remove/Add-as-Refresh as appropriate. Comment added to source file.)_

- [x] 🟡 **[DD 9.4.31 #1059] WhenValueChanged — Null Fallback for Non-Nullable Value Types**  
  _Type: Enhancement_  
  DynamicData enhanced `.WhenValueChanged()` to support type casting within the expression, specifically allowing `null` as a fallback value for non-nullable value types. Our `WhenValueChanged` implementation (which requires an explicit key selector for AOT safety) should be updated to support this pattern.  
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1059  
  _(audited: not affected — our implementation uses explicit selectors rather than expression trees for AOT safety. Callers can already pass default(T) or any custom fallback directly. Comment added to source file.)_

- [x] 🟡 **[DD 9.1.1 #935] Bind for ISortedChangeSet — ResetOnFirstTimeLoad Fix**  
  _Type: Bug Fix_  
  The `Bind()` operators for `ISortedChangeSet<TObject, TKey>` were not correctly using the `ResetOnFirstTimeLoad` option — it was only applied when the initial changeset exceeded the `ResetThreshold`. Audit our `SortAndBind` and any `Bind` overloads for sorted changesets for this defect.  
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/935  
  _(fixed: added `ResetOnFirstTimeLoad` property to `SortAndBindOptions` (default `true`). `SortAndBindInternal` now always performs a full reset on the first changeset when this flag is set, regardless of `ResetThreshold`.)_

- [x] 🟡 **[DD 9.1.1 #938] GroupOnObservable — OnCompleted Handling Fix**  
  _Type: Bug Fix_  
  Fix for `GroupOnObservable` incorrectly handling `OnCompleted`. Audit our `GroupOn` / `GroupOnObservable` implementation for the same issue (missing or incorrect propagation of completion through group state).  
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/938  
  _(audited: not affected — `GroupOn` passes `observer.OnCompleted` directly to `Subscribe`, so source completion propagates immediately to downstream. Comment added to source file.)_

- [x] 🟡 **[DD 9.1.1 #940] ChangeSetMergeTracker — Value Type Support Fix**  
  _Type: Bug Fix_  
  `ChangeSetMergeTracker` did not correctly work with value types. Our `MergeChangeSets` implementation should be audited for the same defect when `TObject` is a struct or value type.  
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/940  
  _(audited: not affected — `MergeChangeSets` uses `HashSet<T>(EqualityComparer<T>.Default)` and `Dictionary<T, long>(EqualityComparer<T>.Default)` throughout, which use proper value equality for structs. Comment added to source file.)_

- [x] 🟡 **[DD 9.1.1 #945] Join Operators — Initialization Fix (single initial changeset)**  
  _Type: Bug Fix_  
  Join operators were emitting more than one initial changeset, and emitting before both sources had initialized. Audit `ObservableCacheEx.Joins.cs` for the same initialization race condition.  
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/945  
  _(audited: not affected — all four join types use `RecomputeAndEmit()` which only emits when there are actual result changes. When only one side has emitted, no overlapping keys exist so no emission occurs; the single initial emission happens only when both sides have matching keys.)_

- [x] 🟡 **[DD 9.4.1 #1012] Join Operators — Re-Grouping When Foreign Key Changes**  
  _Type: Bug Fix_  
  Fixed incomplete or missing support for re-grouping in Join operators when foreign key values change. This is a separate issue from the initialization fix above. Review all four join types (Inner, Left, Right, Full) in our implementation.  
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1012  
  _(audited: not affected — all four join types process Update changes by replacing the dictionary entry and calling `RecomputeAndEmit()`, which removes stale results and adds new overlapping-key results correctly.)_

- [x] 🟡 **[DD 9.1.1 #967] SortAndPage — Missing Downstream Changeset When All Items on Current Page**  
  _Type: Bug Fix_  
  `.SortAndPage()` would not send a downstream changeset when the comparer changed and the current page already contained all items. Audit our `Page()` / `Sort()` combination and the `SortAsync` operator for this edge case.  
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/967  
  _(audited: not affected — our virtualize/page/sort operators use a different architecture that re-emits the full virtual window on every sort change, so this edge case cannot occur.)_

- [x] 🟡 **[DD 9.1.1 #968] Switch — Error Propagation Fix**  
  _Type: Bug Fix_  
  `.Switch()` did not propagate errors downstream. Audit our equivalent switching/flattening operators for proper error propagation.  
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/968  
  _(audited: not affected — inner source is subscribed with `innerSource.Subscribe(observer)`, routing OnNext/OnError/OnCompleted directly to the downstream observer. Comment added to source file.)_

- [x] 🔵 **[DD 9.2.2 #997] Virtual Sort — Same-Page Sort Bug**  
  _Type: Bug Fix_  
  Fixed a virtual sort bug that manifested when sorting items that remain on the same page. Audit our `Virtualize`/`Page`/`Sort` pipeline integration for this edge case.  
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/997  
  _(audited: not affected — different architecture; our virtualize/page/sort pipeline re-computes the full window on each sort change rather than diffing positions.)_

---

## Section 2 — R3Ext.DynamicData Port: New Operators

These are new operators added to DynamicData after the initial port that are not currently in our migration matrix.

- [x] 🟡 **[DD 9.4.1 #1011] AsyncDisposeMany — New Operator**  
  _Type: New Operator_  
  DynamicData added `.AsyncDisposeMany()`, equivalent to `.DisposeMany()` but for items implementing `IAsyncDisposable`. This operator does not exist in our port and should be added to both Cache and List variants.  
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1011  
  _File to add_: `R3Ext.DynamicData/Cache/ObservableCacheEx.AsyncDisposeMany.cs`, `R3Ext.DynamicData/List/ObservableListEx.cs`  
  _(implemented: Cache and List variants added; IAsyncDisposable support with fire-and-forget disposal; 9 tests passing.)_

- [x] 🟡 **[DD 9.4.1 #1008] TransformOnObservable — New Cache Operator (with ordering)**  
  _Type: New Operator_  
  DynamicData has a `TransformOnObservable` Cache operator (transforms each item via an observable, preserving changeset ordering). This operator is not in our migration matrix or codebase. Assess whether it warrants porting.  
  _Upstream source_: `src/DynamicData/Cache/Internal/TransformOnObservable.cs`  
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1008  
  _File to add_: `R3Ext.DynamicData/Cache/Internal/TransformOnObservable.cs`  
  _(implemented: Cache operator added with ordering preservation; 5 tests passing.)_

- [x] 🔵 **[DD 9.1.1 #941] Filter — Predicate State Stream Overloads**  
  _Type: New Operator / Enhancement_  
  New `.Filter()` overloads that accept a predicate _and_ a separate state stream, avoiding the need to allocate a new delegate every time filtering logic changes. Useful for high-frequency filter updates. Add to both Cache and List variants.  
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/941  
  _(implemented: Cache and List overloads added; 10 tests passing.)_

---

## Section 3 — R3Ext.DynamicData Port: Performance and Correctness Improvements

Improvements that don't introduce new APIs but improve the behavior or performance of existing operators.

- [x] 🟡 **[DD 9.4.31 #1027] Background Scheduling — Weak Reference Leak Fix**  
  _Type: Performance / Correctness_  
  DynamicData added weak-referencing to all operators that use background scheduling, ensuring schedulers do not hold a strong reference that prevents operator subscriptions from being collected. Audit all operators in our codebase that use `TimeProvider`-based or background scheduling for equivalent leaks.  
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1027  
  _(fixed: `AutoRefresh` List variant updated to use weak references for the background scheduling subscription, eliminating the memory leak.)_

- [x] 🟡 **[DD 9.4.31 #1064–1069] OnItemAdded / OnItemRemoved / OnItemRefreshed — List Rewrites**  
  _Type: Performance / Correctness_  
  All three list-variant notification operators were rewritten in DynamicData 9.4.31. Our implementations exist in `ObservableListEx.cs`. Review the upstream rewrites for correctness improvements (particularly around change reason handling and concurrency).  
  _Upstream PRs_: https://github.com/reactivemarbles/DynamicData/pull/1064, https://github.com/reactivemarbles/DynamicData/pull/1067, https://github.com/reactivemarbles/DynamicData/pull/1068  
  _(audited: not affected — OnBeingAdded handles Add+AddRange correctly; OnBeingRemoved handles Remove/RemoveRange/Replace/Clear correctly; OnItemRefreshed iterates Refresh changes per item. Comment added to ObservableListEx.cs.)_

- [x] 🟡 **[DD 9.1.1 #936] SortAndBind — Use Move Instead of RemoveAt/Insert**  
  _Type: Performance_  
  DynamicData updated `SortAndBind` to emit `Move` changesets instead of `RemoveAt`/`Insert` pairs when items reorder. This results in fewer downstream change notifications and better binding performance. Audit our `SortAndBind` implementation.  
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/936  
  _(fixed: `SortAndBind` updated to emit `Move` instead of `RemoveAt`/`Insert` pairs for item reordering.)_

- [x] 🔵 **[DD 9.3.2 #1005] Internal Lock Primitive Modernization**  
  _Type: Performance_  
  DynamicData replaced internal locking with a newer lock primitive for better performance. Assess whether our internal synchronization patterns (particularly in `SourceCache` and `SourceList`) should adopt the same approach.  
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1005  
  _(audited: not applicable — R3 port uses a different concurrency model based on R3 schedulers and `Subject<T>`; the upstream lock primitive change does not translate to our architecture.)_

---

## Section 4 — R3Ext.DynamicData Port: MigrationMatrix Updates

These items update the existing migration tracking matrix in `docs/MigrationMatrix.md`.

- [x] 🟡 **Update MigrationMatrix.md — Add `TransformOnObservable` entry**  
  Add a row for `TransformOnObservable` (Cache) to the Transformation section with `NotStarted` / `None` status.  
  _(done: added with `Implemented` / `Passing` status; 5 tests.)_

- [x] 🟡 **Update MigrationMatrix.md — Add `AsyncDisposeMany` entry**  
  Add a row for `AsyncDisposeMany` (Cache/List) to the Lifecycle section with `NotStarted` / `None` status.  
  _(done: added with `Implemented` / `Passing` status; 9 tests.)_

- [x] 🔵 **Update MigrationMatrix.md — `FilterOnProperty` formally removed upstream**  
  DynamicData 9.4.31 explicitly removed `FilterOnProperty` (it was previously just obsoleted). Update the `Deferred` note in the matrix to reflect that it is now fully removed upstream and will never need porting.  
  _(done: FollowUp updated to note full removal in DynamicData 9.4.31.)_

---

## Section 5 — ReactiveUI: Bug Fixes and Correctness

Bugs fixed in ReactiveUI that may have analogues in our R3Ext implementation.

- [x] 🟡 **[RxUI 22.3.1 #4196] RxCommand — ReactiveCommand Cancellation Race Condition**  
  _Type: Bug Fix_  
  A race condition was fixed in ReactiveUI's `ReactiveCommand` cancellation path. Audit our `RxCommand<TInput, TOutput>` for a similar race condition in cancellation handling, particularly when `CanExecute` changes concurrently with command execution.  
  _Upstream PR_: https://github.com/reactiveui/ReactiveUI/pull/4196  
  _(fixed: replaced simple `_isExecuting.Value = true/false` in `Execute()` with an `Interlocked` counter (`_executingCount`). `_isExecuting` is now only set to `true` on the first concurrent increment and cleared to `false` only when the count reaches zero, preventing premature clearing when multiple executions overlap.)_

- [x] 🟡 **[RxUI 23.1.0-beta.1 #4240] Nested Property Binding — Redundant Setter Calls**  
  _Type: Bug Fix_  
  Nested property bindings were calling the setter redundantly when intermediate path nodes changed. Audit our source-generated `WhenChanged(vm => vm.A.B.C)` and `BindOneWay`/`BindTwoWay` implementations for the same redundant-setter behavior.  
  _Upstream PR_: https://github.com/reactiveui/ReactiveUI/pull/4240  
  _(audited: not affected — source-generated bindings use explicit compiled property chains rather than runtime reflection/expression trees; intermediate node changes trigger only the appropriate leaf setter with no redundant calls. Different architecture.)_

- [x] 🟡 **[RxUI 23.1.8 #4301] Builder StackOverflow / Activator Negative RefCount / Binding Regression**  
  _Type: Bug Fix_  
  Multiple related fixes: StackOverflow in builder patterns, negative refCount in activators, and a binding regression. Review our `BindingRegistry`, `RxCommand` activation, and any builder-style initialization APIs for these classes of defect.  
  _Upstream PR_: https://github.com/reactiveui/ReactiveUI/pull/4301  
  _(audited: not applicable — R3Ext uses a different initialization architecture with no RxAppBuilder, no activator refCount, and no builder StackOverflow risk. Source-generated bindings bypass the upstream binding registry entirely.)_

---

## Section 6 — ReactiveUI: Performance Improvements

- [x] 🟡 **[RxUI 22.3.1 #4195] RxObject / RxRecord — Allocation Reduction**  
  _Type: Performance_  
  ReactiveUI reduced allocations within `ReactiveObject` and `ReactiveRecord` (the bases for our `RxObject` and `RxRecord`). Review their change and assess whether equivalent allocation optimizations can be applied to our `RxObject.cs` and `RxRecord.cs` implementations.  
  _Upstream PR_: https://github.com/reactiveui/ReactiveUI/pull/4195  
  _(audited: not affected — `PropertyEventArgsCache` already provides args caching so `PropertyChangedEventArgs` instances are never reallocated; allocations in `RxObject`/`RxRecord` are already minimized. No further action required.)_

---

## Section 7 — ReactiveUI: New Features

Evaluate each for potential inclusion in R3Ext.

- [ ] 🔵 **[RxUI 22.3.1 #4205] ReactiveOwningComponentBase — Blazor Support**  
  _Type: New Feature_  
  ReactiveUI added `ReactiveOwningComponentBase` for Blazor component lifecycle integration. If Blazor is in scope for R3Ext's platform targets, a corresponding `R3OwningComponentBase` should be considered.  
  _Upstream PR_: https://github.com/reactiveui/ReactiveUI/pull/4205

- [ ] 🔵 **[RxUI 23.1.0-beta #4212, #4224] Platform-Specific MAUI Scheduler Support**  
  _Type: New Feature_  
  ReactiveUI added dedicated platform-specific main-thread schedulers for MAUI and improved the builder API with custom scheduler support. Assess whether our `R3Ext.Bindings.MauiTargets` and MAUI dispatcher integration should be updated to align with this pattern.  
  _Upstream PR_: https://github.com/reactiveui/ReactiveUI/pull/4212

- [ ] 🔵 **[RxUI 23.1.0-beta #4228, #4232] RxAppBuilder API Enhancements**  
  _Type: New Feature_  
  ReactiveUI introduced `BuilderMixins` and an enhanced `RxAppBuilder` pattern for application initialization. If R3Ext plans to provide an initialization/bootstrap API, these patterns are worth reviewing.  
  _Upstream PR_: https://github.com/reactiveui/ReactiveUI/pull/4228

- [ ] 🔵 **[RxUI 23.1.0-beta.8 #4277] WhenActivated Default Calls in WPF Base Classes**  
  _Type: New Feature_  
  `WhenActivated` is now called by default in WPF reactive base classes. If R3Ext ever adds WPF platform support, consider this pattern.

---

## Section 8 — Ongoing Internal Work (Carry-Forward)

These items are from `docs/ClosureEliminationStatus.md` and `docs/MigrationMatrix.md` and represent work already planned but not yet complete.

### Closure Elimination (Performance Optimization)

- [ ] 🟡 **List Aggregates — Max, Min, Avg, StdDev (4 operators)**  
  High-priority closure elimination; established patterns exist; est. 8–12 hours.

- [ ] 🟡 **List Internal Operators — GroupBy, TransformMany, Sort, QueryWhenChanged, Reverse, DynamicFilter, OnBeingRemoved (7 operators)**  
  Est. 15–25 hours.

- [ ] 🟡 **Cache Core Operators — Filter, Transform, DisposeMany, FilterOnObservable, ExpireAfter, AutoRefresh, EnsureUniqueKeys, TransformAsync (8 operators)**  
  Est. 10–15 hours for core four.

- [ ] 🔵 **Cache Medium-Priority Operators — AddKey, Cast, ToObservableOptional, Set Operations, QueryWhenChanged, Virtualize, ChangeKey, SuppressRefresh (8 operators)**  
  Est. 20–32 hours.

- [ ] 🔵 **Cache Specialized Operators — Sort (2 overloads), TransformSafe (2), Batch (2), Joins (4), WhenValueChanged (2), IncludeUpdateWhen, Grouping, TreeBuilder (13+ operators)**  
  Est. 30–50 hours.

### Missing Operators (from MigrationMatrix)

- [ ] 🔵 **DynamicCombiner — Dynamic composite collection combining (Cache/List)**  
  Status: NotStarted. Complex implementation; deferred pending need.

- [ ] 🔵 **InvokeEvaluate / IEvaluateAware — Cache evaluation support**  
  Status: NotStarted. Niche feature; low priority.

### Optional Enhancements (from MigrationMatrix)

- [ ] 🔵 **Cache aggregates — Count, Sum, Max/Min, Avg, StdDev for Cache variant**
- [ ] 🔵 **List variants — WhenValueChanged, WhenValueChangedWithPrevious**
- [ ] 🔵 **List variants — TrueForAll / TrueForAny**
- [ ] 🔵 **GroupOnProperty overloads** (property-based GroupOn convenience methods)
- [ ] 🔵 **Custom IEqualityComparer overloads** for various operators
- [ ] 🔵 **Projection overload variants** for QueryWhenChanged

---

## Section 9 — Infrastructure / Housekeeping

- [ ] 🔵 **[DD 9.4.31] .NET 10 Target Framework Support**  
  DynamicData added a `net10.0` target. Track .NET 10 availability and add it to R3Ext.DynamicData and R3Ext target frameworks when it reaches GA.

- [ ] 🔵 **[DD 9.3.1] Verify Net 9.0 Test Package Alignment**  
  DynamicData 9.3.1 upgraded test packages for .NET 9.0 compatibility. Verify our `Microsoft.NET.Test.Sdk`, `xunit`, and coverage packages are at current stable versions (upstream now uses `18.x` SDK and `xunit` v3).

- [ ] 🔵 **[RxUI 23.1.0] netstandard2.0 Dropped Upstream**  
  ReactiveUI has dropped `netstandard2.0`. Our libraries already target `net9.0` exclusively (except for analyzer/build-task projects which remain on `netstandard2.0` by necessity). No action required; noted for context.

---

## Prioritized Action Summary

| Priority | Count | Description |
|----------|-------|-------------|
| 🔴 High  | 0     | All high-priority items resolved (ToObservableChangeSet deadlock fixed; List Filter Refresh support fixed) |
| 🟡 Medium | 2    | Closure elimination: List aggregates and internal operators (Section 8) |
| 🔵 Low   | 14+  | Optional operators, enhancements, infra housekeeping (Sections 7–9) |

**Recommended starting point**: Items in Sections 1 and 2 (DynamicData bug fixes and new operators) have the highest user-visible impact and the most direct precedent in the upstream codebase to reference.
