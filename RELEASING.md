# CI/CD & Release Process

This document explains how versioning, continuous integration, and package publishing work for Gryd.Pipeline.

## Quick Start

**First time setup:**
1. Get a NuGet API key from [nuget.org/account/apikeys](https://www.nuget.org/account/apikeys)
2. Add it to GitHub: **Settings** → **Secrets and variables** → **Actions** → **New secret**
   - Name: `NUGET_API_KEY`
   - Value: Your API key

**To release a new version:**
```bash
git checkout main
git pull origin main
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0
```

GitHub Actions will automatically build, test, and publish both packages to NuGet.org.

---

## Table of Contents

- [Quick Start](#quick-start)
- [Overview](#overview)
- [Versioning Strategy](#versioning-strategy)
- [CI/CD Workflows](#cicd-workflows)
- [How to Release](#how-to-release)
- [Pre-Release Versions](#pre-release-versions)
- [Troubleshooting](#troubleshooting)

## Overview

This repository uses **GitHub Actions** for CI/CD and follows **Semantic Versioning (SemVer)** with **Git tags as the source of truth** for version numbers.

**Key Principles:**

- No hardcoded version numbers in `.csproj` files
- Version is derived from Git tags at build time
- Two workflows: CI (build/test) and Publish (package/deploy)
- Each project produces its own NuGet package

## Versioning Strategy

### Semantic Versioning (SemVer)

We follow [Semantic Versioning 2.0.0](https://semver.org/):

```
MAJOR.MINOR.PATCH[-PRERELEASE]
```

- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes (backward compatible)
- **PRERELEASE**: Optional suffix (e.g., `-alpha.1`, `-beta.2`, `-rc.1`)

### Git Tags as Source of Truth

Version numbers are stored as Git tags in the format `v{version}`:

```bash
v1.0.0          # Production release
v1.2.3-alpha.1  # Pre-release
v2.0.0-rc.1     # Release candidate
```

**Important:** The `v` prefix is required for the tag but is stripped when generating the package version.

### No Hardcoded Versions

The `.csproj` files do NOT contain `<Version>`, `<PackageVersion>`, or `<AssemblyVersion>` elements. These are injected at build time by the publish workflow.

## CI/CD Workflows

### 1. CI Workflow (`ci.yml`)

**Purpose:** Validate code quality on every pull request and push to main.

**Trigger:**
- Pull requests targeting `main`
- Pushes to `main` branch

**Steps:**
1. Checkout code
2. Setup .NET SDK (8.0.x)
3. Restore dependencies
4. Build in Release configuration
5. Run all tests

**What it does NOT do:**
- Does not create packages
- Does not publish to NuGet
- Does not require version tags

**When it runs:**
- On every PR opened, updated, or synchronized
- On every push to main (e.g., merged PRs)

### 2. Publish Workflow (`publish.yml`)

**Purpose:** Build, pack, and publish NuGet packages when a version tag is pushed.

**Trigger:**
- Push of tags matching `v*` (e.g., `v1.0.0`, `v2.3.4-beta.1`)

**Steps:**
1. Checkout code
2. Setup .NET SDK
3. Extract version from tag (strips `v` prefix)
4. Restore dependencies
5. Build in Release configuration
6. Pack both projects:
   - `Gryd.Pipeline`
   - `Gryd.Pipeline.Providers.OpenRouter`
7. Publish both packages to NuGet.org

**Required Secret:**
- `NUGET_API_KEY` - Your NuGet API key (stored in GitHub Secrets)

## How to Release

### Prerequisites

1. Ensure all changes are committed and pushed to `main`
2. Ensure CI workflow passes on `main`
3. Ensure you have a NuGet API key configured in GitHub Secrets

### Release Process

#### Step 1: Decide on Version Number

Follow SemVer conventions:

- **Breaking changes?** → Increment MAJOR (e.g., `1.5.2` → `2.0.0`)
- **New features?** → Increment MINOR (e.g., `1.5.2` → `1.6.0`)
- **Bug fixes only?** → Increment PATCH (e.g., `1.5.2` → `1.5.3`)

#### Step 2: Create and Push the Tag

```bash
# Create an annotated tag (recommended)
git tag -a v1.2.3 -m "Release version 1.2.3"

# Push the tag to GitHub
git push origin v1.2.3
```

**Alternative:** Create a lightweight tag
```bash
git tag v1.2.3
git push origin v1.2.3
```

#### Step 3: Monitor the Workflow

1. Go to **Actions** tab in GitHub
2. Watch the "Publish to NuGet" workflow
3. Verify it completes successfully
4. Check [NuGet.org](https://www.nuget.org/packages/Gryd.Pipeline) for your packages

#### Step 4: Create a GitHub Release (Optional)

1. Go to **Releases** in GitHub
2. Click **Draft a new release**
3. Select the tag you just pushed
4. Add release notes describing changes
5. Publish the release

### Example: Releasing v1.0.0

```bash
# Ensure you're on main and up to date
git checkout main
git pull origin main

# Create the tag
git tag -a v1.0.0 -m "Release version 1.0.0 - Initial stable release"

# Push the tag
git push origin v1.0.0

# Watch the workflow in GitHub Actions
# https://github.com/gcamara/gryd.pipeline/actions
```

## Pre-Release Versions

Pre-release versions allow you to publish experimental or beta packages to NuGet without affecting the stable release channel.

### Pre-Release Tag Format

```
v{MAJOR}.{MINOR}.{PATCH}-{PRERELEASE}
```

**Common pre-release identifiers:**
- `alpha` - Early development, unstable
- `beta` - Feature complete, testing phase
- `rc` - Release candidate, production-ready pending final testing

**Examples:**
```bash
v1.0.0-alpha.1
v1.0.0-alpha.2
v1.0.0-beta.1
v1.0.0-rc.1
v1.0.0          # Final release
```

### Publishing a Pre-Release

```bash
# Create pre-release tag
git tag -a v1.5.0-beta.1 -m "Beta release for 1.5.0"

# Push to GitHub
git push origin v1.5.0-beta.1
```

### Installing Pre-Release Packages

Users can install pre-release versions explicitly:

```bash
dotnet add package Gryd.Pipeline --version 1.5.0-beta.1
```

Or include pre-release versions in search:

```bash
dotnet add package Gryd.Pipeline --prerelease
```

### Pre-Release Best Practices

1. **Use sequential numbers**: `alpha.1`, `alpha.2`, etc.
2. **Progress through stages**: `alpha` → `beta` → `rc` → stable
3. **Document breaking changes**: Pre-releases can have breaking changes
4. **Test thoroughly**: Use pre-releases to gather feedback

## Troubleshooting

### Publish Workflow Fails

**Problem:** Workflow fails with "Source parameter was not specified"

**Solution:**
- This is a workflow configuration issue
- The `--source` parameter must come before `--api-key` in the `dotnet nuget push` command
- This has been fixed in the publish.yml workflow

---

**Problem:** Workflow fails with "401 Unauthorized" or API key error

**Solution:**
1. Verify `NUGET_API_KEY` is set in GitHub Secrets
2. Check that the API key has push permissions
3. Ensure the API key hasn't expired

---

**Problem:** Workflow fails with "duplicate package version"

**Solution:**
- You cannot re-publish the same version to NuGet
- Delete the tag locally and remotely, fix issues, create a new tag
```bash
# Delete local tag
git tag -d v1.2.3

# Delete remote tag
git push origin :refs/tags/v1.2.3
```

---

**Problem:** Wrong version number in published package

**Solution:**
- Check that your tag follows the `v{version}` format exactly
- Ensure no spaces or invalid characters in the tag
- The workflow strips the `v` prefix automatically

### CI Workflow Fails

**Problem:** Build fails on CI but works locally

**Solution:**
1. Ensure all dependencies are committed (no local-only packages)
2. Check that target framework is compatible (net8.0)
3. Run `dotnet clean` and `dotnet restore` locally
4. Try running `dotnet build --configuration Release` locally

---

**Problem:** Tests fail on CI but pass locally

**Solution:**
1. Check for environment-specific assumptions (file paths, environment variables)
2. Ensure tests don't rely on local state or databases
3. Run tests in Release mode locally: `dotnet test --configuration Release`

### Version Number Issues

**Problem:** Need to change version after publishing

**Solution:**
- You cannot change a published version on NuGet
- Publish a new patch version with the fix

---

**Problem:** Need to unpublish a package

**Solution:**
- You can only **unlist** packages on NuGet (they remain available but hidden)
- Go to NuGet.org → Your package → Manage → Unlist

## Configuration Reference

### GitHub Secrets Required

| Secret Name | Description | Where to Get |
|-------------|-------------|--------------|
| `NUGET_API_KEY` | NuGet API key with push permissions | [nuget.org/account/apikeys](https://www.nuget.org/account/apikeys) |

### NuGet Metadata in .csproj

Both `.csproj` files include:

```xml
<PackageId>Gryd.Pipeline</PackageId>
<Description>...</Description>
<Authors>Gryd</Authors>
<RepositoryUrl>https://github.com/gcamara/gryd.pipeline</RepositoryUrl>
<RepositoryType>git</RepositoryType>
<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>
<PackageReadmeFile>README.md</PackageReadmeFile>
<PackageTags>...</PackageTags>
<ContinuousIntegrationBuild Condition="'$(GITHUB_ACTIONS)' == 'true'">true</ContinuousIntegrationBuild>
```

### Supported .NET Versions

- Current target: **net8.0**
- CI/CD uses: **.NET 8.0.x SDK**

To update, modify:
1. `TargetFramework` in both `.csproj` files
2. `dotnet-version` in both workflow files

## Additional Resources

- [Semantic Versioning](https://semver.org/)
- [NuGet Package Versioning](https://docs.microsoft.com/en-us/nuget/concepts/package-versioning)
- [GitHub Actions for .NET](https://docs.github.com/en/actions/automating-builds-and-tests/building-and-testing-net)
- [Creating NuGet Packages](https://docs.microsoft.com/en-us/nuget/create-packages/creating-a-package-dotnet-cli)
