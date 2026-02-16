# CLAUDE.md

This file provides guidance to Claude Code when working with the KtsuBuild project.

## Project Overview

KtsuBuild is a **.NET build automation CLI tool** that replaces the legacy PSBuild PowerShell module. It provides semantic versioning, metadata generation, multi-platform publishing, and Winget manifest generation for the ktsu.dev ecosystem of 79+ .NET projects.

## Solution Structure

```
KtsuBuild.sln
├── .serena/             # Serena MCP plugin configuration
│   ├── project.yml      # C# language server and project settings
│   └── .gitignore       # Ignores cache directory
├── KtsuBuild/           # Core library (multi-targeted)
├── KtsuBuild.CLI/       # Console application (net10.0)
└── KtsuBuild.Tests/     # Test project (net9.0, MSTest.Sdk)
```

### KtsuBuild (Core Library)

Multi-targeted: net10.0, net9.0, net8.0, net7.0, net6.0, net5.0, netstandard2.0, netstandard2.1

| Namespace | Purpose |
|-----------|---------|
| `KtsuBuild.Abstractions` | 10 interfaces defining all service contracts |
| `KtsuBuild.Configuration` | Build configuration from environment/options |
| `KtsuBuild.DotNet` | .NET SDK operations (restore, build, test, pack, publish) |
| `KtsuBuild.Git` | Git operations, version calculation, commit analysis |
| `KtsuBuild.Metadata` | Metadata generation (VERSION, CHANGELOG, LICENSE, AUTHORS) |
| `KtsuBuild.Publishing` | NuGet publishing, GitHub releases |
| `KtsuBuild.Utilities` | Process runner, logger, environment setup |
| `KtsuBuild.Winget` | Winget manifest generation and upload |

### KtsuBuild.CLI (Console Application)

Uses `System.CommandLine` 2.0.3 with DI via `Microsoft.Extensions.DependencyInjection`.

### KtsuBuild.Tests

Uses MSTest.Sdk with Microsoft.Testing.Platform runner and NSubstitute for mocking.

## Build and Test Commands

```bash
# Build
dotnet build

# Run tests (67 tests)
dotnet test

# Run CLI directly
dotnet run --project KtsuBuild.CLI -- <command> [options]

# Example: dry-run CI against another project
dotnet run --project KtsuBuild.CLI -- ci --workspace /path/to/project --dry-run
```

## CLI Command Tree

```
ktsubuild
├── ci            # Full CI/CD pipeline (metadata + build + test + release)
│   Options: --workspace, --configuration, --verbose, --dry-run, --version-bump
├── build         # Build workflow (restore + build + test)
│   Options: --workspace, --configuration, --verbose
├── release       # Release workflow (pack + publish + GitHub release)
│   Options: --workspace, --configuration, --verbose, --dry-run
├── version
│   ├── show      # Display version info (version, tag, increment, reason)
│   ├── bump      # Calculate and print next version
│   └── create    # Write VERSION.md
├── metadata
│   ├── update    # Update all metadata files (--no-commit)
│   ├── license   # Generate LICENSE.md and COPYRIGHT.md
│   └── changelog # Generate CHANGELOG.md
└── winget
    ├── generate  # Generate Winget manifests (--version, --repo, --package-id, --staging)
    └── upload    # Upload manifests to GitHub release (--version)
```

## Architecture

### Key Design Patterns

- **Interface-first**: All services implement interfaces from `KtsuBuild.Abstractions`
- **Constructor injection**: Services accept dependencies via constructors
- **DI at entry point only**: `Program.cs` resolves `IProcessRunner` and `IBuildLogger` from DI; other services are created manually in command handlers
- **CancellationToken everywhere**: All async methods accept `CancellationToken`
- **Polyfill for downlevel TFMs**: Uses `Polyfill` package with `#if !NET10_0_OR_GREATER` guards

### Service Dependency Graph

```
Program.Main() → DI Container
├── IProcessRunner → ProcessRunner
└── IBuildLogger → BuildLogger

Command handlers create services manually:
├── GitService(IProcessRunner, IBuildLogger)
├── GitHubService(IProcessRunner, IGitService, IBuildLogger)
├── BuildConfigurationProvider(IGitService, IGitHubService)
├── DotNetService(IProcessRunner, IBuildLogger)
├── MetadataService(IGitService, IBuildLogger)
├── NuGetPublisher(IProcessRunner, IBuildLogger)
├── ReleaseService(IDotNetService, INuGetPublisher, IGitHubService, IBuildLogger)
└── WingetService(IProcessRunner, IBuildLogger)
```

### System.CommandLine 2.0.3 Conventions

This project uses System.CommandLine 2.0.3 (GA release), which has important differences from earlier betas:

- **Option constructor**: Second positional arg is an alias, NOT a description. Use object initializer for Description:
  ```csharp
  // CORRECT:
  new Option<bool>("--no-commit") { Description = "Don't commit changes" }
  // WRONG (second arg is alias, not description):
  new Option<bool>("--no-commit", "Don't commit changes")
  ```
- **Option.Name** includes the `--` prefix: `o.Name == "--no-commit"`, not `"no-commit"`
- **SetAction** pattern: Commands define structure in Command subclasses; `Program.cs` wires up handlers via `SetAction`
- **CA1010 suppression**: `Command` implements `IEnumerable` for collection initializer support, triggering false positives

### Shared GlobalOptions

