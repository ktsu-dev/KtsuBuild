# iOS support for KtsuBuild

A plan for teaching KtsuBuild to build, test, package, and publish .NET iOS
(`net10.0-ios`) projects, using MeltdownMonitor's hand-rolled iOS pipeline as
the worked example to generalize from.

## Goal and scope

KtsuBuild today builds, tests, packs, and publishes class libraries and
desktop executables, and distributes them through NuGet, GitHub releases, and
Winget. It has no concept of an iOS application head, the macOS-only build
constraints, code signing, `.ipa` packaging, or TestFlight upload.

The goal is to let a consumer project that contains a `net10.0-ios` head run
through KtsuBuild and come out the other side as a signed `.ipa` uploaded to
TestFlight, in the same way a desktop project comes out as zipped runtime
builds plus Winget manifests. The work splits into two halves that can land
independently:

1. **Build and test** an iOS head on a macOS runner (unsigned, no secrets).
   This is useful on its own as pull-request validation and has no dependency
   on signing material.
2. **Package and distribute** a signed `.ipa` to TestFlight on a release tag.
   This needs Apple signing secrets and is gated behind their availability.

Out of scope for the first pass: Android heads, Mac Catalyst, `.app` notarized
distribution outside the App Store, and any MAUI-specific single-project
packaging. The abstractions below should not paint us into a corner on those,
but they are not built now.

## The reference: how MeltdownMonitor does it

MeltdownMonitor proves the whole path by hand in a single workflow
(`.github/workflows/ios.yml`). The mechanics worth lifting are these.

**Project shape.** The iOS work lives in dedicated heads targeting
`net10.0-ios` (`MeltdownMonitor.iOS`, the `Exe` head, and
`MeltdownMonitor.Ble.Apple`, a trimmable library). Platform-neutral logic stays
in `net10.0` projects (`Core`, `Mobile`, `Tests`) that build and test anywhere.
The iOS head carries iOS-only MSBuild properties: `SupportedOSPlatformVersion`,
`ApplicationId`, `CodesignEntitlements`, and the native-asset package references
(`SkiaSharp.NativeAssets.iOS`, `HarfBuzzSharp.NativeAssets.iOS`) without which
the app crashes at startup.

**Host and toolchain pinning.** iOS builds only on macOS. The workflow selects
an exact Xcode (`26.3`, resolved to the canonical app path with an SDK
presence check) and installs an exact iOS workload band via a rollback file
(`dotnet workload install ios --from-rollback-file …`, pinned to
`26.2.10233/10.0.100`). The pin matters: the band default produced a SkiaSharp
asset mismatch, and the workload requires a matching Xcode. If the workload is
unavailable the build fails loudly rather than skipping iOS silently.

**Selective restore.** On macOS the Windows heads throw `NETSDK1100` and pull
Windows SDK references that are not present. The workflow restores an explicit
project list (Core, Mobile, Ble.Apple, Tests, iOS) instead of the whole
solution. This is the single most important idea to generalize: build host and
target framework have to be matched, and a solution-wide build is wrong on a
cross-platform repo.

**Two unsigned builds plus a frameworks check.** Pull-request CI builds the head
for `iossimulator-arm64` and for `ios-arm64`, both with
`-p:EnableCodeSigning=false`, then asserts `libSkiaSharp` is actually embedded
in the device bundle. That catches asset-resolution regressions in CI rather
than at release time.

**Signed archive and upload.** On an `ios-v*` tag, a separate job imports the
distribution certificate (`.p12`) into a temporary keychain, installs the
provisioning profile, stamps `CFBundleShortVersionString` (from the tag) and
`CFBundleVersion` (from the monotonic run number) directly into `Info.plist`
with `PlistBuddy`, then runs:

```
dotnet publish …/MeltdownMonitor.iOS.csproj -c Release -f net10.0-ios \
  -p:RuntimeIdentifier=ios-arm64 -p:ArchiveOnBuild=true -p:BuildIpa=true \
  -p:CodesignKey="$IOS_CODESIGN_KEY" -p:CodesignProvision="$IOS_PROVISION_NAME"
```

