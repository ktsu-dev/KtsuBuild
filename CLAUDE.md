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
├── winget
│   ├── generate  # Generate Winget manifests (--version, --repo, --package-id, --staging)
│   └── upload    # Upload manifests to GitHub release (--version)
└── ios
    ├── build     # Build iOS head(s) for simulator + device, unsigned (macOS only)
    │   # Options: --workspace, --configuration, --verbose, --project, --runtime, --require-framework
    ├── package   # Provision toolchain + stamp version + sign + archive .ipa (macOS only)
    │   # Options: --workspace, --configuration, --verbose, --project, --runtime, --framework, --version, --build-number
    └── upload    # Upload the signed .ipa to TestFlight (macOS only)
        # Options: --workspace, --configuration, --verbose, --project, --ipa
```

The `ios build` command is the pull-request validation path for iOS consumers
(plan phase 2). It auto-detects iOS heads (executable projects on a `net*-ios`
target framework), builds each for `iossimulator-arm64` and `ios-arm64` with
signing disabled (`-p:EnableCodeSigning=false`, no secrets), and verifies the
device `.app` bundle was produced. `--require-framework <name>` (repeatable)
fails the device build when a named native framework (for example
`libSkiaSharp`) is not embedded in the bundle, catching the asset-resolution
launch crash in CI. On a non-macOS host the command logs a skip and exits 0, so
it is safe to call unconditionally.

The `ios package` command is the signed release path (plan phase 3). It gates on
`IOS_SIGNING_AVAILABLE`: with no signing material it no-ops and exits 0, so it is
also safe to call unconditionally (forks and contributors without secrets still
get a green run). When signing is available it selects the pinned Xcode and
installs the pinned iOS workload (both optional, via `IOS_XCODE_VERSION` /
`IOS_WORKLOAD_VERSION`), stamps `CFBundleShortVersionString` (KtsuBuild's computed
version) and `CFBundleVersion` (the build number, defaulting to
`GITHUB_RUN_NUMBER`) into each head's `Info.plist` with `PlistBuddy`, imports the
distribution certificate into a temporary `build.keychain` (with the OpenSSL 3DES
transcode fallback for stubborn `.p12` files), installs the provisioning profile,
and runs `dotnet publish -p:ArchiveOnBuild=true -p:BuildIpa=true` to produce a
signed `.ipa`. The signing inputs carry secrets and are never logged; only the
`IosSigningAvailable` boolean surfaces in output.

The `ios upload` command is the TestFlight delivery path (plan phase 4). It gates
on `IOS_SIGNING_AVAILABLE` the same way as `package`, so it is also safe to call
unconditionally. When signing is available it locates the produced `.ipa` (either
the `--ipa` path, or by searching each detected head's `bin` directory), installs
the App Store Connect API key (`.p8`) under `~/.appstoreconnect/private_keys`, and
runs `xcrun altool --upload-app --type ios --apiKey … --apiIssuer …` for each
archive. `altool`'s exit code is unreliable (it has returned `0` on an App Store
Connect validation failure), so the command also scans the captured output for the
`UPLOAD FAILED` / `Failed to upload package` strings and fails on either signal.
The API key is a secret and is never logged; the decoded `.p8` is wiped after the
run.

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

iOS signing inputs (read by the same provider, consumed by `ios package`). All
but the first carry secrets and are never logged:

| Variable | Purpose |
|----------|---------|
| `IOS_SIGNING_AVAILABLE` | Boolean gate (`true`) for the signing/upload path |
| `IOS_CODESIGN_KEY` | Distribution certificate common name (`CodesignKey`) |
| `IOS_PROVISION_NAME` | Provisioning profile name (`CodesignProvision`) |
| `IOS_CERT_P12_BASE64` | Base64 `.p12` distribution certificate |
| `IOS_CERT_P12_PASSWORD` | Password for the `.p12` |
| `IOS_KEYCHAIN_PASSWORD` | Password for the temporary signing keychain |
| `IOS_PROVISIONING_PROFILE_BASE64` | Base64 `.mobileprovision` |
| `IOS_XCODE_VERSION` | Pinned Xcode version (optional, e.g. `26.3`) |
| `IOS_WORKLOAD_VERSION` | Pinned iOS workload rollback entry (optional, e.g. `26.2.10233/10.0.100`) |

App Store Connect inputs (read by the same provider, consumed by `ios upload`).
The key is a secret and is never logged:

| Variable | Purpose |
|----------|---------|
| `APP_STORE_CONNECT_KEY_BASE64` | Base64 App Store Connect API key (`.p8`) |
| `APP_STORE_CONNECT_KEY_ID` | API key identifier (`--apiKey`, names the `AuthKey_{id}.p8` file) |
| `APP_STORE_CONNECT_ISSUER_ID` | API issuer identifier (`--apiIssuer`) |

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

    # Build arguments array - only add --version-bump if explicitly set (backward compatible)
    $args = @("ci", "--workspace", "${{ github.workspace }}", "--verbose")
    if (![string]::IsNullOrEmpty($versionBump) -and $versionBump -ne "auto") {
      $args += @("--version-bump", $versionBump)
    }

    & dotnet run --project "${{ runner.temp }}/KtsuBuild/KtsuBuild.CLI" -- @args
```

