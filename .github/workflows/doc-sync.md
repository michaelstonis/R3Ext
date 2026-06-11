---
emoji: 📚
description: Daily documentation sync — identifies doc files out of sync with recent code changes and opens a pull request with the necessary updates.
on:
  schedule: daily around 8am UTC
permissions:
  contents: read
  issues: read
  pull-requests: read
strict: true
network:
  allowed: [defaults, github]
tools:
  github:
    mode: gh-proxy
    toolsets: [default]
safe-outputs:
  create-pull-request:
    title-prefix: "[doc-sync] "
    branch-prefix: doc-sync/
    labels: [documentation]
    draft: true
    if-no-changes: ignore
    allowed-files:
      - "*.md"
      - "docs/**/*.md"
---

# Documentation Sync

## Goal

Keep the repository documentation accurate and up to date by identifying markdown files that are out of sync with recent code changes, then opening a pull request with the necessary corrections.

## Steps

### 1. Discover recent code changes

Use `gh` to list commits from the past 7 days on the default branch:

```
gh api repos/{owner}/{repo}/commits?since=<7-days-ago>&per_page=100
```

For each commit, retrieve the list of files changed. Focus only on non-documentation source files (`.cs`, `.csproj`, `.sln`, `.props`, `.targets`, `.json`). Collect a deduplicated list of changed source paths.

### 2. Identify documentation files

The documentation files in this repository are:

- `README.md` — primary project overview, features, installation, quick start, API reference
- `DEPLOYMENT.md` — deployment and release process
- `AOT_REFLECTION_ISSUES.md` — known AOT/trimming issues and workarounds
- `docs/ClosureEliminationPlan.md` — plan and progress for eliminating closures in R3.DynamicData
- `docs/ClosureEliminationStatus.md` — current status of the closure elimination effort
- `docs/LibraryParity.md` — parity tracking between R3Ext and upstream libraries
- `docs/MigrationMatrix.md` — migration guidance matrix
- `docs/UpstreamChangesReview.md` — review of changes in upstream dependencies

Read each documentation file using the `edit` or `bash` tool to understand its current content.

### 3. Correlate code changes to documentation

For each changed source file, determine which documentation files describe, reference, or track its functionality. Use the following heuristics:

- Changes in `R3.DynamicData/` → correlate with `docs/ClosureEliminationPlan.md`, `docs/ClosureEliminationStatus.md`, `docs/LibraryParity.md`
- Changes in `R3Ext/`, `R3Ext.Bindings.SourceGenerator/`, `R3Ext.Bindings.MauiTargets/` → correlate with `README.md`, `docs/LibraryParity.md`
- Changes to `.csproj`, `Directory.Build.props`, `GitVersion.yml` → correlate with `DEPLOYMENT.md`, `README.md`
- New public APIs (classes, methods) → may need documentation in `README.md`
- AOT-related changes → correlate with `AOT_REFLECTION_ISSUES.md`

### 4. Identify out-of-sync sections

For each correlated documentation file, read the relevant source code and compare it against what the documentation describes. Identify specific sections that are inaccurate, incomplete, or missing based on the actual code. Examples:

- A status document references a percentage or count that no longer matches reality
- A doc lists operators or APIs that have been renamed, added, or removed
- A migration or parity matrix is missing recently added or removed entries
- A plan document has completed items still marked as pending

### 5. Apply documentation updates

For each out-of-sync section, use the `edit` tool to apply precise, targeted corrections. Keep changes minimal and focused — do not rewrite sections that remain accurate. Preserve the existing structure, tone, and formatting of each document.

### 6. Produce the pull request

After applying all updates, emit a `create_pull_request` output describing:

- Which documents were changed and why
- A concise summary of each correction keyed to the source commits that triggered it

If no documentation is out of sync after review, call `noop` with a brief explanation (e.g. "No documentation updates required for changes in the past 7 days.").

## Safe Outputs

- Use `create-pull-request` to open a draft PR with the documentation updates.
- Use `noop` when no changes are needed.