The resulting `.ipa` is uploaded to TestFlight with
`xcrun altool --upload-app` using an App Store Connect API key. A cheap
`check-secrets` job on Ubuntu reads a boolean repository secret and gates the
signing job, so forks and contributors without keys still get the unsigned
build.

The takeaway for KtsuBuild: MeltdownMonitor's `ios.yml` is exactly the kind of
bespoke, copy-pasted workflow KtsuBuild exists to replace. The plan is to move
each of those steps behind a KtsuBuild command so a consumer's workflow shrinks
to "clone KtsuBuild, run `ktsubuild ios …`".

## Gap analysis against KtsuBuild today

| Capability | MeltdownMonitor (manual) | KtsuBuild today | Gap |
|---|---|---|---|
| Detect an iOS head | Hard-coded project paths | `ProjectDetector` / `DotNetService.IsExecutableProject` keyed on `OutputType`/`.App` SDK | No TFM-aware classification, no `net*-ios` recognition |
| Match build host to target | macOS-only workflow, explicit restore list | `RestoreAsync` restores the whole working directory | No per-TFM host gating or project filtering |
| Toolchain provisioning | Pinned Xcode + workload rollback file | None | New responsibility |
| Build for an iOS RID | `-p:RuntimeIdentifier=iossimulator-arm64`/`ios-arm64`, signing off | `BuildAsync` takes only `configuration` + freeform args | No first-class iOS build path |
| Package `.ipa` | `dotnet publish -p:ArchiveOnBuild -p:BuildIpa` + signing props | `PublishAsync` is desktop-shaped (`--self-contained`, `PublishSingleFile`) | iOS publish is a different command shape |
| Version stamping | `PlistBuddy` on `Info.plist` | `MetadataService` writes VERSION/CHANGELOG | No plist stamping |
| Signing material | Keychain import, profile install | None | New responsibility, secret-handling |
| Distribution | `xcrun altool` to TestFlight | NuGet / GitHub release / Winget | New distribution target, mirrors Winget structure |
| Secret gating | `check-secrets` boolean | `ShouldRelease` gates NuGet/GitHub | Reusable pattern, new inputs |

Nothing here breaks the existing model. iOS slots in as a new platform service
alongside `WingetService`, plus iOS-aware branches inside `DotNetService` and
`ProjectDetector`.

## Design

The structure mirrors the existing Winget feature deliberately: a focused
service in the core library, an options record, an abstraction interface, a
configuration extension, and a CLI command with `build`/`package`/`upload`
subcommands. That keeps the codebase legible and the review small.

### 1. Project classification (`KtsuBuild.DotNet`, `KtsuBuild.Winget.ProjectDetector`)

Add target-framework awareness so the rest of the tool can reason about which
projects build where.

- Introduce a small `ProjectPlatform` classification (for example `Neutral`,
  `Windows`, `Ios`) derived from the project's `TargetFramework(s)`. A project
  whose TFM matches `net\d+\.\d+-ios` is `Ios`. Read the TFM with the same
  lightweight regex/XML approach `ProjectDetector` already uses, and handle
  multi-targeting (`<TargetFrameworks>`) by treating the project as iOS-capable
  if any TFM is an iOS TFM.
- Extend `IsExecutableProject` so an iOS head (`OutputType=Exe` on an iOS TFM,
  or a `Maui`/iOS SDK) is recognized as an app rather than a library.
- Guard against the `IsTestOrDemoProject` substring trap noted in `CLAUDE.md`:
  an iOS head named with `Demo`/`Sample` would be wrongly excluded. The
  classification should key on TFM and `OutputType`, not directory-name
  substrings, for the iOS path.

This is the dependency the build-host gating and the new commands all rely on,
so it lands first.

### 2. Host-aware restore and build (`KtsuBuild.DotNet.DotNetService`)

