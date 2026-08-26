# KtsuBuild

> .NET build automation tool with semantic versioning, changelog generation, and multi-platform publishing.

[![License](https://img.shields.io/github/license/ktsu-dev/KtsuBuild.svg?label=License&logo=nuget)](LICENSE.md)
[![NuGet Version](https://img.shields.io/nuget/v/ktsu.KtsuBuild.Tool?label=Stable&logo=nuget)](https://nuget.org/packages/ktsu.KtsuBuild.Tool)
[![NuGet Version](https://img.shields.io/nuget/vpre/ktsu.KtsuBuild.Tool?label=Latest&logo=nuget)](https://nuget.org/packages/ktsu.KtsuBuild.Tool)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ktsu.KtsuBuild.Tool?label=Downloads&logo=nuget)](https://nuget.org/packages/ktsu.KtsuBuild.Tool)
[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/ktsu-dev/KtsuBuild?label=Commits&logo=github)](https://github.com/ktsu-dev/KtsuBuild/commits/main)
[![GitHub contributors](https://img.shields.io/github/contributors/ktsu-dev/KtsuBuild?label=Contributors&logo=github)](https://github.com/ktsu-dev/KtsuBuild/graphs/contributors)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/ktsu-dev/KtsuBuild/dotnet.yml?branch=main&label=Build&logo=github)](https://github.com/ktsu-dev/KtsuBuild/actions)

## Features

- **Semantic Versioning**: Automatic version calculation based on commit messages and public API diff analysis
- **Changelog Generation**: Auto-generated CHANGELOG.md from git history with multi-level commit filtering
- **License Generation**: Generates LICENSE.md and COPYRIGHT.md from embedded templates
- **Multi-Platform Publishing**: Build and publish for Windows, Linux, and macOS (x64, x86, arm64)
- **NuGet Publishing**: Publish to NuGet.org, GitHub Packages, and custom feeds
- **GitHub Releases**: Create releases with assets, SHA256 hashes, and release notes
- **Winget Manifests**: Generate Windows Package Manager manifests with auto-detection

## Usage

### As a .NET Tool (Recommended)

```bash
dotnet tool install -g ktsu.KtsuBuild.Tool

ktsubuild ci --workspace .
ktsubuild build --workspace .
ktsubuild version show --workspace .
```

In CI, prefer `--tool-path` over `-g`: the install is self-contained and the command does
not depend on the runner having the global tools directory on `PATH`.

```bash
dotnet tool install ktsu.KtsuBuild.Tool --tool-path ./.ktsubuild
./.ktsubuild/ktsubuild ci --workspace .
```

### From Source

```bash
git clone --depth 1 https://github.com/ktsu-dev/KtsuBuild.git /tmp/KtsuBuild

# Run the full CI/CD pipeline
dotnet run --project /tmp/KtsuBuild/KtsuBuild.Tool -- ci --workspace .

# Build only
dotnet run --project /tmp/KtsuBuild/KtsuBuild.Tool -- build --workspace .

# Show version info
dotnet run --project /tmp/KtsuBuild/KtsuBuild.Tool -- version show --workspace .
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
ktsubuild ci [options]
```

**Options:**
- `--dry-run`: Preview actions without executing them
- `--version-bump`: Force a specific version bump type (auto, patch, minor, major)
- `--no-test`: Skip the test step, for a pipeline that runs tests elsewhere
- `--no-release`: Skip the release step, leaving the release to a later step or job

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

`--no-test` and `--no-release` exist so a workflow can split the pipeline across jobs without
losing the parts that only `ci` performs: the metadata update and commit, the repository topics,
the version gate that makes `[skip ci]` work, and the `version`, `release_hash`, `should_release`,
and `build_skipped` step outputs. A workflow that fans tests across a matrix runs
`ktsubuild ci --no-test --no-release` for everything around the tests, then `ktsubuild release`
once its quality gate passes.

`--no-release` stops this run from releasing. It does not change what the `should_release` output
reports, because the job that reads that output is the one performing the release.

### `build`

Build workflow: restore, build, and test.

```bash
ktsubuild build [options]
```

**Options:**
- `--no-test`: Skip the test step, running restore and build only

### `test`

Test project discovery and execution, for splitting a test run across several machines.

#### `test list`

List the workspace's test projects as a single line of JSON on stdout.

```bash
ktsubuild test list [options]
```

**Output:**
```json
[{"project":"tests/Foo.Tests/Foo.Tests.csproj","platform":"neutral"},{"project":"tests/Bar.Tests/Bar.Tests.csproj","platform":"windows"}]
```

`project` is a forward-slash path relative to the workspace, on every host. `platform` is `neutral`, `windows`, or `ios`. Entries are sorted by `project`.

The list covers every test project regardless of the current host, unlike `build` and `ci`, which test only what the host can build. A `windows` project appears on Linux and an `ios` project appears on Windows. The caller decides which host and project pairs are valid, which is what lets one machine enumerate a matrix that other machines run.

Errors are written to stdout, not stderr, so check the exit code before parsing the output. Exit code 0 means stdout holds the JSON line, and 1 means it holds an error message.

#### `test run`

Run one test project with coverage, instead of every test project in the workspace.

```bash
ktsubuild test run --project <path> [options]
```

**Options:**
- `--project`: Path to the test project, relative to the workspace or absolute (required)
- `--no-build`: Skip building before running the tests, for a caller that has already built this project

The project isn't checked against the `test list` results. Pointing this at a project the host can't build, or at one that isn't a test project, fails during the test run rather than with a message naming the cause, so filter with `test list` first.

`--no-build` exists so a CI matrix can build once per platform and reuse that output across every
test cell on the same platform, instead of rebuilding the same tree in each cell. The caller has to
guarantee what the flag asserts: the project must already be built for the configuration being
tested, in the same workspace path. `dotnet test --no-build` reads `obj/project.assets.json`, and
that file holds absolute paths, so output moved between machines or paths will not resolve.

#### `test all`

Restore, build, and test every test project the host can build, in one `dotnet test` invocation
across the workspace.

```bash
ktsubuild test all [options]
```

Projects the host cannot build are skipped and named, with the reason, before anything is built.
The single invocation reports every project's results itself, so there is nothing left for this
command to accumulate: a failure in one project shows up in that one report rather than stopping
the projects around it.

The invocation asks `ktsu.Sdk` to pin every project's build to the host runtime by setting
`-p:KtsuHostRuntimeOnly=true`, an opt-in property the Sdk turns into a per-project runtime
identifier. A workspace-wide run cannot take a runtime identifier directly: passing
`-p:RuntimeIdentifier` on a solution build fails with `NETSDK1134`, which is why the property
exists instead of the identifier itself. On a repository whose Sdk version does not know the
property, the flag is inert and the run is runtime-agnostic, exactly as it always was.

The pin is the point once it applies. Without it, a test project's output carries the native
assets for every runtime identifier its packages ship, which for a repository using the ImGui
packages is sixteen of them, Android included. Measured on ImGuiApp, the smallest test project's
output went from 115 MB to 39 MB with the pin, and its tests passed either way. That copying is
what makes a test run slow, and it costs most on Windows, where file writes are several times
slower than on Linux. An earlier version of `test all` paid for the pin by running `dotnet test`
once per project, which cost more in repeated test host startups than the copying it saved
(measured on ImGuiApp: 21.4 minutes against 22.5 unpinned on Windows, 12.7 against 8.0 on Ubuntu),
which is why the pin now rides a single invocation instead of a loop.

This is for testing, not for shipping. `release` still publishes for every runtime it names, and
`build` stays runtime-agnostic, because a solution build cannot take a runtime identifier at all.

##### Excluding projects

`--exclude` takes a glob matched against each project's path as the solution records it, and is
repeatable. Matching projects are left out of the test run:

```bash
ktsubuild test all --exclude "**/*.UITests/*"
```

The exclusion works by writing a solution filter and testing that, so the run stays a single
`dotnet test` invocation. Looping over the remaining projects instead would cost one test host
startup each, which is the trade this command already measured and rejected.

Matching is case insensitive and runs against the forward-slash form of the path, so one pattern
works whichever platform wrote the solution. `**` crosses directory separators and `*` does not.

Every excluded project is named in the log and the closing summary counts only what ran, because a
project silently dropped from a run is indistinguishable from a run that passed. A pattern matching
nothing is reported too, at information level rather than as a warning, because one workflow file
shared across every repository passes the same patterns everywhere and matching nothing is the
ordinary case for a repository that has no such projects yet.

Use this when a suite is worth running on one platform but not on all of them. ImGuiApp excludes
its UI suites on Windows: they are the entire cost of that job, and what they exercise is a managed
CPU rasterizer that measures the same on both operating systems.

### `release`

Release workflow: pack, publish NuGet packages, and create GitHub release.

```bash
ktsubuild release [options]
```

**Options:**
- `--dry-run`: Preview actions without executing them

`release` resolves the version the same way `ci` does, from the repository's tags and commit
history, and publishes against the current commit. It also honors the version gate, so a run whose
commits all carry `[skip ci]` publishes nothing.

Targeting the current commit matters in a split pipeline. When `ci --no-test --no-release` runs
first, it commits the updated metadata, so the current commit is the one whose VERSION.md carries
the version being published. Targeting the commit that triggered the run instead would tag a tree
that predates the bump.

### `version`

Version management commands.

#### `version show`

Display current version information including last tag, calculated version, and increment reason.

```bash
ktsubuild version show [options]
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
ktsubuild version bump [options]
```

#### `version create`

Create or update the VERSION.md file with the calculated version.

```bash
ktsubuild version create [options]
```

### `metadata`

Metadata file management commands.

#### `metadata update`

Update all metadata files (VERSION.md, CHANGELOG.md, LICENSE.md, COPYRIGHT.md, AUTHORS.md, URL files).

```bash
ktsubuild metadata update [options]
```

**Options:**
- `--no-commit`: Don't commit changes after updating

#### `metadata license`

Generate LICENSE.md and COPYRIGHT.md files from embedded templates.

```bash
ktsubuild metadata license [options]
```

#### `metadata changelog`

Generate CHANGELOG.md from git history.

```bash
ktsubuild metadata changelog [options]
```

### `winget`

Windows Package Manager manifest commands.

#### `winget generate`

Generate Winget manifests for a version.

```bash
ktsubuild winget generate --version <version> [options]
```

**Options:**
- `--version`, `-V`: The version to generate manifests for (required)
- `--repo`, `-r`: The GitHub repository (owner/repo)
- `--package-id`, `-p`: The package identifier
- `--staging`, `-s`: The staging directory with hashes.txt

#### `winget upload`

Upload manifests to a GitHub release.

```bash
ktsubuild winget upload --version <version> [options]
```

**Options:**
- `--version`, `-V`: The version to upload manifests for (required)

## Version Bump Control

KtsuBuild determines version bumps through three methods (in order of precedence):

### 1. CLI Option (Highest Priority)

Use `--version-bump` to explicitly control the version increment:

```bash
# Force a major version bump
ktsubuild ci --version-bump major

# Force a minor version bump
ktsubuild ci --version-bump minor

# Force a patch version bump
ktsubuild ci --version-bump patch

# Use automatic detection (default)
ktsubuild ci --version-bump auto
```

This option is also available in GitHub Actions workflow_dispatch:

```yaml
workflow_dispatch:
  inputs:
    version-bump:
      type: choice
      options: [auto, patch, minor, major]
```

### 2. Commit Message Tags

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

### 3. Automatic Version Detection (Lowest Priority)

If no CLI option or commit tag is specified, KtsuBuild automatically determines the version bump by:

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
  workflow_dispatch:
    inputs:
      version-bump:
        description: 'Version bump type'
        required: false
        default: 'auto'
        type: choice
        options:
          - auto
          - patch
          - minor
          - major

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
          $versionBump = "${{ github.event.inputs.version-bump }}"

          # Build arguments array - only add --version-bump if explicitly set (backward compatible)
          $args = @("ci", "--workspace", "${{ github.workspace }}", "--verbose")
          if (![string]::IsNullOrEmpty($versionBump) -and $versionBump -ne "auto") {
            $args += @("--version-bump", $versionBump)
          }

          & dotnet run --project "${{ runner.temp }}/KtsuBuild/KtsuBuild.CLI" -- @args
```

### Local Development

```bash
# Check what version would be released
ktsubuild version show

# Preview CI actions without making changes
ktsubuild ci --dry-run

# Force a specific version bump
ktsubuild ci --version-bump minor

# Build and test locally
ktsubuild build

# Update metadata files only
ktsubuild metadata update --no-commit

# Generate winget manifests
ktsubuild winget generate --version 1.0.0
```

## Architecture

KtsuBuild is organized into three projects:

- **KtsuBuild** - Core library with all business logic, multi-targeted across .NET 5-10 and netstandard2.0/2.1
- **KtsuBuild.Tool** - CLI using System.CommandLine 2.0.3 with Microsoft.Extensions.DependencyInjection, packed as the `ktsu.KtsuBuild.Tool` .NET tool (`ktsubuild` command)
- **KtsuBuild.Tests** - Test suite using MSTest.Sdk with NSubstitute for mocking

All services implement interfaces from the `KtsuBuild.Abstractions` namespace, enabling testability and loose coupling.

## License

This project is licensed under the MIT License - see the LICENSE.md file for details.
