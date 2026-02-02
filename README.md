# KtsuBuild

.NET build automation tool with semantic versioning, changelog generation, and multi-platform publishing.

## Features

- **Semantic Versioning**: Automatic version calculation based on commit messages
- **Changelog Generation**: Auto-generated CHANGELOG.md from git history
- **License Generation**: Generates LICENSE.md and COPYRIGHT.md from templates
- **Multi-Platform Publishing**: Build and publish for Windows, Linux, and macOS
- **NuGet Publishing**: Publish to NuGet.org, GitHub Packages, and custom feeds
- **GitHub Releases**: Create releases with assets and release notes
- **Winget Manifests**: Generate Windows Package Manager manifests

## Installation

```bash
dotnet tool install -g KtsuBuild.CLI
```

## Quick Start

```bash
# Run the full CI/CD pipeline
ktsub ci

# Build only (restore, build, test)
ktsub build

# Show current version information
ktsub version show

# Update all metadata files
ktsub metadata update
```

## CLI Commands

### Global Options

All commands support these global options:

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--workspace` | `-w` | The workspace/repository path | Current directory |
| `--configuration` | `-c` | Build configuration (Debug/Release) | Release |
| `--verbose` | `-v` | Enable verbose output | false |

### `ktsub ci`

Run the full CI/CD pipeline: metadata update, build, test, pack, publish, and release.

```bash
ktsub ci [options]
```

**Options:**
- `--dry-run`: Preview actions without executing them

**What it does:**
1. Updates metadata files (VERSION.md, CHANGELOG.md, LICENSE.md)
2. Restores NuGet packages
3. Builds the solution
4. Runs tests with coverage
5. Packs NuGet packages
6. Publishes executables for all platforms
7. Publishes NuGet packages to configured feeds
8. Creates a GitHub release with assets

### `ktsub build`

Build workflow: restore, build, and test.

```bash
ktsub build [options]
```

### `ktsub release`

Release workflow: pack, publish NuGet packages, and create GitHub release.

```bash
ktsub release [options]
```

**Options:**
- `--dry-run`: Preview actions without executing them

### `ktsub version`

Version management commands.

#### `ktsub version show`

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

#### `ktsub version bump`

Calculate and display the next version number.

```bash
ktsub version bump [options]
```

#### `ktsub version create`

Create or update the VERSION.md file with the calculated version.

```bash
ktsub version create [options]
```

### `ktsub metadata`

Metadata file management commands.

#### `ktsub metadata update`

Update all metadata files (VERSION.md, CHANGELOG.md, LICENSE.md, COPYRIGHT.md).

```bash
ktsub metadata update [options]
```

**Options:**
- `--no-commit`: Don't commit changes after updating

#### `ktsub metadata license`

Generate LICENSE.md and COPYRIGHT.md files from templates.

```bash
ktsub metadata license [options]
```

#### `ktsub metadata changelog`

Generate CHANGELOG.md from git history.

```bash
ktsub metadata changelog [options]
```

### `ktsub winget`

Windows Package Manager manifest commands.

#### `ktsub winget generate`

Generate Winget manifests for a version.

```bash
ktsub winget generate --version <version> [options]
```

**Options:**
- `--version`, `-V`: The version to generate manifests for (required)
- `--repo`, `-r`: The GitHub repository (owner/repo)
- `--package-id`, `-p`: The package identifier
- `--staging`, `-s`: The staging directory with hashes.txt

#### `ktsub winget upload`

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
| `[major]` | Major version bump (1.0.0 → 2.0.0) | Breaking API changes |
| `[minor]` | Minor version bump (1.0.0 → 1.1.0) | New features |
| `[patch]` | Patch version bump (1.0.0 → 1.0.1) | Bug fixes |
| `[pre]` | Prerelease bump (1.0.0 → 1.0.1-pre.0) | Unstable changes |
| `[skip ci]` | Skip release entirely | Documentation-only changes |

**Examples:**
```bash
git commit -m "[minor] Add new authentication feature"
git commit -m "[patch] Fix null reference in user service"
git commit -m "[major] Redesign public API"
git commit -m "[skip ci] Update documentation"
```

If no tag is specified, KtsuBuild automatically determines the version bump by:
1. Detecting public API changes in C# files (triggers minor bump)
2. Filtering out bot commits and PR merges
3. Defaulting to patch for meaningful changes, prerelease otherwise

## Generated Metadata Files

KtsuBuild generates and maintains these files:

| File | Purpose |
|------|---------|
| `VERSION.md` | Contains the current version number |
| `CHANGELOG.md` | Complete changelog with all versions |
| `LATEST_CHANGELOG.md` | Changelog for the current version only |
| `LICENSE.md` | License file generated from template |
| `COPYRIGHT.md` | Copyright notice with contributors |

## Environment Variables

KtsuBuild uses these environment variables when running in CI/CD:

| Variable | Description |
|----------|-------------|
| `GITHUB_TOKEN` | GitHub API token for releases and packages |
| `NUGET_API_KEY` | NuGet.org API key for publishing |
| `KTSU_PACKAGE_KEY` | API key for ktsu.dev package feed |
| `GITHUB_SERVER_URL` | GitHub server URL (default: https://github.com) |
| `GITHUB_REF` | Git reference (branch/tag) |
| `GITHUB_SHA` | Git commit SHA |
| `GITHUB_REPOSITORY` | Repository in owner/repo format |
| `GITHUB_REPOSITORY_OWNER` | Repository owner |
| `EXPECTED_OWNER` | Expected owner for official builds |

## Build Configuration

The build system automatically determines:

- **IsOfficial**: Whether the repository is the official one (not a fork)
- **IsMain**: Whether the build is on the main branch
- **IsTagged**: Whether the current commit is already tagged
- **ShouldRelease**: Whether a release should be created (IsMain && !IsTagged && IsOfficial)

## Examples

### CI/CD Pipeline (GitHub Actions)

```yaml
name: CI/CD

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Install KtsuBuild
        run: dotnet tool install -g KtsuBuild.CLI

      - name: Run CI Pipeline
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
        run: ktsub ci
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
```

## License

This project is licensed under the MIT License - see the LICENSE.md file for details.
