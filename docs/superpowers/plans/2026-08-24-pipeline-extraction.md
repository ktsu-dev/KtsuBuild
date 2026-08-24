# Pipeline extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the CI pipeline's stages out of `CiCommand` into a service the other commands can call, so `ci` becomes an orchestrator that delegates, and `release` and `build` stop reimplementing or missing pieces of what `ci` does.

**Architecture:** A new `KtsuBuild.Pipeline` namespace holds one stage per thing the pipeline does, plus a context object carrying state between stages. `CiCommand` calls them in order and does nothing else. `ReleaseCommand` calls the preparation stage and the release stage, which is what fixes the defect below. `BuildCommand` calls the build and test stages, which removes a duplicated copy of the dotnet-script handling.

**Tech Stack:** C#, .NET 10, System.CommandLine, MSTest, NSubstitute.

**Spec:** none. This is a defect found in production, plus the structural change that prevents its whole class.

## The defect that prompted this

On 2026-08-24, ImGuiApp's CI published a GitHub release tagged `v1.0.0-pre.0` with 35 assets, on a repository whose previous release was `v3.10.0` and whose VERSION.md read `3.11.0`. Nothing reached nuget.org. The release and tag have been deleted.

The pipeline was not confused. Its log shows `Found 288 tag(s)`, `Using last tag: v3.10.0`, `Version increment type: Minor`, and it wrote VERSION.md as `3.11.0`. The wrong version came from the step after it.

| Component | Sets `BuildConfiguration.Version`? |
| --- | --- |
| `BuildConfigurationProvider.CreateFromEnvironmentAsync` (line 77) | Yes, to the placeholder `"1.0.0-pre.0"` |
| `CiCommand` (line 143) | Yes, `buildConfig.Version = metadataResult.Version` |
| `ReleaseCommand` | **No** |

`ReleaseService.ExecuteReleaseAsync` reads `config.Version` for the zip names, the GitHub tag, and the prerelease flag, and `config.ReleaseHash` as the release target. A standalone `ktsubuild release` supplies neither, so it published the placeholder.

This stayed invisible while `release` was only ever reached through `ci`. It surfaced when a workflow started calling `ci --no-test --no-release` and then `release` separately, so a quality gate could sit between them.

**The narrow fix would be to set two fields in `ReleaseCommand`. This plan does the structural one instead**, because the same shape produced the same class of problem twice already: `build` carries its own copy of the dotnet-script handling, and `ci` had to grow `--no-test` and `--no-release` flags precisely because its stages were not callable individually. Extracting the stages removes the reason those workarounds existed.

## Global Constraints

- Tabs for indentation in C# files. Match the line endings of the file being edited rather than assuming a style.
- File-scoped namespaces. Using directives inside the namespace. Braces on every control flow statement. Explicit accessibility modifiers. No `this.` qualifiers. Nullable reference types enabled. Warnings as errors, and unused parameters (IDE0060) are errors here.
- US English in all code, comments, and documentation.
- MSTest with semantic asserts. This suite asserts counts with `Assert.AreEqual(n, x.Count)`. `Assert.HasCount` and `Assert.IsEmpty` appear nowhere in it and must not be introduced. `[DataRow]` is established by `CiReleaseDecisionTests`.
- No global warning suppressions. Only narrow, justified, targeted ones matching the existing `#pragma warning disable CA1031` style.
- **Never pass `--nologo` to `dotnet test`.** It reports `total: 0` with exit code 5 while every test passes.
- **Building rewrites `.editorconfig`.** Run `git checkout .editorconfig` before every commit, and stage files by name. Never `git add -A`.
- Commit messages carry a version tag. Do not add Co-Authored-By lines.
- Prose in comments and XML docs must not use em dashes, en dashes, or semicolons joining clauses.
- `KtsuBuild.Tests` references `KtsuBuild.csproj` only, not `KtsuBuild.Tool.csproj`. **Anything needing a unit test must live in the `KtsuBuild` library**, which is the main reason the stages move there.
- Work on branch `fix/pipeline-extraction`, cut from `main`.
- The suite stands at 388 tests before this plan starts.
- **`ci` is how every ktsu repo releases.** A green suite is not sufficient evidence that this refactor preserved its behavior. Task 1 requires a differential proof.

## File Structure