- Teach `RestoreAsync` (and the project enumeration feeding build/test/pack) to
  filter projects by `ProjectPlatform` against the current OS. On non-macOS
  hosts, iOS projects are excluded from restore and build, the way
  MeltdownMonitor's workflow restores an explicit list. On macOS, Windows-only
  heads are excluded. This generalizes the `NETSDK1100` workaround into a rule
  instead of a hand-maintained project list.
- Add an iOS build entry point rather than overloading the desktop `PublishAsync`.
  A new method on `IDotNetService`, sketched:

  ```csharp
  Task BuildIosAsync(
      string workingDirectory,
      string projectPath,
      string runtimeIdentifier,      // iossimulator-arm64 | ios-arm64
      string configuration = "Release",
      bool codeSigning = false,
      CancellationToken cancellationToken = default);
  ```

  Unsigned, this constructs the PR-CI command:
  `dotnet build "{project}" -c {config} -p:RuntimeIdentifier={rid} -p:BuildIpa=false -p:EnableCodeSigning=false -p:CodesignKey= -p:CodesignProvision=`.
- Keep the desktop `PublishAsync` untouched. iOS packaging is a distinct shape
  and belongs in the iOS service below, not in the runtime-loop publish that
  desktop uses.

### 3. The iOS service (`KtsuBuild.Ios`, new namespace)

A new `IosService` modeled on `WingetService`, with an `IIosService` interface
in `KtsuBuild.Abstractions` and an `IosOptions` record mirroring `WingetOptions`.

Responsibilities:

- **Toolchain provisioning** (macOS only). Select Xcode and install the pinned
  iOS workload via a rollback file. The Xcode version, workload version, and
  rollback band are configuration inputs with sensible defaults, not hard-coded,
  because they move over time (MeltdownMonitor already had to chase them). Fail
  loudly when the workload is missing.
- **Version stamping.** Write `CFBundleShortVersionString` from the computed
  KtsuBuild version and `CFBundleVersion` from a monotonic build number into the
  head's `Info.plist` with `PlistBuddy`. This reuses KtsuBuild's existing version
  calculation rather than the tag-parsing MeltdownMonitor does inline, which is
  an improvement over the reference.
- **Signing material setup.** Import the distribution certificate into a
  temporary keychain and install the provisioning profile, from base64 secrets.
  Include MeltdownMonitor's OpenSSL 3DES transcode fallback for stubborn `.p12`
  files. This step is skipped entirely when signing inputs are absent.
- **Archive.** Run the signed publish:
  `dotnet publish "{project}" -c {config} -f {iosTfm} -p:RuntimeIdentifier=ios-arm64 -p:ArchiveOnBuild=true -p:BuildIpa=true -p:CodesignKey={key} -p:CodesignProvision={profile}`,
  then locate the produced `.ipa`.
- **Upload.** Push the `.ipa` to TestFlight with `xcrun altool --upload-app`
  using the App Store Connect API key, parsing output for the failure strings
  `altool` returns on a zero exit code.

All process invocation goes through the existing `IProcessRunner`, and logging
through `IBuildLogger`, so the service is testable with `NSubstitute` the same
way the rest of the suite is.

### 4. Configuration (`KtsuBuild.Configuration`)

Add iOS inputs to `BuildConfiguration` and read them in
`BuildConfigurationProvider.CreateFromEnvironmentAsync`, following the existing
env-var convention:

| Property | Env var | Purpose |
|---|---|---|
| `IosSigningAvailable` | `IOS_SIGNING_AVAILABLE` | Gate for the signing/upload path |
| `IosCodesignKey` | `IOS_CODESIGN_KEY` | Distribution certificate common name |
| `IosProvisionName` | `IOS_PROVISION_NAME` | Provisioning profile name |
| `IosCertP12Base64` | `IOS_CERT_P12_BASE64` | Base64 `.p12` certificate |
| `IosProvisioningProfileBase64` | `IOS_PROVISIONING_PROFILE_BASE64` | Base64 profile |
| `AppStoreConnectKeyBase64` | `APP_STORE_CONNECT_KEY_BASE64` | Base64 `.p8` API key |
| `AppStoreConnectKeyId` / `IssuerId` | `APP_STORE_CONNECT_KEY_ID` / `_ISSUER_ID` | API key identifiers |
| `XcodeVersion` / `IosWorkloadVersion` | `IOS_XCODE_VERSION` / `IOS_WORKLOAD_VERSION` | Toolchain pins |

