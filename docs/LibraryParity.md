# Library Parity Tracking

This document tracks R3Ext's parity with its upstream source libraries. Update this document whenever upstream changes are reviewed and incorporated.

---

## How to Use This Document

R3Ext borrows from, ports, or depends on three upstream libraries. This document answers the question: **"How current is our implementation relative to what's upstream?"**

**Workflow:**

1. When a new upstream release is published (watch each library's releases page), open `docs/UpstreamChangesReview.md` and add a new review section for the release.
2. Work through the review checklist — for each change, determine whether it applies to R3Ext and either apply it or defer it with a note.
3. Once the review is complete, come back here and:
   - Update "Last Upstream Synced Against" in the table below.
   - Add a row to the [Sync History](#sync-history) table.
4. For R3 specifically, "syncing" means bumping the NuGet package version and verifying no breaking changes affect our API surface.

**When to check:** After each upstream release, or at minimum before each R3Ext release milestone.

---

## Upstream Library Versions

| Library | Our Dependency Version | Last Upstream Synced Against | Upstream Latest | Changelog |
|---------|------------------------|------------------------------|-----------------|-----------|
| [R3](https://github.com/Cysharp/R3) | 1.3.0 | 1.3.0 | 1.3.0 | [Releases](https://github.com/Cysharp/R3/releases) |
| [DynamicData](https://github.com/reactivemarbles/DynamicData) | N/A (ported) | 9.4.31 (Mar 2026) | 9.4.31 | [Releases](https://github.com/reactivemarbles/DynamicData/releases) |
| [ReactiveUI](https://github.com/reactiveui/ReactiveUI) | N/A (patterns ported) | 23.1.8 (Mar 2026) | 23.1.8 | [Releases](https://github.com/reactiveui/ReactiveUI/releases) |

> **Note**: "N/A (ported)" means the library is not a NuGet dependency — its code was translated and adapted into R3Ext. Parity tracking means reviewing upstream changes and deciding whether to incorporate them, not updating a package reference.

---

## Component Mapping

### R3Ext.DynamicData ← DynamicData

The `R3Ext.DynamicData` project is a full port of [DynamicData](https://github.com/reactivemarbles/DynamicData) (by Roland Pheasant / ReactiveMarbles) from `System.Reactive` to R3. The port was performed in November 2025 against DynamicData ~9.0.x.

**Key architectural differences from upstream:**
- Uses R3 `Observable<T>` and `Observer<T>` instead of `System.Reactive` equivalents.
- Closure-elimination pattern applied throughout to reduce allocations (see `docs/ClosureEliminationStatus.md`).
- AOT-safe: no runtime expression trees; key selectors are explicit delegate parameters.
- `IScheduler` replaced by R3 `TimeProvider`-based scheduling.

**Namespace / Class Mapping:**

| DynamicData (upstream) | R3Ext.DynamicData (ours) | Notes |
|------------------------|-----------------------|-------|
| `DynamicData` | `R3Ext.DynamicData` | Root namespace |
| `SourceCache<TObject, TKey>` | `SourceCache<TObject, TKey>` | Full parity |
| `SourceList<T>` | `SourceList<T>` | Full parity |
| `ObservableCacheEx` | `ObservableCacheEx` | Extension methods; split into per-operator files |
| `ObservableListExtensions` | `ObservableListEx` | Renamed for consistency |
| `IObservableCache<TObject, TKey>` | `IObservableCache<TObject, TKey>` | Full parity |
| `IObservableList<T>` | Merged into `ISourceList<T>` | No separate type; `Count`/`CountChanged`/`Items`/`Connect()` absorbed into `ISourceList<T>`; `Connect(predicate)` overload and `Preview()` not ported — see Known Gaps |
| `IChangeSet<T>` | `IChangeSet<T>` | List changeset |
| `IChangeSet<TObject, TKey>` | `IChangeSet<TObject, TKey>` | Cache changeset |
| `SortExpressionComparer<T>` | Not ported | Fluent comparer-builder not ported; Sort operators accept `IComparer<T>` directly — see Known Gaps |
| `GroupWithImmutableState` | `GroupWithImmutableState` | Full parity |
| `ChangeAwareList<T>` | `ChangeAwareList<T>` | Full parity |
| `TransformOnObservable` | ✅ Ported | See `UpstreamChangesReview.md` §2 |
| `AsyncDisposeMany` | ✅ Ported | See `UpstreamChangesReview.md` §2 |
| `FilterOnProperty` (obsolete) | Deferred | Superseded upstream; use `AutoRefresh + Filter` |

**File layout in `R3Ext.DynamicData/`:**

```
Cache/
  Internal/           ← Internal operator implementations (one class per operator)
  ObservableCacheEx.*.cs  ← Public extension methods (one file per operator group)
List/
  Internal/           ← Internal operator implementations
  ObservableListEx.cs ← Public extension methods for list operators
Shared/               ← Shared utilities (change reasons, comparers, etc.)
```

For operator-level status (Implemented / Optimized / Deferred / NotStarted), see [`docs/MigrationMatrix.md`](MigrationMatrix.md).

---

### R3Ext ← ReactiveUI

The `R3Ext` project borrows patterns from [ReactiveUI](https://github.com/reactiveui/ReactiveUI). These are **not** line-for-line ports — they are R3-native reimplementations of the same concepts with adaptations for AOT, source generation, and R3's observable model.

**Patterns borrowed and their R3Ext equivalents:**

| ReactiveUI (upstream) | R3Ext equivalent | Notes |
|-----------------------|------------------|-------|
| `ReactiveCommand<TInput, TOutput>` | `RxCommand<TInput, TOutput>` | R3-native; same `ICommand` + `IObservable<T>` contract |
| `ReactiveCommand.CreateFromTask(...)` | `RxCommand.CreateFromTask(...)` | Async with `CancellationToken` support |
| `ReactiveCommand.CreateCombined(...)` | `RxCommand<Unit, Unit[]>.CreateCombined(...)` | Aggregate command |
| `Interaction<TInput, TOutput>` | `Interaction<TInput, TOutput>` | Direct port; view-ViewModel communication pattern |
| `ReactiveObject` | `RxObject` | INPC base class; uses R3 `Subject<T>` internally |
| `ReactiveRecord` | `RxRecord` | Record variant of `RxObject` |
| `WhenAnyValue(vm => vm.Property)` | `WhenChanged(vm => vm.Property)` | Source-generated; compile-time path validation |
| `WhenAnyObservable(vm => vm.Obs)` | `WhenObserved(vm => vm.Obs)` | Source-generated; auto-switches subscriptions |
| `BindCommand(...)` / `OneWayBind(...)` | `BindOneWay(...)` / `BindTwoWay(...)` | Source-generated binding methods |
| `IActivatableViewModel` | Not yet ported | Lifecycle activation pattern |
| `WhenActivated(...)` | Not yet ported | View activation observable |

**Key differences from ReactiveUI:**
- No `Splat`/`Locator` dependency; no service locator pattern.
- Bindings are source-generated at compile time rather than using runtime expression trees.
- `WhenChanged` / `WhenObserved` use `UnsafeAccessor` for member access on .NET 8+ (no reflection).
- `RxCommand` uses R3's `ReactiveProperty`-style state management internally.

---

### R3Ext ← R3 (direct dependency)

R3Ext takes a **direct NuGet dependency** on [R3](https://github.com/Cysharp/R3) by Cysharp. The entire observable pipeline, scheduling, and operator foundation is R3.

**Parity for R3 means:** keeping the `<PackageReference>` version current and verifying no breaking changes affect R3Ext's API surface.

**Current state:** ✅ Using R3 1.3.0, which is the current latest release.

**Upgrade checklist (for future R3 bumps):**
1. Review the R3 release notes for breaking changes.
2. Check whether any `Observable` extension methods in `R3Ext` now duplicate new R3 built-ins (remove duplicates if so).
3. Run the full test suite — R3 breaking changes will surface as compilation errors.
4. Update the version in `Directory.Build.props` and the table above.

---

## Sync Workflow

### Checking for Upstream Changes

**Recommended cadence:** After each upstream release, or before each R3Ext sprint planning session.

1. Check releases pages:
   - R3: https://github.com/Cysharp/R3/releases
   - DynamicData: https://github.com/reactivemarbles/DynamicData/releases
   - ReactiveUI: https://github.com/reactiveui/ReactiveUI/releases

2. Open `docs/UpstreamChangesReview.md`. This is the **working document** for a given review period — it contains itemized change analysis with checkboxes.

3. **For R3**: Check if the NuGet version needs bumping. Scan release notes for breaking changes or new operators that overlap with R3Ext's extension operators.

4. **For DynamicData**: Compare the release notes from the "Last Upstream Synced Against" version through the latest. For each change:
   - Bug fixes → Audit the equivalent operator in `R3Ext.DynamicData/` and fix if the same bug exists.
   - New operators → Evaluate whether to port; add to `docs/MigrationMatrix.md` if accepted.
   - Performance improvements → Assess for adoption.

5. **For ReactiveUI**: Compare release notes from the last synced version. Focus on:
   - Bug fixes in `ReactiveCommand`, `ReactiveObject`, `Interaction`, or binding operators.
   - New patterns relevant to MVVM, MAUI, or AOT that R3Ext could adopt.

### Updating This Document

After completing a sync review and incorporating changes:

1. Update "Last Upstream Synced Against" in the [Upstream Library Versions](#upstream-library-versions) table.
2. Add a row to the [Sync History](#sync-history) table with the date, library, from/to versions, and a brief description.
3. Update `docs/UpstreamChangesReview.md` — check off completed items and add notes on deferred items.
4. If new operators were ported, update `docs/MigrationMatrix.md` accordingly.

---

## Sync History

| Date | Library | From Version | To Version | Changes |
|------|---------|--------------|------------|---------|
| 2025-11-22 | DynamicData | — | 9.0.x | Initial migration — ~93% operator parity achieved |
| 2025-11-22 | ReactiveUI | — | 22.2.1 | Initial migration — RxCommand, Interaction, RxObject, WhenChanged patterns ported |
| 2025-11-22 | R3 | — | 1.3.0 | Initial dependency — current latest at time of project creation |
| 2026-03-30 | DynamicData | 9.0.x | 9.4.31 | Bug fixes: ToObservableChangeSet thread safety (fixed), List Filter Refresh support (fixed), SortAndBind ResetOnFirstTimeLoad (fixed), SortAndBind Move optimization (fixed), RxCommand concurrent execution (fixed), List AutoRefresh memory leak (fixed); New operators: AsyncDisposeMany (Cache+List), TransformOnObservable (Cache), Filter predicate state stream overloads (Cache+List); 8 audits confirmed not affected |
| 2026-03-30 | ReactiveUI | 22.2.1 | 23.1.8 | Bug fixes and patterns review (see UpstreamChangesReview.md) |

---

## Known Gaps

The following are known areas where R3Ext's implementation differs from or lags behind upstream. Items are categorized by priority.

### DynamicData Gaps

**Interface hierarchy differences:**
- `IObservableList<T>` does not exist as a separate type. In upstream DynamicData, `IObservableList<T>` is the read-only base interface and `ISourceList<T>` extends it with a single `Edit()` method. In R3Ext, `ISourceList<T>` is a standalone interface that absorbs the read-only members (`Count`, `CountChanged`, `Items`, `Connect()`), but two members are not ported:
  - `Connect(Func<T, bool>? predicate)` — the filtered overload is absent from the list interface (filtering is available via the `Filter` operator instead).
  - `Preview(Func<T, bool>? predicate)` — pre-application change stream is absent from the list interface (it is present on `IObservableCache<TObject, TKey>`).

**Missing convenience types:**
- `SortExpressionComparer<T>` — the fluent comparer-builder class (`Ascending(x => x.Prop).ThenByDescending(x => x.Other)`) is not ported. The `Sort` operator accepts any `IComparer<T>`, so callers must supply their own implementation (e.g., `Comparer<T>.Create(...)` or a custom class).


**Not yet ported (new upstream operators):**
- _(None — all new operators from DD 9.0.x–9.4.31 have been ported: `AsyncDisposeMany`, `TransformOnObservable`, and `Filter` predicate state stream overloads.)_

**Bug fixes pending audit:**
- _(All audited — ToObservableChangeSet thread safety fixed; List Filter Refresh support fixed; SortAndBind ResetOnFirstTimeLoad fixed; SortAndBind Move optimization fixed. 8 additional items audited as not affected. See `UpstreamChangesReview.md` §1–§3 for details.)_

**In-progress internal work (unrelated to upstream):**
- Closure elimination across ~30+ operators — see `docs/ClosureEliminationStatus.md`.
- Optional cache-variant aggregates (`Count`, `Sum`, `Max`, `Min`, `Avg`, `StdDev`).
- List-variant `WhenValueChanged` / `WhenValueChangedWithPrevious`, `TrueForAll` / `TrueForAny`.

### ReactiveUI Gaps

**Bug fixes pending audit:**
- Nested property binding redundant setter calls (RxUI 23.1.0 #4240).
- Builder StackOverflow / activator negative refcount (RxUI 23.1.8 #4301).

**Fixed (this sprint):**
- `RxCommand` cancellation/concurrent-execution race condition (RxUI 22.3.1 #4196) — resolved via `Interlocked` counter (`_executingCount`); `IsExecuting` clears only when the last concurrent execution completes.

**Not yet ported:**
- `IActivatableViewModel` / `WhenActivated` lifecycle pattern.
- `ReactiveOwningComponentBase` for Blazor (low priority; Blazor not currently in scope).

### R3 Gaps

None currently — on latest version (1.3.0). ✅

---

_Last updated: 2026-05-12_