| File | Responsibility |
| --- | --- |
| `KtsuBuild/Pipeline/PipelineContext.cs` (create) | The state threaded between stages: the build configuration, the resolved version information, and whether the version gate suppressed the release. |
| `KtsuBuild/Pipeline/PipelineService.cs` (create) | One method per pipeline stage. This is where the logic lives after the move. |
| `KtsuBuild.Tests/Pipeline/PipelineServiceTests.cs` (create) | Covers the stages that carry decisions, especially version resolution. |
| `KtsuBuild.Tool/Commands/CiCommand.cs` (modify) | Becomes an orchestrator. Calls the stages in order and holds no pipeline logic. |
| `KtsuBuild.Tool/Commands/ReleaseCommand.cs` (modify) | Calls the preparation stage, then the release stage. This is the defect fix. |
| `KtsuBuild.Tool/Commands/BuildCommand.cs` (modify) | Calls the shared build and test stages instead of its own copy. |
| `README.md` (modify) | State what `release` now resolves and from where. |

---

### Task 1: Extract the stages and make ci delegate

This is the load-bearing task. Nothing observable may change.

**Files:**
- Create: `KtsuBuild/Pipeline/PipelineContext.cs`, `KtsuBuild/Pipeline/PipelineService.cs`
- Create: `KtsuBuild.Tests/Pipeline/PipelineServiceTests.cs`
- Modify: `KtsuBuild.Tool/Commands/CiCommand.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces, all in namespace `KtsuBuild.Pipeline`, and Tasks 2 and 3 call these exact names:
  - `PipelineContext` with settable properties `BuildConfiguration Configuration`, `VersionInfo VersionInfo`, and `bool ReleaseSuppressedByVersionGate`.
  - `PipelineService(IProcessRunner processRunner, IBuildLogger logger)`.
  - `Task<PipelineContext> PrepareAsync(string workspace, string configuration, string versionBump, CancellationToken cancellationToken)`
  - `Task UpdateMetadataAsync(PipelineContext context, CancellationToken cancellationToken)`
  - `Task RestoreAndBuildAsync(string workspace, string configuration, string? buildArgs, CancellationToken cancellationToken)`
  - `Task RunTestsAsync(string workspace, string configuration, CancellationToken cancellationToken)`
  - `Task<bool> ValidateIosAsync(string workspace, string configuration, CancellationToken cancellationToken)`
  - `Task ReleaseAsync(PipelineContext context, CancellationToken cancellationToken)`
  - `void WriteStepOutputs(PipelineContext context)`

- [ ] **Step 1: Read the whole method being moved**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
sed -n '/private static async Task<int> ExecutePipelineAsync/,/^	}/p' KtsuBuild.Tool/Commands/CiCommand.cs
grep -n "ExecuteIosValidationAsync\|UpdateRepositoryTopicsAsync\|WriteStepOutputs\|ParseVersionBump" KtsuBuild.Tool/Commands/CiCommand.cs
```

Every one of those private helpers moves too. Read them all before writing anything, and keep their bodies byte-identical through the move. This task relocates code, it does not rewrite it.

- [ ] **Step 2: Note the ordering that must be preserved**

Write down the current order of operations before you move anything, and check your result against it afterwards. The order is load-bearing in ways that are not obvious:

1. Build configuration is created from the environment.
2. Metadata is updated and committed, but only when the build is both official and on main. The result supplies `Version` and `ReleaseHash`.
3. Repository topics are updated, gated on the same condition as the metadata commit.
4. The version increment is calculated, and `VersionType.Skip` suppresses the release only. Build and test still run. The comment in the current code explains why: returning early would leave a workspace whose commits all carry `[skip ci]` never compiled and never tested, and would break the SonarQube scanner's begin and end pair, which needs a compilation between them.
5. dotnet-script is installed when the workspace contains `.csx` files.
6. Restore, build, test.
7. iOS validation, which builds only on macOS and reports and skips elsewhere.
8. Release, when the configuration permits it and the version gate did not suppress it.
9. Step outputs are written last, and always, including when the release was suppressed.

- [ ] **Step 3: Create the context**

