# KtsuBuild

> .NET build automation tool with semantic versioning, changelog generation, and multi-platform publishing.

## Features

- **Semantic Versioning**: Automatic version calculation based on commit messages and public API diff analysis
- **Changelog Generation**: Auto-generated CHANGELOG.md from git history with multi-level commit filtering
- **License Generation**: Generates LICENSE.md and COPYRIGHT.md from embedded templates
- **Multi-Platform Publishing**: Build and publish for Windows, Linux, and macOS (x64, x86, arm64)
- **NuGet Publishing**: Publish to NuGet.org, GitHub Packages, and custom feeds
- **GitHub Releases**: Create releases with assets, SHA256 hashes, and release notes
- **Winget Manifests**: Generate Windows Package Manager manifests with auto-detection

## Usage

### From Source (Recommended for CI/CD)

Clone and run directly with `dotnet run`:

```bash
git clone --depth 1 https://github.com/ktsu-dev/KtsuBuild.git /tmp/KtsuBuild

# Run the full CI/CD pipeline
dotnet run --project /tmp/KtsuBuild/KtsuBuild.CLI -- ci --workspace .

# Build only
dotnet run --project /tmp/KtsuBuild/KtsuBuild.CLI -- build --workspace .

# Show version info
dotnet run --project /tmp/KtsuBuild/KtsuBuild.CLI -- version show --workspace .
```

### As a Global Tool

```bash
dotnet tool install -g KtsuBuild.CLI

ktsub ci
ktsub build
ktsub version show
```

## CLI Commands

### Global Options

