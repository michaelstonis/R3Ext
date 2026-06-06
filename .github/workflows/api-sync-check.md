---
emoji: 🔄
description: Weekly check to keep R3Ext APIs and features in sync with ReactiveUI and DynamicData. Identifies out-of-sync APIs and opens a pull request with the necessary updates.
on:
  schedule: weekly on Monday
  skip-if-match: 'is:pr is:open label:api-sync in:title "[api-sync]"'
permissions:
  contents: read
  issues: read
  pull-requests: read
tools:
  github:
    mode: gh-proxy
    toolsets: [default]
network:
  allowed: [dotnet]
safe-outputs:
  create-pull-request:
    title-prefix: "[api-sync] "
    labels: [api-sync, automation]
    draft: true
    reviewers: [copilot]
    allowed-files:
      - "R3Ext/**/*.cs"
      - "R3.DynamicData/**/*.cs"
      - "R3Ext.Bindings.SourceGenerator/**/*.cs"
      - "R3Ext.Bindings.MauiTargets/**/*.cs"
      - "docs/**/*.md"
      - "README.md"
---

# API Sync Check — ReactiveUI & DynamicData

## Goal

Keep R3Ext aligned with the latest ReactiveUI and DynamicData APIs and features. Each week:

1. Inspect recent commits and releases in both upstream repos.
2. Compare against the current R3Ext implementation.
3. If gaps exist, produce a pull request with the necessary source updates.
4. If everything is already in sync, call `noop`.

## Context

- **R3Ext repo (this repo)** — a reactive MVVM + dynamic-data library built on R3.
  - `R3Ext/` — ReactiveUI-compatible APIs: commands, interactions, bindings, MVVM helpers.
  - `R3.DynamicData/` — DynamicData port: observable caches, lists, operators, change sets.
- **ReactiveUI upstream**: `https://github.com/reactiveui/reactiveui`
- **DynamicData upstream**: `https://github.com/reactivemarbles/DynamicData`

## Steps

### 1 — Gather upstream changes

Use `gh` to inspect each upstream repository.

For **ReactiveUI** (`reactiveui/reactiveui`):
- Fetch commits from the last 7 days on the default branch.
- Fetch the latest release tag and release notes.
- List recently modified public C# files to spot new or changed public APIs (classes, interfaces, public methods/properties).

For **DynamicData** (`reactivemarbles/DynamicData`):
- Fetch commits from the last 7 days on the default branch.
- Fetch the latest release tag and release notes.
- List recently modified public C# files to spot new or changed public APIs.

### 2 — Inspect the current R3Ext implementation

Read relevant files in this repo to understand the current state:
- `R3Ext/Commands/` — RxCommand implementations.
- `R3Ext/Interactions/` — RxInteraction implementations.
- `R3Ext/Bindings/` — binding helpers and source-generator hooks.
- `R3Ext/Extensions/` — observable extension methods.
- `R3.DynamicData/` — DynamicData port.

### 3 — Identify gaps

Compare the upstream public API surface against what R3Ext implements:

- **Missing APIs**: public types or members in the upstream that have no R3Ext equivalent.
- **Signature drift**: existing R3Ext methods whose signatures no longer match the upstream (parameter types, return types, overloads).
- **New features**: functionality added upstream (new operators, new command overloads, new change-set types) not yet present in R3Ext.
- **Removed/deprecated upstream APIs**: flag any that should also be deprecated in R3Ext.

Ignore internal, private, or test-only members. Ignore platform-specific code that does not apply to R3Ext's .NET / MAUI targets.

### 4 — Apply updates

For each identified gap:
- Add or update the corresponding C# source file(s) to bring R3Ext in line with the upstream API.
- Follow the existing code style: XML doc comments for public members, no reflection, AOT-safe patterns.
- Prefer additive changes (new overloads, new types) over breaking changes. If a breaking change is required, mark it with `// TODO: breaking change — <reason>` and note it in the PR body.
- Do not modify test files or benchmark files.

### 5 — Produce output

**If updates were made**: create a pull request using the `create-pull-request` safe output.

The PR body must include:

```
## Summary

Brief description of what changed and why.

## ReactiveUI changes
- List specific upstream commits or release notes that motivated each change.

## DynamicData changes
- List specific upstream commits or release notes that motivated each change.

## Breaking changes (if any)
- Describe any breaking changes or API removals.
```

**If nothing is out of sync**: call `noop` with a short message such as "R3Ext APIs are already in sync with ReactiveUI and DynamicData — no changes needed."

## Safe Outputs

- Use `create-pull-request` to open a draft PR when source changes are produced.
- Use `noop` when R3Ext is already in sync and no code changes are needed.