The signing properties carry secrets. They must never be logged or echoed.
`IosSigningAvailable` is the only one that should ever surface in output, and
only as a boolean, exactly as MeltdownMonitor's `check-secrets` job is careful
to never print the secret value.

### 5. CLI surface (`KtsuBuild.CLI`)

Add an `ios` command with subcommands, wired with the project's
`System.CommandLine` 2.0.3 conventions (command subclass plus `SetAction` in
`Program.cs`, `Description` via object initializer, alias as the second
positional arg):

```
ktsubuild ios
├── build     # restore + build head for simulator and device, unsigned (no secrets)
├── package   # provision toolchain + stamp version + sign + archive .ipa
└── upload    # push .ipa to TestFlight
```

`build` is the PR-validation path and runs with no signing inputs. `package`
and `upload` are the release path and no-op with a clear message when
`IosSigningAvailable` is false, so they are safe to call unconditionally from a
consumer workflow. Reuse `GlobalOptions` (`Workspace`, `Configuration`,
`Verbose`, `DryRun`) and add iOS-specific options (`--version`, `--runtime`,
`--bundle-id`) where the env-var config is not sufficient.

Whether `ci` should automatically invoke the iOS path when it detects an iOS
head is an open question (see below). The conservative first step keeps `ios` a
separate top-level command and leaves `ci` unchanged.

### 6. CI workflow

Two things change. First, KtsuBuild's own consumer-facing guidance gains an iOS
job pattern that consumers paste into their workflow. Second, that pattern is
thin because the logic now lives in the tool:

```yaml
ios-build:
  runs-on: macos-latest
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
    - run: git clone --depth 1 https://github.com/ktsu-dev/KtsuBuild.git "${{ runner.temp }}/KtsuBuild"
    - run: dotnet run --project "${{ runner.temp }}/KtsuBuild/KtsuBuild.CLI" -- ios build --workspace "${{ github.workspace }}" --verbose

ios-release:
  needs: ios-build
  if: startsWith(github.ref, 'refs/tags/ios-v')
  runs-on: macos-latest
  environment: ios-release
  env:
    IOS_SIGNING_AVAILABLE: ${{ secrets.IOS_SIGNING_AVAILABLE }}
    IOS_CODESIGN_KEY: ${{ secrets.IOS_CODESIGN_KEY }}
    # … remaining signing secrets …
  steps:
    - uses: actions/checkout@v4
    - run: dotnet run --project "${{ runner.temp }}/KtsuBuild/KtsuBuild.CLI" -- ios package --workspace "${{ github.workspace }}" --verbose
    - run: dotnet run --project "${{ runner.temp }}/KtsuBuild/KtsuBuild.CLI" -- ios upload --workspace "${{ github.workspace }}" --verbose
```

The macOS runner requirement does not change. What changes is that the Xcode
selection, workload pin, selective restore, frameworks check, signing, archive,
and upload all move out of the workflow and into KtsuBuild, where they are
written once and tested.

## Phased implementation

**Phase 1: detection and host gating.** Add `ProjectPlatform` classification and
the host-vs-target filtering in `DotNetService`. No behavior change for existing
non-iOS repos. Unit-testable end to end with synthetic `.csproj` fixtures.
This is the foundation and ships alone.

**Phase 2: unsigned iOS build.** Add `BuildIosAsync`, the `ios build` command,
and the simulator/device builds plus the embedded-frameworks assertion. Needs a
macOS runner in CI but no secrets. Delivers PR validation for iOS consumers.

**Phase 3: packaging and signing.** Add `IosService` toolchain provisioning,
plist stamping, keychain/profile setup, and the archive step, behind
`ios package`. Gate everything on `IosSigningAvailable`.