### iOS Pull-Request Validation

A consumer with a `net*-ios` head adds a macOS job for unsigned build validation.
The macOS runner, Xcode selection, and iOS workload pin still live in the
consumer workflow for now (toolchain provisioning is a later phase); the build
and the embedded-frameworks check move into the tool:

```yaml
ios-build:
  runs-on: macos-latest
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
    # ... select Xcode + install the pinned iOS workload ...
    - run: git clone --depth 1 https://github.com/ktsu-dev/KtsuBuild.git "${{ runner.temp }}/KtsuBuild"
    - run: >
        dotnet run --project "${{ runner.temp }}/KtsuBuild/KtsuBuild.CLI" --
        ios build --workspace "${{ github.workspace }}" --verbose
        --require-framework libSkiaSharp
```

### iOS Signed Packaging

On a release tag, a second macOS job archives a signed `.ipa`. The signing
material lives in a protected `ios-release` environment; the toolchain pins and
the build/sign/archive logic now live in the tool, so the job is thin. The
command no-ops when `IOS_SIGNING_AVAILABLE` is not `true`, so it is safe to call
unconditionally:

```yaml
ios-package:
  needs: ios-build
  if: startsWith(github.ref, 'refs/tags/ios-v')
  runs-on: macos-latest
  environment: ios-release
  env:
    IOS_SIGNING_AVAILABLE: ${{ secrets.IOS_SIGNING_AVAILABLE }}
    IOS_CODESIGN_KEY: ${{ secrets.IOS_CODESIGN_KEY }}
    IOS_PROVISION_NAME: ${{ secrets.IOS_PROVISION_NAME }}
    IOS_CERT_P12_BASE64: ${{ secrets.IOS_CERT_P12_BASE64 }}
    IOS_CERT_P12_PASSWORD: ${{ secrets.IOS_CERT_P12_PASSWORD }}
    IOS_KEYCHAIN_PASSWORD: ${{ secrets.IOS_KEYCHAIN_PASSWORD }}
    IOS_PROVISIONING_PROFILE_BASE64: ${{ secrets.IOS_PROVISIONING_PROFILE_BASE64 }}
    APP_STORE_CONNECT_KEY_BASE64: ${{ secrets.APP_STORE_CONNECT_KEY_BASE64 }}
    APP_STORE_CONNECT_KEY_ID: ${{ secrets.APP_STORE_CONNECT_KEY_ID }}
    APP_STORE_CONNECT_ISSUER_ID: ${{ secrets.APP_STORE_CONNECT_ISSUER_ID }}
    IOS_XCODE_VERSION: "26.3"
    IOS_WORKLOAD_VERSION: "26.2.10233/10.0.100"
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
    - run: git clone --depth 1 https://github.com/ktsu-dev/KtsuBuild.git "${{ runner.temp }}/KtsuBuild"
    - run: >
        dotnet run --project "${{ runner.temp }}/KtsuBuild/KtsuBuild.CLI" --
        ios package --workspace "${{ github.workspace }}" --verbose
    - run: >
        dotnet run --project "${{ runner.temp }}/KtsuBuild/KtsuBuild.CLI" --
        ios upload --workspace "${{ github.workspace }}" --verbose
```

The `ios upload` step reuses the `.ipa` produced by `ios package` in the same
workspace, so it must run on the same runner after the package step. It no-ops the
same way when `IOS_SIGNING_AVAILABLE` is not `true`.

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
