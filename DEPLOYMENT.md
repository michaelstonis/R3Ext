# Deployment Guide

This project uses semantic versioning with GitVersion and automated NuGet publishing via GitHub Actions.

## Semantic Versioning

Version numbers follow the format: `MAJOR.MINOR.PATCH[-PRERELEASE]`

### Automatic Version Calculation

GitVersion automatically calculates versions based on:
- Git tags (e.g., `v1.0.0`)
- Branch names
- Commit messages with semver hints

### Branch Versioning Strategy

- **main**: Stable releases (e.g., `1.0.0`, `1.0.1`)
- **develop**: Alpha pre-releases (e.g., `1.1.0-alpha.1`)
- **feature/***: Feature branches (e.g., `1.1.0-feature-name.1`)
- **pull-request/***: PR builds (e.g., `1.1.0-PullRequest1.1`)

### Commit Message Versioning

Control version bumps with commit messages:

```bash
# Bump MAJOR version (breaking change)
git commit -m "feat: new API +semver: major"

# Bump MINOR version (new feature)
git commit -m "feat: add new extension +semver: minor"

# Bump PATCH version (bug fix)
git commit -m "fix: correct behavior +semver: patch"

# No version bump
git commit -m "docs: update readme +semver: none"
```

## Publishing Releases

### 1. Prepare Release

From develop branch:
```bash
# Ensure tests pass
dotnet test

# Merge develop to main (or create PR)
git checkout main
git merge develop
git push origin main
```

### 2. Create Git Tag

```bash
# Tag the release commit
git tag v1.0.0
git push origin v1.0.0
```

### 3. Automated Publishing

When a tag starting with `v` is pushed:
1. GitHub Actions builds the project
2. Runs tests
3. Packs NuGet packages
4. Publishes to NuGet.org (requires `NUGET_API_KEY` secret)
5. Creates GitHub release with artifacts

## NuGet Packages

The following packages are published:

- **R3Ext**: Core library with extensions
- **R3Ext.Bindings.SourceGenerator**: Data binding source generator
- **R3Ext.Bindings.MauiTargets**: MAUI-specific build targets
- **R3Ext.PropertyChanged.SourceGenerator**: PropertyChanged source generator

## GitHub Secrets

Required secrets in repository settings:

- `NUGET_API_KEY`: API key for NuGet.org publishing
  - Get from: https://www.nuget.org/account/apikeys
  - Scope: Push new packages and package versions

## Manual Testing

Test package creation locally:

```bash
# Restore and build
dotnet restore
dotnet build -c Release

# Create packages
dotnet pack R3Ext/R3Ext.csproj -c Release -o ./artifacts
dotnet pack R3Ext.Bindings.SourceGenerator/R3Ext.Bindings.SourceGenerator.csproj -c Release -o ./artifacts
dotnet pack R3Ext.Bindings.MauiTargets/R3Ext.Bindings.MauiTargets.csproj -c Release -o ./artifacts
dotnet pack R3Ext.PropertyChanged.SourceGenerator/R3Ext.PropertyChanged.SourceGenerator.csproj -c Release -o ./artifacts

# Inspect packages
dotnet nuget verify ./artifacts/*.nupkg
```

## Development Workflow

### Feature Development
```bash
git checkout develop
git checkout -b feature/my-feature
# ... make changes ...
git commit -m "feat: add my feature +semver: minor"
git push origin feature/my-feature
# Create PR to develop
```

### Bug Fixes
```bash
git checkout develop
git checkout -b fix/bug-description
# ... make changes ...
git commit -m "fix: resolve issue +semver: patch"
git push origin fix/bug-description
# Create PR to develop
```

### Breaking Changes
```bash
git checkout develop
# ... make changes ...
git commit -m "feat!: breaking API change +semver: major"
# or
git commit -m "feat: breaking API change

BREAKING CHANGE: Description of breaking change
+semver: major"
```

## Version Examples

| Branch | Commits Since Tag | Example Version |
|--------|------------------|-----------------|
| main (tagged v1.0.0) | 0 | 1.0.0 |
| main | 3 | 1.0.1 |
| develop | 5 | 1.1.0-alpha.5 |
| feature/new-api | 2 | 1.1.0-new-api.2 |
| pull-request/123 | 1 | 1.1.0-PullRequest123.1 |

## Troubleshooting

### Packages not publishing
- Check GitHub Actions logs
- Verify `NUGET_API_KEY` secret is set correctly
- Ensure tag format is `vX.Y.Z`

### Version not incrementing
- Check GitVersion.yml configuration
- Use `+semver:` hints in commit messages
- Ensure proper branch naming

### Test local versioning
```bash
# Install GitVersion tool
dotnet tool install --global GitVersion.Tool

# Calculate version
dotnet-gitversion
```