**Phase 4: TestFlight upload.** Add `ios upload` and the `altool` integration.
Document the full secret set and the `ios-release` environment.

Each phase is a reviewable pull request that leaves the tool working.

## Testing

The same constraints MeltdownMonitor calls out apply here. The signing, archive,
and upload steps cannot be exercised without macOS, Apple secrets, and an App
Store Connect account, so the automated suite covers what it can and the rest is
verified on a real release.

- **Unit tests (MSTest + NSubstitute, runs anywhere).** Project classification
  against fixture `.csproj` files (iOS TFM, multi-target, the `Demo`/`Sample`
  naming trap). Host-vs-target filtering logic. Command-string construction for
  `BuildIosAsync` and the archive publish, asserted against the expected
  `dotnet` argument lists by mocking `IProcessRunner`. The signing-skip path
  when `IosSigningAvailable` is false.
- **Not unit-testable.** Actual Xcode/workload provisioning, real code signing,
  `.ipa` production, and TestFlight upload. These are verified by running the
  pipeline on a macOS runner against a consumer repo (MeltdownMonitor itself is
  the obvious first guinea pig). Treat a green `ios.yml`-equivalent run as the
  acceptance gate, mirroring the "live app only" caution in MeltdownMonitor's
  docs.

Keep temp directory names in tests clear of `Test`/`Demo`/`Example`/`Sample`
per the existing `ProjectDetector.IsTestOrDemoProject` pitfall.

## Risks and open questions

- **Toolchain churn.** MeltdownMonitor had to chase the exact Xcode and workload
  band pairing. Making these configuration inputs (not constants) is the
  mitigation, but consumers will still occasionally need to bump the pins. The
  docs should say so plainly.
- **Secret surface.** This feature introduces certificate and API-key handling
  into KtsuBuild for the first time. The keychain must be temporary and torn
  down, base64 material wiped after decode, and nothing secret logged. This
  deserves a careful security pass before Phase 3 merges.
- **`ci` integration.** Should `ci` auto-run the iOS path when it detects a head,
  or stay opt-in via the separate `ios` command? Auto-running is more in the
  spirit of KtsuBuild replacing bespoke workflows, but it couples the
  Windows-runner `ci` job to a macOS-only step. The plan defaults to opt-in and
  leaves this for a follow-up decision.
- **Versioning channel.** MeltdownMonitor uses a separate `ios-v*` tag namespace
  so iOS releases do not collide with library versioning. KtsuBuild needs to
  decide whether iOS releases share the repo version or get their own channel,
  and how that interacts with the existing commit-tag version calculation.
- **No KtsuBuild self-hosting on iOS.** KtsuBuild is a CLI tool, not an app, so
  it gains the ability to build iOS projects but never targets iOS itself. The
  CI changes here are about consumer projects, not KtsuBuild's own build.

## Reference files

KtsuBuild touch points:

- `KtsuBuild/DotNet/DotNetService.cs` — classification, host gating, `BuildIosAsync`
- `KtsuBuild/Abstractions/IDotNetService.cs`, new `IIosService.cs`
- `KtsuBuild/Winget/ProjectDetector.cs` — TFM-aware detection
- `KtsuBuild/Publishing/ReleaseService.cs` — where an iOS branch would hook in
- `KtsuBuild/Configuration/BuildConfiguration.cs` and `BuildConfigurationProvider.cs`
- New `KtsuBuild/Ios/IosService.cs`, `IosOptions.cs` (mirroring `Winget/`)
- `KtsuBuild.CLI/Program.cs` and `KtsuBuild.CLI/Commands/` — the `ios` command
- `.github/workflows/dotnet.yml` — consumer workflow guidance

MeltdownMonitor reference:

- `.github/workflows/ios.yml` — the full manual pipeline this generalizes
- `MeltdownMonitor.iOS/MeltdownMonitor.iOS.csproj`, `Info.plist`, `Entitlements.plist`
