# GitHub Actions Workflows

This directory contains the CI/CD workflows for Gryd.Pipeline.

> **For release instructions**, see [RELEASING.md](../../RELEASING.md) in the repository root.

## Workflows Overview

### ci.yml - Continuous Integration

Runs on every pull request and push to `main`:
- Restores dependencies
- Builds in Release configuration
- Runs all tests

Does NOT create or publish packages.

### publish.yml - Publish to NuGet

Triggers on version tags (e.g., `v1.0.0`):
- Extracts version from Git tag
- Builds in Release configuration
- Packs both projects with extracted version
- Publishes to NuGet.org

**Requires:** `NUGET_API_KEY` GitHub Secret

**Publishes:**
- Gryd.Pipeline
- Gryd.Pipeline.Providers.OpenRouter

## Setup

### Configure NuGet API Key

1. Get API key: [nuget.org/account/apikeys](https://www.nuget.org/account/apikeys)
2. In GitHub: **Settings** → **Secrets and variables** → **Actions**
3. Create new secret:
   - Name: `NUGET_API_KEY`
   - Value: Your NuGet API key

That's it! See [RELEASING.md](../../RELEASING.md) for how to create releases.

## Maintenance

### Update .NET SDK Version

1. Update `dotnet-version` in both workflow files
2. Update `TargetFramework` in both `.csproj` files
3. Test locally before pushing

### Modify Packaging

1. Test locally: `dotnet pack --configuration Release`
2. Update `publish.yml` workflow
3. Test with a pre-release tag: `v0.0.1-test`

## Troubleshooting

**Workflow fails with "401 Unauthorized":**
- NuGet API key not configured or invalid
- Check Settings → Secrets → NUGET_API_KEY

**Workflow doesn't trigger on tag:**
- Ensure tag starts with `v` (e.g., `v1.0.0`)
- Check Actions tab for any errors

**Build/test failures:**
- Run locally: `dotnet build --configuration Release && dotnet test`
- Check for environment-specific issues

For detailed troubleshooting, see [RELEASING.md](../../RELEASING.md).
