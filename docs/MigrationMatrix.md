## DynamicData Port Migration Matrix

Legend:

-   Status: NotStarted | InProgress | Partial | Implemented | Optimized | Deferred
-   Tests: None | Added | Passing | Failing | Partial
-   FollowUp: Brief notes of remaining work / rationale.

| Category                | Feature / Operator                                   | Scope (Cache/List) | Status      | Tests   | FollowUp                                             |
| ----------------------- | ---------------------------------------------------- | ------------------ | ----------- | ------- | ---------------------------------------------------- |
| Aggregation             | Count                                                | List               | Implemented | Passing | Consider cache variant parity if needed              |
| Aggregation             | Sum                                                  | List (int)         | Implemented | Passing | Extend numeric generics if required                  |
| Aggregation             | Max/Min                                              | List               | Optimized   | Passing | Already running stats; consider cache counterparts   |
| Aggregation             | Avg                                                  | List               | Optimized   | Passing | Running sum; add nullable handling later             |
| Aggregation             | StdDev (population)                                  | List               | Optimized   | Passing | Consider sample stddev overload                      |
| Transformation          | TransformMany (dedup)                                | Cache              | Implemented | Passing | Add list variant / observable child list overload    |
| Transformation          | Transform (core overloads)                           | Cache/List         | Implemented | Passing | Key-aware and simple overloads present               |
| Transformation          | TransformAsync                                       | Cache/List         | NotStarted  | None    | Async versions + scheduling                          |
| Transformation          | TransformImmutable                                   | Cache/List         | NotStarted  | None    | Optional performance feature                         |
| Transformation          | TransformSafe                                        | Cache/List         | NotStarted  | None    | Error handling path                                  |
| Transformation          | TransformToTree                                      | Cache              | NotStarted  | None    | Hierarchical build logic                             |
| Transformation          | Cast / Convert                                       | Cache/List         | NotStarted  | None    | Lightweight transform parity                         |
| Transformation          | ChangeKey                                            | Cache              | NotStarted  | None    | Key remap semantics                                  |
| Transformation          | AddKey                                               | List               | NotStarted  | None    | Promote list to keyed changeset                      |
| Filtering               | Filter (static predicate)                            | Cache/List         | Partial     | Passing | Stateful cache FilterOnObservable variant missing    |
| Filtering               | Filter (observable predicate)                        | Cache/List         | Implemented | Passing | Stateful/property-based cache filter pending         |
| Filtering               | FilterOnObservable                                   | Cache/List         | Implemented | Passing |                                                     |
| Filtering               | FilterOnProperty (obsolete)                          | Cache/List         | Deferred    | None    | Replace via AutoRefresh + Filter                     |
| Logical                 | And / Or / Except / Xor                              | Cache              | Implemented | Passing | Dynamic composite list versions needed               |
| Logical                 | And / Or / Except / Xor                              | List               | Partial     | Passing | Composite (IObservableList sources) pending          |
| Logical                 | Combine dynamic collections                          | Cache/List         | NotStarted  | None    | Implement DynamicCombiner equivalents                |
| Grouping                | Group / GroupOn / GroupOnProperty                    | Cache/List         | Partial     | Passing | Optimize diff emission; add property-based overloads |
| Joins                   | Inner / Left / Right / Full (+ Many)                 | Cache              | Implemented | Passing | Optimize diff logic; add Many joins if needed        |
| Property Observation    | WhenValueChanged                                     | Cache              | Implemented | Passing | Add list parity                                      |
| Property Observation    | WhenValueChangedWithPrevious                         | Cache              | Implemented | Passing | Add list parity                                      |
| Property Observation    | WhenPropertyChanged / WhenAnyPropertyChanged         | Cache/List         | NotStarted  | None    | Need unified API                                     |
| Refresh                 | AutoRefresh / AutoRefreshOnObservable                | Cache/List         | Partial     | Passing | List implemented & used; cache AutoRefresh added; OnObservable pending |
| Refresh                 | SuppressRefresh                                      | List               | NotStarted  | None    | Simple filter on reasons                             |
| Refresh                 | InvokeEvaluate                                       | Cache              | NotStarted  | None    | IEvaluateAware support                               |
| Lifecycle               | DisposeMany                                          | Cache/List         | Implemented | Passing |                                                     |
| Lifecycle               | ExpireAfter                                          | Cache              | NotStarted  | None    | Time-based eviction                                  |
| Lifecycle               | LimitSizeTo                                          | List               | Implemented | Passing | Cache size/time combos pending                       |
| Lifecycle               | EnsureUniqueKeys                                     | Cache              | NotStarted  | None    | Enforce uniqueness externally                        |
| Batching                | Batch / BatchIf                                      | Cache              | NotStarted  | None    | Time / conditional batching                          |
| Batching                | BufferIf                                             | List               | Implemented | Passing | Add timeout variant parity                           |
| Paging / Virtualisation | Virtualise / Page / Top                              | List               | Partial     | Passing | Cache side not started                               |
| Query                   | QueryWhenChanged                                     | Cache/List         | Implemented | Passing | Add projection overload variants                     |
| Query                   | ToCollection / ToSortedCollection                    | Cache/List         | NotStarted  | None    | Provide snapshot projection                          |
| Merge                   | MergeMany / MergeChangeSets                          | Cache/List         | Partial     | Passing | Advanced comparer/equality overloads missing         |
| Subscription            | RefCount                                             | List               | Implemented | Passing | Cache counterpart optional                           |
| Subscription            | SubscribeMany                                        | Cache/List         | Implemented | Passing |                                                     |
| Index Ops               | RemoveIndex / Reverse / SkipInitial / StartWithEmpty | List               | NotStarted  | None    | Convenience wrappers                                 |
| Size / TTL              | ToObservableChangeSet (expire/size)                  | Cache/List         | Partial     | Passing | Cache time+size combined missing                     |
| Evaluation              | IncludeUpdateWhen                                    | Cache              | NotStarted  | None    | Selective update emission                            |
| Evaluation              | TrueForAll / TrueForAny                              | Cache              | Implemented | Passing | List variants not done                               |
| Observation             | WatchValue                                           | Cache              | NotStarted  | None    | Single key watch convenience                         |
| Population              | PopulateInto                                         | Cache/List         | NotStarted  | None    | Clone changes into another source                    |
| Sorting                 | Sort (legacy)                                        | Cache/List         | Partial     | Passing | Fused SortAndBind optimization present               |
| Sorting                 | SortAndBind (fused)                                  | Cache/List         | Implemented | Passing | Document perf delta                                  |
| Distinct                | DistinctValues                                       | Cache              | Implemented | Passing | List side missing                                    |
| Distinct                | DistinctValues                                       | List               | NotStarted  | None    | Implement or defer                                   |
| Misc                    | DeferUntilLoaded                                     | Cache/List         | NotStarted  | None    | Subscription deferral logic                          |
| Misc                    | Refresh item (SourceCache.Refresh)                   | Cache              | Implemented | Passing | Parity for list (none upstream)                      |
| Misc                    | Top (wrapper of Virtualise)                          | List               | NotStarted  | None    | Provide convenience overload                         |

### Notes

This matrix will be updated after each port. Initial grouping implementation will be minimal (full reset strategy) and marked Partial until incremental change-emission & property-based regroup features are added. Tests added will move the Tests column to Passing. FollowUp column must be cleared or updated upon completion.