All commands support these options:

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--workspace` | `-w` | The workspace/repository path | Current directory |
| `--configuration` | `-c` | Build configuration (Debug/Release) | Release |
| `--verbose` | `-v` | Enable verbose output | false |

### `ci`

Run the full CI/CD pipeline: metadata update, build, test, pack, publish, and release.

```bash
ktsub ci [options]
```

**Options:**
- `--dry-run`: Preview actions without executing them

**Pipeline steps:**

1. Updates metadata files (VERSION.md, CHANGELOG.md, LICENSE.md, COPYRIGHT.md, AUTHORS.md)
2. Checks version increment (skips release if `[skip ci]` or no meaningful changes)
3. Installs dotnet-script if `.csx` files are present
4. Restores NuGet packages
5. Builds the solution
6. Runs tests with coverage
7. Packs NuGet packages (if ShouldRelease)
8. Publishes executables for all platforms (if ShouldRelease)
9. Generates SHA256 hashes for all artifacts
10. Publishes NuGet packages to configured feeds (if ShouldRelease)
11. Creates a GitHub release with assets (if ShouldRelease)

### `build`

Build workflow: restore, build, and test.

```bash
ktsub build [options]
```

### `release`

Release workflow: pack, publish NuGet packages, and create GitHub release.

```bash
ktsub release [options]
```

**Options:**
- `--dry-run`: Preview actions without executing them

### `version`

Version management commands.

#### `version show`

Display current version information including last tag, calculated version, and increment reason.

```bash
ktsub version show [options]
```

**Output:**
```
Current Version: 1.2.3
Last Tag: v1.2.2
Last Version: 1.2.2
Version Increment: Patch
Reason: Found changes warranting at least a patch version
Is Prerelease: False
```

#### `version bump`

Calculate and display the next version number.

```bash
ktsub version bump [options]
```

#### `version create`

Create or update the VERSION.md file with the calculated version.

```bash
ktsub version create [options]
```

### `metadata`

Metadata file management commands.

#### `metadata update`

Update all metadata files (VERSION.md, CHANGELOG.md, LICENSE.md, COPYRIGHT.md, AUTHORS.md, URL files).

```bash
ktsub metadata update [options]
```

**Options:**
- `--no-commit`: Don't commit changes after updating

#### `metadata license`

Generate LICENSE.md and COPYRIGHT.md files from embedded templates.

```bash
ktsub metadata license [options]
```

#### `metadata changelog`

Generate CHANGELOG.md from git history.

```bash
ktsub metadata changelog [options]
```

### `winget`

Windows Package Manager manifest commands.

#### `winget generate`

Generate Winget manifests for a version.

```bash
ktsub winget generate --version <version> [options]
```

**Options:**
- `--version`, `-V`: The version to generate manifests for (required)
- `--repo`, `-r`: The GitHub repository (owner/repo)
- `--package-id`, `-p`: The package identifier
- `--staging`, `-s`: The staging directory with hashes.txt

#### `winget upload`

Upload manifests to a GitHub release.

```bash
ktsub winget upload --version <version> [options]
```

**Options:**
- `--version`, `-V`: The version to upload manifests for (required)

## Version Increment Tags

Control version increments by including tags in your commit messages:

| Tag | Effect | Example |
|-----|--------|---------|
| `[major]` | Major version bump (1.0.0 -> 2.0.0) | Breaking API changes |
| `[minor]` | Minor version bump (1.0.0 -> 1.1.0) | New features |
| `[patch]` | Patch version bump (1.0.0 -> 1.0.1) | Bug fixes |
| `[pre]` | Prerelease bump (1.0.0 -> 1.0.1-pre.0) | Unstable changes |
| `[skip ci]` | Skip release entirely | Documentation-only changes |

**Examples:**
```bash
git commit -m "[minor] Add new authentication feature"
git commit -m "[patch] Fix null reference in user service"
git commit -m "[major] Redesign public API"
git commit -m "[skip ci] Update documentation"
```

### Automatic Version Detection

If no tag is specified, KtsuBuild automatically determines the version bump by:

1. **Public API analysis**: Diffs C# files for added/removed/modified public types, methods, properties, and constants. Any public API surface change triggers a **minor** bump.
2. **Commit filtering**: Bot commits (dependabot, renovate, etc.) and PR merge commits are excluded from analysis.
3. **Fallback**: Meaningful code changes default to **patch**; trivial changes default to **prerelease**.

## Generated Metadata Files

KtsuBuild generates and maintains these files in the workspace:

| File | Purpose |
|------|---------|
| `VERSION.md` | Contains the current version number |
| `CHANGELOG.md` | Complete changelog with all versions |
| `LATEST_CHANGELOG.md` | Changelog for the current version only (used as release notes) |
| `LICENSE.md` | MIT license with project URL and copyright |
| `COPYRIGHT.md` | Copyright notice with year range and contributors |
| `AUTHORS.md` | List of contributors from git history |
| `PROJECT_URL.url` | Windows shortcut to the project repository |
| `AUTHORS.url` | Windows shortcut to the organization/owner |

## Environment Variables

KtsuBuild reads these environment variables when running in CI/CD:

| Variable | Description |
|----------|-------------|
| `GITHUB_TOKEN` / `GH_TOKEN` | GitHub API token for releases and packages |
| `NUGET_API_KEY` | NuGet.org API key for publishing |
| `KTSU_PACKAGE_KEY` | API key for ktsu.dev package feed |
| `GITHUB_SERVER_URL` | GitHub server URL (default: https://github.com) |
| `GITHUB_REF` | Git reference (branch/tag) |
| `GITHUB_SHA` | Git commit SHA |
| `GITHUB_REPOSITORY` | Repository in owner/repo format |
| `EXPECTED_OWNER` | Expected owner for official builds |

## Build Configuration

The build system automatically determines:

- **IsOfficial**: Whether the repository is the official one (not a fork, matches ExpectedOwner)
- **IsMain**: Whether the build is on the main branch
- **IsTagged**: Whether the current commit is already tagged
- **ShouldRelease**: Whether a release should be created (`IsMain && !IsTagged && IsOfficial`)

## Publish Targets

For executable projects, KtsuBuild publishes to these runtime identifiers:

| Platform | Architectures |
| -------- | ------------- |
| Windows | x64, x86, arm64 |
| Linux | x64, arm64 |
| macOS | x64, arm64 |

Each target produces a self-contained, single-file executable packaged as a ZIP archive with SHA256 hash.

## Examples

### CI/CD Pipeline (GitHub Actions) - Clone from Source

```yaml
name: CI/CD

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: windows-latest
    permissions:
      contents: write
      packages: write
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Clone KtsuBuild
        run: git clone --depth 1 https://github.com/ktsu-dev/KtsuBuild.git "${{ runner.temp }}/KtsuBuild"
        shell: bash

      - name: Run CI Pipeline
        id: pipeline
        shell: pwsh
        env:
          GH_TOKEN: ${{ github.token }}
          NUGET_API_KEY: ${{ secrets.NUGET_KEY }}
          KTSU_PACKAGE_KEY: ${{ secrets.KTSU_PACKAGE_KEY }}
          EXPECTED_OWNER: ktsu-dev
        run: |
          dotnet run --project "${{ runner.temp }}/KtsuBuild/KtsuBuild.CLI" -- ci --workspace "${{ github.workspace }}" --verbose
```

### Local Development

```bash
# Check what version would be released
ktsub version show

# Preview CI actions without making changes
ktsub ci --dry-run

# Build and test locally
ktsub build

# Update metadata files only
ktsub metadata update --no-commit

# Generate winget manifests
ktsub winget generate --version 1.0.0
```

## Architecture

KtsuBuild is organized into three projects:

- **KtsuBuild** - Core library with all business logic, multi-targeted across .NET 5-10 and netstandard2.0/2.1
- **KtsuBuild.CLI** - Console application using System.CommandLine 2.0.3 with Microsoft.Extensions.DependencyInjection
- **KtsuBuild.Tests** - Test suite using MSTest.Sdk with NSubstitute for mocking

All services implement interfaces from the `KtsuBuild.Abstractions` namespace, enabling testability and loose coupling.

## License

This project is licensed under the MIT License - see the LICENSE.md file for details.