Create `KtsuBuild/Pipeline/PipelineContext.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Pipeline;

using KtsuBuild.Configuration;
using KtsuBuild.Git;

/// <summary>
/// The state a pipeline run threads between its stages.
/// </summary>
/// <remarks>
/// Stages that only need a workspace and a configuration take them as parameters instead, so a
/// caller such as <c>build</c> can run them without a git repository or a GitHub token.
/// </remarks>
public sealed class PipelineContext
{
	/// <summary>
	/// Gets or sets the configuration this run was prepared with.
	/// </summary>
	public required BuildConfiguration Configuration { get; set; }

	/// <summary>
	/// Gets or sets the version information resolved for this run.
	/// </summary>
	public required VersionInfo VersionInfo { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the version increment suppressed the release.
	/// </summary>
	/// <remarks>
	/// This is how <c>[skip ci]</c> and a run with no meaningful changes behave. It suppresses the
	/// release only. Build and test still run, so a workspace whose commits all carry the skip
	/// marker is still compiled and tested.
	/// </remarks>
	public bool ReleaseSuppressedByVersionGate { get; set; }
}
```

Confirm `VersionInfo`'s namespace before writing the using directive. It is referenced in `CiCommand` already, so match whatever that file imports.

- [ ] **Step 4: Create the service by moving, not rewriting**

Create `KtsuBuild/Pipeline/PipelineService.cs` as a class taking `(IProcessRunner processRunner, IBuildLogger logger)`, and move each stage across with its body unchanged apart from the mechanical substitutions the move forces: parameters replacing captured locals, and `logger`/`processRunner` coming from the constructor.

`PrepareAsync` is the one stage that is genuinely new code rather than a move, because `CiCommand` currently interleaves configuration creation with the metadata update. It must:

1. Create the `BuildConfiguration` through `BuildConfigurationProvider.CreateFromEnvironmentAsync`, then set `Configuration` on it from the parameter.
2. Log `Is Official`, `Is Main`, and `Should Release` exactly as `CiCommand` does today, with the same wording.
3. Resolve the version through `VersionCalculator.GetVersionInfoAsync`, passing the forced version type parsed from `versionBump`.
4. Set `Configuration.Version` from the resolved version information, and `Configuration.ReleaseHash` from the current commit hash.
5. Set `ReleaseSuppressedByVersionGate` from `VersionInfo.VersionIncrement == VersionType.Skip`, logging the same skip message `CiCommand` logs today.

Point 4 is the defect fix. Every caller that prepares a run now gets a real version rather than the `BuildConfigurationProvider` placeholder, whether or not it goes on to update metadata.

`UpdateMetadataAsync` then overwrites `Configuration.Version` and `Configuration.ReleaseHash` from its metadata result, exactly as `CiCommand` does today, because the metadata commit is what `ci` releases against. Preserve that overwrite. Do not assume `PrepareAsync` already got it right and skip it.

Move `ParseVersionBump`, `ExecuteIosValidationAsync` (as `ValidateIosAsync`), `UpdateRepositoryTopicsAsync`, and `WriteStepOutputs` into the service as well. `WriteStepOutputs` keeps computing `should_release` through `CiReleaseDecision.ShouldReleaseOutput`, and still takes no suppression flag.

- [ ] **Step 5: Make `ci` delegate**

Rewrite `CiCommand.ExecutePipelineAsync` so its body is a sequence of `PipelineService` calls and the flag checks that gate them, with no pipeline logic of its own. It keeps: the dry-run early return, the `try`/`catch` and its `CA1031` suppression, the `--no-test` and `--no-release` gating, and the `CiReleaseDecision.ShouldExecuteRelease` call.

Delete every private helper that moved. If a helper is left behind unused, the build fails under warnings as errors, which is the check that you finished the move.

- [ ] **Step 6: Write tests for the stage that carries the defect**

Create `KtsuBuild.Tests/Pipeline/PipelineServiceTests.cs`. At minimum, prove the thing that broke production: **a prepared context never carries the placeholder version**.

```csharp
	// The defect this refactor exists to remove. BuildConfigurationProvider seeds Version with
	// "1.0.0-pre.0", and a caller that forgets to overwrite it publishes a real release under a
	// version nobody chose. Preparation now owns that, so no caller can forget.
	[TestMethod]
	public async Task PrepareResolvesTheVersionRatherThanLeavingThePlaceholder()
```

Use the repository's existing mocking approach for `IProcessRunner`, `IGitService`, and `IGitHubService`. Look at `BuildConfigurationProviderTests` and the `Mocks` folder for the established pattern before inventing one. Assert that the resolved version is the one the calculator produced and, separately, that it is not `"1.0.0-pre.0"`.

