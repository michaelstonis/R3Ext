# Upstream Changes Review

This document holds one section per review window, newest first. See [docs/LibraryParity.md](LibraryParity.md) for how reviews feed the parity tracking workflow.

---

# Review — 2026-08-13 (Window: March 2026 → August 2026)

> **Status as of 2026-08-13 — INVENTORY ONLY**
> This is the Sprint 1 deliverable of the parity health assessment. Items are classified and prioritized but **not yet audited against our code** — applicability annotations (Applicable / Not Affected / Already Fixed / Architecturally N/A) land in Sprint 2. No decision to merge any change has been made.

**Review Date**: 2026-08-13
**Review Window**: March 2026 → August 2026 (since the 2026-03-30 review below)
**Baseline (per LibraryParity.md)**: DynamicData 9.4.31 · ReactiveUI 23.1.8
**Upstream Sources**:
- [reactivemarbles/DynamicData](https://github.com/reactivemarbles/DynamicData) — latest release: **9.4.33** (2026-06-30); `main` is now **10.0-preview** with significant unreleased fixes
- [reactiveui/ReactiveUI](https://github.com/reactiveui/ReactiveUI) — latest release: **24.1.0** (2026-08-02); **24.0.0 (2026-07-26) is a major re-platform release**

Prior-review coverage check: the 2026-03-30 review covered DynamicData through PR #1064 and ReactiveUI through PR #4301. Every item below is newer; there is no overlap.

## Headline findings

1. **ReactiveUI 24.0 re-platformed onto `ReactiveUI.Primitives`** — an allocation-conscious engine with System.Reactive now *optional*, custom sinks/schedulers (`ISequencer`, `RxVoid`, `Signal<T>`), and AOT-friendly activation. Upstream is converging on the same design thesis R3Ext was founded on (low-alloc, AOT-safe, no mandatory System.Reactive). None of this code ports directly (different engine), but it changes the competitive/strategic picture and its perf work is worth studying. Claimed benchmarks: 3–4× faster `WhenAnyValue`/`ToProperty` subscribe+emit, 5–13× less allocation.
2. **DynamicData has a cluster of unreleased correctness fixes on `main`** (Switch completion semantics, deadlock rework, notification-suspension race, filter index bugs) that exist in operators we ported. These are merged upstream but not in any 9.4.x release — our drift is against `main`, not just released versions.
3. **ReactiveUI 23.1.1–23.2.27 were unlisted due to a revoked code-signing certificate** (NuGet `NU3012`); 23.2.28 is the re-signed consolidation. No code impact on us; noted for ecosystem awareness.
4. Several upstream fix areas (activation, suspension, routing, `BindCommand`, WPF/WinForms/Blazor platform code) **have no counterpart surface in R3Ext** and are expected to resolve as Architecturally N/A in Sprint 2.

---

## Section A — [DD] Released in 9.4.33 (2026-06-30)

- [ ] 🔴 **[DD 9.4.33 #1076] ExpireAfter — race when item removed/updated before expiration fires**
  _Type: Bug Fix_
  Race condition in `ExpireAfter` when an item is removed or updated before its expiration timer fires. We ported this operator (`Cache/Internal/ExpireAfter.cs`) and rebuilt it on `TimeProvider`; the same interleaving may be reproducible.
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1076

- [ ] 🔵 **[DD 9.4.33 #1084] Cache dynamic Filter — dictionary mutation during enumeration on old TFMs**
  _Type: Bug Fix_
  The cache-land dynamic `.Filter()` relied on limited mutation of an internal `Dictionary<,>` during enumeration — unsupported before .NET Core 3.0. Upstream now copies keys on older TFMs. We target `netstandard2.1`+ (≈ .NET Core 3.0 semantics), so likely Not Affected, but our Filter internals should be checked for the same enumeration-mutation pattern as a latent-bug matter.
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1084

- [ ] 🔵 **[DD 9.4.33 #1085] Change — corrected docs and exception messages**
  _Type: Enhancement (docs/diagnostics)_
  Fixes incorrect XML docs and exception message text on the `Change` types. Cheap to mirror if our ported `Change` carries the same text.
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1085

- [ ] 🔵 **[DD 9.4.33 #1077] SwappableLock — support .NET 9+ `System.Threading.Lock`**
  _Type: Performance_
  Upstream's lock abstraction now uses the .NET 9 `Lock` type where available. Our port has its own locking; adopting `Lock` on `net9.0`+ targets is an optional perf/idiom improvement.
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1077

- [ ] 🔵 **[DD 9.4.33 #1087] BindingListEx — `DynamicallyAccessedMembers` attributes**
  _Type: Enhancement (AOT)_
  AOT/trimming annotations for WinForms `BindingList` binding. We have no `BindingListEx`; expected Architecturally N/A, but flagging because AOT-annotation hygiene is core to our value proposition.
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1087

- [ ] 🔵 **[DD 9.4.33 #1080/#1081] XML documentation overhaul for `ObservableCacheEx` / `ObservableListEx`**
  _Type: Enhancement (docs)_
  Comprehensive doc-comment rewrites for both extension surfaces. Candidate source for improving our own XML docs where operator semantics match.
  _Upstream PRs_: https://github.com/reactivemarbles/DynamicData/pull/1080, https://github.com/reactivemarbles/DynamicData/pull/1081

Not applicable (CI/repo housekeeping, no library code): #1078 (Copilot instruction files), #1088, #1092, #1123, #1124, #1125 (release/CI plumbing), #1082 (CI test timeout).

---

## Section B — [DD] Merged on `main`, UNRELEASED (post-9.4.33, 10.0-preview branch)

> These fixes are not in any shipped DynamicData package yet (`main` was bumped to 10.0-preview in #1128). They are drift all the same: real defects fixed upstream in operators we ported.

- [ ] 🔴 **[DD main #1079] Cross-cache deadlocks — queue-drain delivery pattern**
  _Type: Bug Fix (architectural)_
  Reworks changeset delivery to a queue-drain pattern to eliminate deadlocks when caches are chained/interconnected. Our port kept lock-based delivery (and our 2026-03 review already fixed one lock-inversion in `ToObservableChangeSet` from #1017 — this is the general fix). High-value audit; potentially significant to port.
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1079

- [ ] 🔴 **[DD main #1111] WhenPropertyChanged — events fired during subscribe are dropped**
  _Type: Bug Fix_
  Property-change events raised while subscription setup is in progress were lost. We ported `WhenPropertyChanged` (cache + list); same window likely exists.
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1111

- [ ] 🔴 **[DD main #1120] List static Filter — exception from index assumptions**
  _Type: Bug Fix_
  Fixes an exception caused by incorrect index assumptions in the static list `Filter`. Directly relevant: we rewrote `List/Internal/Filter.cs` in the 2026-03 sprint (per #1063); must audit whether our rewrite shares the index assumption.
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1120

- [ ] 🔴 **[DD main #1137/#1139/#1141/#1145] Switch operator family — completion & error-propagation cluster**
  _Type: Bug Fix (4 PRs)_
  - #1137: cache `Switch` never completes.
  - #1139: list `Switch` drops completion and throws errors incorrectly.
  - #1141: a source failure is reported as successful completion to deferred subscriptions.
  - #1145: internal misuse of `ObservableCacheEx.Switch()` where `Observable.Switch()` was intended.
  We ported `List/Internal/Switch.cs` and the cache variant; completion/error semantics in R3 differ from Rx (`OnCompleted(Result)`), so this audit doubles as a semantics check.
  _Upstream PRs_: https://github.com/reactivemarbles/DynamicData/pull/1137, https://github.com/reactivemarbles/DynamicData/pull/1139, https://github.com/reactivemarbles/DynamicData/pull/1141, https://github.com/reactivemarbles/DynamicData/pull/1145

- [ ] 🔴 **[DD main #1132] SuspendNotifications / ResumeNotifications — race condition (fixes upstream #1131)**
  _Type: Bug Fix_
  Race between suspending and resuming notifications. Initial grep finds no `SuspendNotifications` in our port — either we didn't port it (gap to record in MigrationMatrix) or it's named differently. Sprint 2 resolves which.
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1132

- [ ] 🟡 **[DD main #1113] FilterImmutable — wrong Current value when Update transitions to Remove**
  _Type: Bug Fix_
  `FilterImmutable` emitted the wrong `Current` when an update caused an item to leave the filter. No `FilterImmutable` found in our port — expected Not Ported (MigrationMatrix check).
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1113

- [ ] 🟡 **[DD main #1153] BatchIf — make every overload shape resolve**
  _Type: Bug Fix_
  Overload-resolution fixes for the `BatchIf` family. We ported `BatchIf` (`ObservableCacheEx.Batch.cs`); our overload surface should be compared.
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1153

- [ ] 🟡 **[DD main #1155] WhenPropertyChanged — support implicit casts in property expressions**
  _Type: Enhancement_
  Expression-tree cast handling. Our AOT design uses explicit selectors, not expression trees — likely Not Affected by construction, mirroring the #1059 outcome from the previous review.
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1155

- [ ] 🟡 **[DD main #1135] TransformAsync — cancellation support** *(feature: case-by-case)*
  _Type: New Feature_
  Adds `CancellationToken` flow to `TransformAsync`. We ported `TransformAsync` (cache + list). Fits R3Ext's async ergonomics (our `RxCommand.CreateFromTask` already leads with cancellation) — strong candidate under the features filter, pending API-shape review.
  _Upstream PR_: https://github.com/reactivemarbles/DynamicData/pull/1135

- [ ] 🔵 **[DD main #1154] Switch — document shadowing behavior** · **[#1096/#1095] split `ObservableListEx`/`ObservableCacheEx` into per-family partials**
  _Type: Docs / Structural_
  The file splits mirror the per-operator layout we already use — informational only. The Switch shadowing docs are worth mirroring alongside the Section B Switch audit.
  _Upstream PRs_: https://github.com/reactivemarbles/DynamicData/pull/1154, https://github.com/reactivemarbles/DynamicData/pull/1096, https://github.com/reactivemarbles/DynamicData/pull/1095

- [ ] 🔵 **[DD main — test hardening] #1100, #1098, #1101, #1161, #1071**
  _Type: Tests_
  Determinism and coverage improvements (MergeManyChangeSets quiescence waits, SizeLimit dedup, AutoRefresh test rewrite, ToCollection tests, Sum tests). Candidate patterns for our own flaky-test defenses.

Not applicable (repo tooling): #1134 (file nesting), #1159 (internals-visible-to cleanup), #1118 (benchmarks), #1102/#1058/#1128 (CI/branch management).

---

## Section C — [RxUI] Strategic: the 24.0 re-platform

- [ ] 🔴 **[RxUI 24.0 #4382 + #4363 + #4387 + #4418] Re-platform onto `ReactiveUI.Primitives`; custom sinks**
  _Type: Breaking Change / Performance (strategic review, not a port)_
  ReactiveUI now runs on an allocation-conscious engine: System.Reactive optional, `IScheduler`→`ISequencer`, `Unit`→`RxVoid`, subjects→`Signal<T>` family, dual package distributions (`ReactiveUI` vs `ReactiveUI.Reactive`). DynamicData integration moved out of core into `ReactiveUI.Routing`.
  **Why it matters to us**: (a) validates R3Ext's founding thesis; (b) narrows our headline differentiation — "faster and AOT-ready" is now partially claimed upstream; (c) their sink implementations and benchmark methodology are a rich comparison target for `R3Ext.Benchmarks`. Proposed Sprint 2/3 action: benchmark R3Ext vs RxUI 24 on `WhenChanged`-equivalent paths and reflect findings in positioning docs.
  _Upstream PRs_: https://github.com/reactiveui/ReactiveUI/pull/4382, https://github.com/reactiveui/ReactiveUI/pull/4363, https://github.com/reactiveui/ReactiveUI/pull/4387, https://github.com/reactiveui/ReactiveUI/pull/4418

- [ ] 🟡 **[RxUI 24.0 #4413] AOT-friendly `WhenActivated` overloads**
  _Type: New Feature_
  New `IActivatableView.WhenActivated` overloads accepting an `IObservable<object?>` ViewModel-change source, avoiding reflection/trim warnings. R3Ext has no activation system today — this is input to the case-by-case decision on whether to add one, not a fix to port.
  _Upstream PR_: https://github.com/reactiveui/ReactiveUI/pull/4413

---

## Section D — [RxUI] Bug fixes with potential R3Ext analogs

- [ ] 🔴 **[RxUI 24.0 #4381] WhenAnyValue — subscribes to PropertyChanged before reading initial value**
  _Type: Bug Fix_
  A concurrent first change could be lost because the initial value was read before the PropertyChanged subscription existed. Our source-generated `WhenChanged`/`WhenObserved` perform the same initial-read-then-subscribe dance in generated code — the highest-value RxUI audit in this window.
  _Upstream PR_: https://github.com/reactiveui/ReactiveUI/pull/4381

- [ ] 🔴 **[RxUI 23.2 #4351 + 24.0 #4409] Interactions — async-handler scheduling; resume on captured context**
  _Type: Bug Fix (2 PRs)_
  #4351 fixes async interaction-handler scheduling (upstream #4280); #4409 makes interaction task handlers resume on the captured UI context. We ported Interactions (`R3Ext/Interactions/`); both semantics apply directly.
  _Upstream PRs_: https://github.com/reactiveui/ReactiveUI/pull/4351, https://github.com/reactiveui/ReactiveUI/pull/4409

- [ ] 🟡 **[RxUI 24.0 #4361] WaitForDispatcherScheduler — marshal to UI thread from non-UI threads**
  _Type: Bug Fix_
  UI-thread marshaling defect when scheduled from background threads. We advertise automatic UI-thread marshaling on MAUI/Avalonia/Uno — our `TimeProvider`-based marshaling paths should be audited for the analogous early-dispatch case.
  _Upstream PR_: https://github.com/reactiveui/ReactiveUI/pull/4361

- [ ] 🟡 **[RxUI 23.2 #4324] BindCommand — wrong parameter after new ViewModel assigned to View**
  _Type: Bug Fix_
  Stale-parameter capture across ViewModel replacement. We have no `BindCommand`, but our source-generated command bindings re-resolve targets on VM swap — the same staleness class is worth one audit pass over `R3Ext.Bindings.SourceGenerator` output.
  _Upstream PR_: https://github.com/reactiveui/ReactiveUI/pull/4324

- [ ] 🟡 **[RxUI 24.0 #4410] ObservableMixins helpers usable before builder initialization**
  _Type: Bug Fix_
  Init-order robustness for pure helpers. We have no RxApp-style builder/global state by design — expected Architecturally N/A; confirm no hidden init-order dependencies in our static registries (`BindingRegistry`).
  _Upstream PR_: https://github.com/reactiveui/ReactiveUI/pull/4410

Expected Architecturally N/A (no counterpart surface in R3Ext — confirm in Sprint 2): #4349 (activation null load state), #4353 (suspension persistence), #4350 (WPF inherited `DependencyProperty` lookup), #4313/#4337/#4404/#4412 (WPF), #4314/#4339/#4358/#4369 (WinForms), #4318 (Blazor), #4316 (builder `WithCoreServices`), #4427 (testing-package TFMs), #4321/#4320 (sample apps).

---

## Section E — Infrastructure ideas observed upstream (optional)

- [ ] 🔵 **[RxUI #4428/#4429/#4432] PublicApiSharp.Analyzers for public API tracking**
  Upstream now gates public-API changes via analyzer. We hand-maintain parity docs; an API-surface analyzer would mechanize part of what `api-sync-check` does. Candidate future infra sprint, out of scope for this assessment.
- [ ] 🔵 **[DD #1088 / RxUI #4388] Preview/beta release channels** — context for how quickly upstream fixes reach consumers; informs how we weigh "unreleased" Section B items.

---

## Prioritized summary (inventory counts)

| Priority | Count | Items |
|----------|-------|-------|
| 🔴 High | 7 | DD #1076, #1079, #1111, #1120, Switch cluster (#1137/#1139/#1141/#1145), #1132 · RxUI #4381, #4351+#4409, Primitives strategic review |
| 🟡 Medium | 7 | DD #1113, #1153, #1155, #1135 · RxUI #4361, #4324, #4410, #4413 |
| 🔵 Low | 8 | DD #1084, #1085, #1077, #1087, #1080/#1081, #1154/#1096/#1095, test hardening · RxUI/DD infra ideas |
| Expected N/A | ~15 | Platform-specific (WPF/WinForms/Blazor), builder/activation/suspension/routing surfaces we don't have, CI/housekeeping |

**Filter applied** (per project decision 2026-08-13): bug fixes and performance items are presumed-relevant pending Sprint 2 code audit; features (#1135, #4413) and strategic items are case-by-case against R3Ext's AOT/source-gen goals.

---

# Review — 2026-03-30 (Window: November 2025 → March 2026)

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