`GlobalOptions` defines static `Option<T>` instances (`Workspace`, `Configuration`, `Verbose`, `DryRun`, `VersionBump`) shared across multiple commands. These are added to commands via `Options.Add()`.

The `VersionBump` option accepts: `auto` (default), `patch`, `minor`, or `major`. When specified, it overrides automatic version detection.

## Environment Variables

The `BuildConfigurationProvider.CreateFromEnvironmentAsync()` reads:

| Variable | Source in GitHub Actions |
|----------|------------------------|
| `GITHUB_SERVER_URL` | Auto-set |
| `GITHUB_REF` | Auto-set |
| `GITHUB_SHA` | Auto-set |
| `GITHUB_REPOSITORY` | Auto-set (parsed as owner/repo) |
| `GITHUB_TOKEN` / `GH_TOKEN` | `${{ github.token }}` |
| `NUGET_API_KEY` | `${{ secrets.NUGET_KEY }}` |
| `KTSU_PACKAGE_KEY` | `${{ secrets.KTSU_PACKAGE_KEY }}` |
| `EXPECTED_OWNER` | Hardcoded per-project (e.g., `ktsu-dev`) |

## Version Calculation

Version bumps can be controlled three ways (in order of precedence):

1. **CLI/GitHub Actions `--version-bump` option**: Explicitly force `major`, `minor`, or `patch`
2. **Commit message tags**: `[major]`, `[minor]`, `[patch]`, `[pre]`, `[skip ci]`
3. **Auto-detection** (when no tag and no forced bump):
   - Public API diff analysis (added/removed public types, methods, properties) → minor
   - Meaningful code changes → patch
   - Bot/merge commits only → prerelease

Key classes: `VersionCalculator`, `CommitAnalyzer`, `VersionInfo`, `VersionType` enum.

**Method signature:**

```csharp
Task<VersionInfo> GetVersionInfoAsync(
    string workingDirectory,
    string commitHash,
    string initialVersion = "1.0.0",
    VersionType? forcedVersionType = null, // NEW: overrides auto-detection
    CancellationToken cancellationToken = default)
```

## Testing Conventions

- Framework: MSTest.Sdk with `Microsoft.Testing.Platform` runner (configured in `global.json`)
- Mocking: NSubstitute
- Test naming: CA1707 suppressed (underscores allowed)
- CA1515 suppressed (test classes must be public for MSTest discovery)
- Temp directory names in tests must avoid containing "Test", "Demo", "Example", or "Sample" — `ProjectDetector.IsTestOrDemoProject()` uses substring matching on directory names

## Embedded Resources

`scripts/LICENSE.template` is embedded as `Templates/LICENSE.template` in the core library and used by `LicenseGenerator` to produce LICENSE.md files.

## CI/CD Workflow

The `.github/workflows/dotnet.yml` self-hosts by cloning and running KtsuBuild directly. The workflow supports manual triggering with version bump control.

### Workflow Features

- **Automatic triggers**: push to main, pull requests, daily schedule (11 PM UTC)
- **Manual trigger** (`workflow_dispatch`): Allows selecting version bump type (auto, patch, minor, major)
- **Concurrency control**: Only one workflow per branch runs at a time

### Manual Workflow Dispatch

```yaml
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
```

### Consumer Project Integration

Consumer projects clone KtsuBuild and invoke:

```yaml
- name: Clone KtsuBuild
  run: git clone --depth 1 https://github.com/ktsu-dev/KtsuBuild.git "${{ runner.temp }}/KtsuBuild"

- name: Run KtsuBuild CI Pipeline
  shell: pwsh
  env:
    GH_TOKEN: ${{ github.token }}
    NUGET_API_KEY: ${{ secrets.NUGET_KEY }}
    KTSU_PACKAGE_KEY: ${{ secrets.KTSU_PACKAGE_KEY }}
    EXPECTED_OWNER: ktsu-dev
  run: |
    $versionBump = "${{ github.event.inputs.version-bump }}"

    # Build command - only add --version-bump if explicitly set (backward compatible)
    $command = "ci --workspace `"${{ github.workspace }}`" --verbose"
    if (![string]::IsNullOrEmpty($versionBump) -and $versionBump -ne "auto") {
      $command += " --version-bump $versionBump"
    }

    dotnet run --project "${{ runner.temp }}/KtsuBuild/KtsuBuild.CLI" -- $command
```

## Serena MCP Plugin Integration

This project is configured for use with the [Serena MCP plugin](https://github.com/oraios/serena), enabling symbol-based code navigation and editing.

### Configuration

`.serena/project.yml`:
- **Language server**: C# (roslyn OmniSharp)
- **Encoding**: UTF-8
- **Gitignore integration**: Enabled
- **Read-only mode**: Disabled (editing allowed)

### Serena Cache

The `.serena/cache/` directory contains language server caches and is excluded from version control via `.serena/.gitignore`.

## Common Pitfalls

- **System.CommandLine Option constructor**: Always use initializer syntax for Description (see above)
- **Option.Name includes `--`**: Lookups must use `o.Name == "--no-commit"`, not `"no-commit"`
- **Test temp directory names**: Avoid names containing "Test" to prevent false positives in `IsTestOrDemoProject`
- **Multi-targeting warnings**: Lower TFMs (net5.0, net6.0, net7.0) produce warnings from newer packages — these are expected
- **Shared static options**: `GlobalOptions` instances can only have one parent command in System.CommandLine
- **Version bump override**: When `--version-bump` is specified, it completely overrides commit tag detection and auto-detection