Add tests for the version gate too: a `VersionType.Skip` result must set `ReleaseSuppressedByVersionGate` true, and any other increment must leave it false.

- [ ] **Step 7: Run the suite**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet build
dotnet test
```

Expected: build succeeds with 0 warnings and 0 errors, and every pre-existing test still passes. Report the new total.

- [ ] **Step 8: Prove `ci` is behaviorally identical**

A green suite does not establish this, because almost nothing covers `CiCommand` directly. `ci` is how every ktsu repo releases, so the extraction has to be demonstrated rather than assumed.

Build a scratch program outside the repository that drives the pre-refactor and post-refactor pipelines side by side against the same substituted `IProcessRunner`, `IGitService`, and `IGitHubService`, and compare:

- the exact command strings passed to the process runner, in order, character for character
- the number of invocations of each service member
- the logger lines emitted, in order
- the step outputs written, key by key
- whether the release stage was reached

Get the pre-refactor behavior with `git show main:KtsuBuild.Tool/Commands/CiCommand.cs`. Cover at least these cases, since they are where the ordering above can silently change:

| Case | What it catches |
| --- | --- |
| official, on main, normal increment | the ordinary release path |
| official, on main, `VersionType.Skip` | build and test still run, release suppressed, outputs still written |
| not official | metadata not committed, topics not updated |
| official, not on main | same, via the other half of the condition |
| `--no-test` | test stage skipped, everything else identical |
| `--no-release` | release stage skipped, `should_release` output unchanged |
| forced `--version-bump major` | the parsed override reaches the calculator |

Report the number of cases compared and any divergence. Zero divergences is the bar.

- [ ] **Step 9: Commit**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git add KtsuBuild/Pipeline/PipelineContext.cs KtsuBuild/Pipeline/PipelineService.cs KtsuBuild.Tests/Pipeline/PipelineServiceTests.cs KtsuBuild.Tool/Commands/CiCommand.cs
git commit -m "refactor: extract the CI pipeline stages into a service [minor]"
```

---

### Task 2: Make release prepare before it publishes

**Files:**
- Modify: `KtsuBuild.Tool/Commands/ReleaseCommand.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes: `PipelineService.PrepareAsync` and `PipelineService.ReleaseAsync` from Task 1.
- Produces: `ktsubuild release` publishes the version the pipeline resolves, against the current commit.

- [ ] **Step 1: Rewrite the handler to delegate**

`ReleaseCommand` currently creates its own services, builds a configuration, checks `ShouldRelease`, and calls `ReleaseService.ExecuteReleaseAsync` with a configuration whose `Version` is still the placeholder. Replace that with a `PipelineService`, a `PrepareAsync` call, and a `ReleaseAsync` call.

Keep all of the existing behavior around it: the dry-run warning and early return, the `ShouldRelease` early return with its existing log lines, the `try`/`catch` with its `CA1031` suppression and the `Release workflow failed: <message>` wording, and the `Release workflow completed successfully!` success line.

Honor the version gate as well. `release` must not publish when `ReleaseSuppressedByVersionGate` is true, because that is how `[skip ci]` suppresses a release, and today a standalone `release` ignores it entirely. Use `CiReleaseDecision.ShouldExecuteRelease` with `suppressedByFlag: false` so both commands answer the question the same way.

- [ ] **Step 2: Build and run the suite**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet build
dotnet test
```

Expected: 0 warnings, 0 errors, and the suite at Task 1's total.

- [ ] **Step 3: Prove the fix against the real defect, by execution**

In a throwaway git repository with no remote, `IsOfficial` is false so `ShouldRelease` is false and nothing can publish, but the log still shows what was resolved.

```bash
SCRATCH="$(mktemp -d)/verify"
mkdir -p "$SCRATCH" && cd "$SCRATCH"
git init -q -b main
echo "3.11.0" > VERSION.md
git add VERSION.md
git -c user.email=t@example.com -c user.name=Test commit -q -m "chore: version file"
git tag v3.10.0
/c/dev/ktsu-dev/KtsuBuild/KtsuBuild.Tool/bin/Debug/net10.0/ktsu.KtsuBuild.Tool.exe release --workspace "$SCRATCH" --verbose 2>&1 | tee /tmp/rel-verify.txt | tail -20
echo "--- placeholder present? (must be 0) ---"
grep -c "1\.0\.0-pre\.0" /tmp/rel-verify.txt
```

Expected: the run reports the version it resolved from the tag and the commits, and `1.0.0-pre.0` appears zero times. Report the actual count and the resolved version.

**Positive control, required.** A count of zero proves nothing on its own if the command failed before resolving anything. Check out `main`'s `ReleaseCommand.cs`, rebuild, run the identical command, and confirm the placeholder **does** appear. Then restore your version. Report both counts.

- [ ] **Step 4: Document the contract**

In `README.md`, under `### \`release\``, after the existing `**Options:**` list, add:

```markdown
`release` resolves the version the same way `ci` does, from the repository's tags and commit
history, and publishes against the current commit. It also honors the version gate, so a run whose
commits all carry `[skip ci]` publishes nothing.
```

Check whether `README.md` is LF or CRLF in the working tree before editing, and match it. The two states disagree in this repository, so read the file rather than assuming.

- [ ] **Step 5: Commit**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git add KtsuBuild.Tool/Commands/ReleaseCommand.cs README.md
git commit -m "fix: resolve the version before releasing [patch]"
```

---

### Task 3: Make build delegate too

**Files:**
- Modify: `KtsuBuild.Tool/Commands/BuildCommand.cs`

**Interfaces:**
- Consumes: `PipelineService.RestoreAndBuildAsync` and `PipelineService.RunTestsAsync` from Task 1.
- Produces: nothing consumed later.

- [ ] **Step 1: Confirm the two copies agree before merging them**

`BuildCommand` sets `buildArgs = "-maxCpuCount:1"` when the workspace contains `.csx` files. `BuildConfigurationProvider` line 66 computes `BuildArgs = useDotnetScript ? "-maxCpuCount:1" : string.Empty`. Same rule, written twice.

One difference is real and must be preserved or deliberately resolved: with no `.csx` files, `build` passes `null` and `ci` passes `string.Empty`. Check what `DotNetService.BuildAsync` does with each before you unify them, and report which you chose and why. If they produce different command strings, that is a behavior change and must be called out rather than absorbed.

- [ ] **Step 2: Replace the body with delegation**

Replace `BuildCommand`'s inline dotnet-script handling, restore, build, and test with calls to `RestoreAndBuildAsync` and `RunTestsAsync`, keeping the `--no-test` gate, the `try`/`catch` with its `CA1031` suppression, the `Build workflow failed: <message>` wording, and the `Build workflow completed successfully!` success line.

`build` must keep working in a directory that is not a git repository, so do not introduce a `PrepareAsync` call here.

- [ ] **Step 3: Verify by execution, each flag alone**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet build
dotnet test
```

Then, against a scratch workspace containing a trivial class library and a solution file, run `build` with no flags and with `--no-test`, and confirm the step headers differ by exactly one entry, `Running Tests with Coverage`. That header is emitted before the test-project check, so a workspace with no test projects still prints it. Report both header sets.

Also run `build` in a directory that is **not** a git repository and confirm it still succeeds, since that is the behavior Step 2 must not break.

- [ ] **Step 4: Commit**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git add KtsuBuild.Tool/Commands/BuildCommand.cs
git commit -m "refactor: build delegates to the shared pipeline stages [patch]"
```

---

### Task 4: Open the pull request

**Files:** none.

- [ ] **Step 1: Confirm the branch is clean and complete**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git status --short
git log --oneline main..HEAD
dotnet build
dotnet test
```

Expected: a clean tree, four commits, 0 warnings, 0 errors, and a green suite.

- [ ] **Step 2: Open the pull request**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git push -u origin fix/pipeline-extraction
gh pr create --base main --head fix/pipeline-extraction \
  --title "Extract the pipeline stages so every command shares them" \
  --body "Moves the CI pipeline's stages out of CiCommand into a PipelineService the other commands call, so ci orchestrates and delegates rather than owning the logic. Fixes a production defect where a standalone ktsubuild release published the 1.0.0-pre.0 placeholder, because only ci ever set the version. Also removes build's duplicated copy of the dotnet-script handling. ci's behavior is unchanged and was proved so by differential comparison against main."
```

- [ ] **Step 3: Stop and report**

Merging and releasing are the repository owner's calls. Report the PR URL and stop.

State plainly that ImGuiApp's release path stays broken until this ships, because its workflow calls `ci --no-test --no-release` and then `release`, and that the mitigation choice was to fix KtsuBuild first rather than change the workflow.
