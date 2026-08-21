# CI skip flags Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `--no-test` and `--no-release` to `ktsubuild ci` so a restructured workflow can run the full pipeline (metadata, topics, version gate, restore, build, step outputs) without running tests or publishing, then release separately after the SonarCloud quality gate.

**Architecture:** Two boolean flags on `CiCommand`, each suppressing one step of `ExecutePipelineAsync`. The release decision is extracted into a pure static class in the `KtsuBuild` library so it can be tested, because `ExecutePipelineAsync` constructs its services concretely and cannot be unit tested. The extraction encodes the load-bearing invariant in the signature: the `should_release` step output is computed **without** the suppression flag, so `--no-release` stops the release from running in this job without telling later jobs that no release should happen.

**Tech Stack:** C#, .NET 10, System.CommandLine, MSTest, NSubstitute.

**Spec:** `C:\dev\ktsu-dev\ImGuiApp\docs\superpowers\specs\2026-08-21-ci-parallel-test-matrix-design.md`

## Why this plan exists

The spec decided the restructured workflow would stop calling `ktsubuild ci` and call restore, build, and test directly, on the grounds that "`ktsubuild release` still handles pack, publish, and release, so no release automation is lost."

That is false. Reading `CiCommand.cs` and `ReleaseCommand.cs` against each other, `ci` does five things `release` does not:

| Behavior | Lives in | Reachable from the CLI otherwise |
| --- | --- | --- |
| Update and commit VERSION.md, CHANGELOG.md, LICENSE.md, COPYRIGHT.md, AUTHORS.md | `CiCommand` via `MetadataService.UpdateAllAsync` | Yes, `metadata update` |
| Update GitHub repository topics from TAGS.md | `CiCommand.UpdateRepositoryTopicsAsync` | No |
| Gate the release on the version increment, which is how `[skip ci]` works | `CiCommand`, `VersionType.Skip` | No |
| Honor a forced `--version-bump` | `CiCommand.ParseVersionBump` | No, `metadata update` has no such option |
| Write the `version`, `release_hash`, `should_release`, `build_skipped` step outputs | `CiCommand.WriteStepOutputs` | No, `CiCommand` is the only caller of `GitHubActionsOutput` |

`ReleaseService.ExecuteReleaseAsync` packs, publishes, and creates the GitHub release unconditionally once called. Its only gate is `BuildConfiguration.ShouldRelease`, which means "on main, not tagged, official repo" and nothing about whether the version moved.

Two jobs in ImGuiApp's real `dotnet.yml` consume those outputs: `winget` and `security`. Both are `needs: build`, both are `if: needs.build.outputs.should_release == 'true'`, and both check out `needs.build.outputs.release_hash`. Without the outputs, neither job ever runs and neither reports a failure. The spec's job graph does not mention `security` at all.

So the restructured release job calls `ktsubuild ci --no-test --no-release` inside the SonarCloud begin and end window, then `ktsubuild release` after it, gated on the `should_release` output that `ci` just wrote. Every one of the five behaviors is preserved with no rule reimplemented in YAML, and the quality gate blocks the release, which is what the spec wanted and what today's shape does not do.

This plan delivers only the KtsuBuild half. The workflow restructure is a separate plan, written after this ships to nuget.org, because CI installs the tool with `dotnet tool install`.

## Global Constraints

- Tabs for indentation in C# files. Match the line endings of the file being edited rather than assuming a style.
- File-scoped namespaces. Using directives inside the namespace. Braces on every control flow statement. Explicit accessibility modifiers. No `this.` qualifiers. Nullable reference types enabled. Warnings as errors.
- US English in all code, comments, and documentation.
- MSTest with semantic asserts. This project asserts counts with `Assert.AreEqual(n, x.Count)` and substrings with `StringAssert.Contains`. `Assert.HasCount` and `Assert.IsEmpty` appear nowhere in `KtsuBuild.Tests` and must not be introduced.
- No global warning suppressions. Use the narrowest targeted suppression with a justification, matching the `#pragma warning disable CA1010` and `CA1031` pattern already in the command files.
- **Never pass `--nologo` to `dotnet test`.** It reports `total: 0` with exit code 5 while every test passes. Plain `dotnet test` is correct.
- **Building rewrites `.editorconfig`.** Run `git checkout .editorconfig` before every commit, and stage files by name. Never `git add -A`.
- Commit messages carry a version tag: `[major]`, `[minor]`, `[patch]`, or `[pre]`.
- `KtsuBuild.Tests` references `KtsuBuild.csproj` only, not `KtsuBuild.Tool.csproj`. Anything that needs a unit test must live in the `KtsuBuild` library.
- Work on branch `feat/ci-skip-flags`, cut from `main` at `acba58b` or later.
- The suite stands at 375 tests before this plan starts.

## File Structure

| File | Responsibility |
| --- | --- |
| `KtsuBuild/Configuration/CiReleaseDecision.cs` (create) | Two pure functions: whether to execute the release in this run, and what the `should_release` output says. Separate so both are testable and so the difference between them is stated in one place. |
| `KtsuBuild.Tests/Configuration/CiReleaseDecisionTests.cs` (create) | Truth tables for both functions. |
| `KtsuBuild.Tool/Commands/GlobalOptions.cs` (modify) | Add the `NoRelease` option beside the existing `NoTest`. |
| `KtsuBuild.Tool/Commands/CiCommand.cs` (modify) | Accept the two flags, suppress the two steps, and route both release decisions through `CiReleaseDecision`. |
| `KtsuBuild.Tool/Program.cs` (modify) | Read the two options and pass them to the handler. |
| `README.md` (modify) | Document both flags under `ci`. |

---

### Task 1: Extract the release decision

Nothing observable changes in this task. It creates the tested seam that Task 2 needs, so that Task 2's behavior change is a one-line edit against proven logic rather than a new conditional written directly into an untestable method.

**Files:**
- Create: `KtsuBuild/Configuration/CiReleaseDecision.cs`
- Create: `KtsuBuild.Tests/Configuration/CiReleaseDecisionTests.cs`
- Modify: `KtsuBuild.Tool/Commands/CiCommand.cs:177` and `KtsuBuild.Tool/Commands/CiCommand.cs:282-289`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `KtsuBuild.Configuration.CiReleaseDecision.ShouldExecuteRelease(bool shouldRelease, bool releaseSkipped, bool suppressedByFlag) -> bool` and `KtsuBuild.Configuration.CiReleaseDecision.ShouldReleaseOutput(bool shouldRelease, bool releaseSkipped) -> string`. Task 2 calls both with those exact names and parameter orders.

- [ ] **Step 1: Write the failing tests**

Create `KtsuBuild.Tests/Configuration/CiReleaseDecisionTests.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Configuration;

using KtsuBuild.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class CiReleaseDecisionTests
{
	[TestMethod]
	[DataRow(false, false, false, false, DisplayName = "not a release build")]
	[DataRow(false, false, true, false, DisplayName = "not a release build, flag set")]
	[DataRow(false, true, false, false, DisplayName = "not a release build, version skipped")]
	[DataRow(false, true, true, false, DisplayName = "not a release build, version skipped, flag set")]
	[DataRow(true, false, false, true, DisplayName = "release build, nothing suppressing it")]
	[DataRow(true, false, true, false, DisplayName = "release build suppressed by the flag")]
	[DataRow(true, true, false, false, DisplayName = "release build suppressed by the version gate")]
	[DataRow(true, true, true, false, DisplayName = "release build suppressed by both")]
	public void ShouldExecuteReleaseCoversEveryCombination(bool shouldRelease, bool releaseSkipped, bool suppressedByFlag, bool expected)
	{
		bool actual = CiReleaseDecision.ShouldExecuteRelease(shouldRelease, releaseSkipped, suppressedByFlag);

		Assert.AreEqual(expected, actual);
	}

	[TestMethod]
	[DataRow(false, false, "false", DisplayName = "not a release build")]
	[DataRow(false, true, "false", DisplayName = "not a release build, version skipped")]
	[DataRow(true, false, "true", DisplayName = "release build, version moved")]
	[DataRow(true, true, "false", DisplayName = "release build, version skipped")]
	public void ShouldReleaseOutputReportsWhetherAReleaseIsWarranted(bool shouldRelease, bool releaseSkipped, string expected)
	{
		string actual = CiReleaseDecision.ShouldReleaseOutput(shouldRelease, releaseSkipped);

		Assert.AreEqual(expected, actual);
	}

	// The whole point of the split. A run that suppresses its own release must still tell later
	// jobs that a release is warranted, because a later job is the one that performs it. If this
	// ever becomes false, the winget and security jobs stop running and report nothing.
	[TestMethod]
	public void SuppressingTheReleaseDoesNotChangeWhatLaterJobsAreTold()
	{
		Assert.IsFalse(CiReleaseDecision.ShouldExecuteRelease(shouldRelease: true, releaseSkipped: false, suppressedByFlag: true));
		Assert.AreEqual("true", CiReleaseDecision.ShouldReleaseOutput(shouldRelease: true, releaseSkipped: false));
	}
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet test
```

Expected: a compile error, `The name 'CiReleaseDecision' does not exist in the current context` or `type or namespace name 'CiReleaseDecision' could not be found`. A compile failure is the correct failure here, because the type does not exist yet.

- [ ] **Step 3: Write the implementation**

Create `KtsuBuild/Configuration/CiReleaseDecision.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Configuration;

/// <summary>
/// Decides what the CI pipeline does about the release, separately from doing it.
/// </summary>
/// <remarks>
/// Two questions look the same and are not. "Should this run publish?" may be answered no because
/// the caller asked for the release to happen in a later job, while "should a release happen at
/// all?" is still yes. Consuming workflows gate their publishing jobs on the second answer, so
/// folding the first into it would leave those jobs skipped and silent.
/// <para>
/// <see cref="ShouldReleaseOutput"/> therefore takes no suppression parameter. That is deliberate:
/// the invariant is carried by the signature rather than by a comment, so a later edit cannot
/// accidentally feed the flag into the reported answer without changing the shape of the call.
/// </para>
/// </remarks>
public static class CiReleaseDecision
{
	/// <summary>
	/// Determines whether this run performs the release itself.
	/// </summary>
	/// <param name="shouldRelease">Whether the build configuration permits a release, meaning an official repo, on main, untagged.</param>
	/// <param name="releaseSkipped">Whether the version increment suppressed the release, which is how <c>[skip ci]</c> and a run with no meaningful changes behave.</param>
	/// <param name="suppressedByFlag">Whether the caller asked this run not to release, leaving it to a later step or job.</param>
	/// <returns><see langword="true"/> only when a release is warranted and nothing suppresses it.</returns>
	public static bool ShouldExecuteRelease(bool shouldRelease, bool releaseSkipped, bool suppressedByFlag) =>
		shouldRelease && !releaseSkipped && !suppressedByFlag;

	/// <summary>
	/// Determines what the <c>should_release</c> step output reports to later jobs.
	/// </summary>
	/// <param name="shouldRelease">Whether the build configuration permits a release, meaning an official repo, on main, untagged.</param>
	/// <param name="releaseSkipped">Whether the version increment suppressed the release.</param>
	/// <returns><c>"true"</c> when a release is warranted, otherwise <c>"false"</c>, as the literal text GitHub Actions compares against.</returns>
	public static string ShouldReleaseOutput(bool shouldRelease, bool releaseSkipped) =>
		shouldRelease && !releaseSkipped ? "true" : "false";
}
```

- [ ] **Step 4: Route the existing call sites through it**

In `KtsuBuild.Tool/Commands/CiCommand.cs`, replace the condition at line 177:

```csharp
		// Release workflow
		if (buildConfig.ShouldRelease && !skipRelease)
		{
			await releaseService.ExecuteReleaseAsync(buildConfig, workspace, configuration, cancellationToken).ConfigureAwait(false);
		}
```

with:

```csharp
		// Release workflow
		if (CiReleaseDecision.ShouldExecuteRelease(buildConfig.ShouldRelease, skipRelease, suppressedByFlag: false))
		{
			await releaseService.ExecuteReleaseAsync(buildConfig, workspace, configuration, cancellationToken).ConfigureAwait(false);
		}
```

and in `WriteStepOutputs`, replace the `should_release` line:

```csharp
			new("should_release", (!releaseSkipped && buildConfig.ShouldRelease) ? "true" : "false"),
```

with:

```csharp
			new("should_release", CiReleaseDecision.ShouldReleaseOutput(buildConfig.ShouldRelease, releaseSkipped)),
```

`KtsuBuild.Configuration` is already imported at the top of `CiCommand.cs`, so no using directive is needed. Verify that before adding one.

- [ ] **Step 5: Run the tests to verify they pass**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet test
```

Expected: PASS, total 388 (375 pre-existing plus 13 new: 8 plus 4 data rows plus 1 method).

- [ ] **Step 6: Prove the truth-table tests discriminate**

A passing truth table proves nothing on its own if the implementation happens to agree with a wrong table. Break the implementation and confirm the tests catch it.

Temporarily change `ShouldExecuteRelease` to ignore its flag:

```csharp
	public static bool ShouldExecuteRelease(bool shouldRelease, bool releaseSkipped, bool suppressedByFlag) =>
		shouldRelease && !releaseSkipped;
```

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet test
```

Expected: FAIL. Specifically the row "release build suppressed by the flag" and `SuppressingTheReleaseDoesNotChangeWhatLaterJobsAreTold`. Record the failing test names in the report.

Then revert that edit and re-run:

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet test
```

Expected: PASS, 388.

- [ ] **Step 7: Commit**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git add KtsuBuild/Configuration/CiReleaseDecision.cs KtsuBuild.Tests/Configuration/CiReleaseDecisionTests.cs KtsuBuild.Tool/Commands/CiCommand.cs
git commit -m "refactor: extract the CI release decision behind a tested seam [patch]"
```

---

### Task 2: Add --no-test and --no-release to ci

**Files:**
- Modify: `KtsuBuild.Tool/Commands/GlobalOptions.cs:65` (append beside `NoTest`)
- Modify: `KtsuBuild.Tool/Commands/CiCommand.cs` (constructor, `CreateHandler`, `ExecutePipelineAsync`)
- Modify: `KtsuBuild.Tool/Program.cs:60-74` (`AddCiCommand`)

**Interfaces:**
- Consumes: `CiReleaseDecision.ShouldExecuteRelease` and `CiReleaseDecision.ShouldReleaseOutput` from Task 1.
- Produces: `ktsubuild ci --no-test` and `ktsubuild ci --no-release` on the command line. The workflow plan that follows depends on both names exactly, and on `--no-release` leaving the `should_release` output untouched.

- [ ] **Step 1: Add the option**

In `KtsuBuild.Tool/Commands/GlobalOptions.cs`, after the `NoTest` property and before the closing brace:

```csharp

	/// <summary>
	/// Gets the no-release option.
	/// </summary>
	public static Option<bool> NoRelease { get; } = new("--no-release")
	{
		Description = "Skip the release step, leaving the release to a later step or job",
		DefaultValueFactory = _ => false,
	};
```

Adding a property to `GlobalOptions` does not attach it to any command. Each command opts in through `Options.Add`, so `build`, `release`, and the rest are unaffected.

- [ ] **Step 2: Introduce an options record for the handler**

`CiCommand.CreateHandler` currently takes `Func<string, string, bool, bool, string, CancellationToken, Task<int>>`. Adding two more booleans gives four adjacent `bool` parameters (`verbose`, `dryRun`, `noTest`, `noRelease`) in a positional delegate. A transposed pair at the call site compiles cleanly and misbehaves silently, and no test of the flags can distinguish `noTest` from `noRelease` when both are passed together, which is how the workflow passes them.

This is a deliberate deviation from the positional-`Func` shape the other commands use. The hazard is silent and the compiler cannot catch it, so the parameters get names at the call site.

At the top of the `CiCommand` class body, before `CreateHandler`:

```csharp
	/// <summary>
	/// The inputs to a CI pipeline run. A record rather than positional parameters because four
	/// adjacent booleans in a delegate signature can be transposed without a compiler error, and
	/// a run that passes several of them at once cannot tell the transposition apart from correct
	/// wiring.
	/// </summary>
	/// <param name="Workspace">The workspace or repository path.</param>
	/// <param name="Configuration">The build configuration, Debug or Release.</param>
	/// <param name="Verbose">Whether to enable verbose logging.</param>
	/// <param name="DryRun">Whether to report what would happen without making changes.</param>
	/// <param name="VersionBump">The forced version bump type, or <c>auto</c> to detect it.</param>
	/// <param name="NoTest">Whether to skip the test step, for a pipeline whose tests run elsewhere.</param>
	/// <param name="NoRelease">Whether to skip the release step, leaving it to a later step or job.</param>
	public sealed record CiOptions(
		string Workspace,
		string Configuration,
		bool Verbose,
		bool DryRun,
		string VersionBump,
		bool NoTest,
		bool NoRelease);
```

- [ ] **Step 3: Register the options on the command**

In the `CiCommand` constructor, after `Options.Add(GlobalOptions.VersionBump);`:

```csharp
		Options.Add(GlobalOptions.NoTest);
		Options.Add(GlobalOptions.NoRelease);
```

- [ ] **Step 4: Change the handler to take the record**

Replace the `CreateHandler` signature and body:

```csharp
	public static Func<CiOptions, CancellationToken, Task<int>> CreateHandler(
		IProcessRunner processRunner,
		IBuildLogger logger)
	{
		return async (options, cancellationToken) =>
		{
			logger.VerboseEnabled = options.Verbose;
			BuildEnvironment.Initialize();

			if (options.DryRun)
			{
				logger.WriteWarning("DRY RUN MODE - No changes will be made");
			}

			logger.WriteStepHeader("Starting CI/CD Pipeline");

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				return await ExecutePipelineAsync(processRunner, logger, options, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				logger.WriteError($"CI/CD pipeline failed: {ex.Message}");
				return 1;
			}
#pragma warning restore CA1031
		};
	}
```

- [ ] **Step 5: Change ExecutePipelineAsync to take the record**

Change the signature:

```csharp
	private static async Task<int> ExecutePipelineAsync(
		IProcessRunner processRunner,
		IBuildLogger logger,
		CiOptions options,
		CancellationToken cancellationToken)
	{
```

Then inside the method, replace every use of the old parameters with the record's properties: `workspace` becomes `options.Workspace`, `configuration` becomes `options.Configuration`, `dryRun` becomes `options.DryRun`, and `versionBump` becomes `options.VersionBump`. Read the whole method and change every occurrence, including the ones in the calls to `configProvider.CreateFromEnvironmentAsync`, `metadataService.UpdateAllAsync`, `UpdateRepositoryTopicsAsync`, `versionCalculator.GetVersionInfoAsync`, `processRunner.RunWithCallbackAsync`, `dotNetService.RestoreAsync`, `dotNetService.BuildAsync`, `ExecuteIosValidationAsync`, and `releaseService.ExecuteReleaseAsync`. The compiler finds any that are missed, because the old names no longer exist.

- [ ] **Step 6: Suppress the test step**

Replace the test call, currently at line 164:

```csharp
		await dotNetService.TestAsync(workspace, configuration, "coverage", cancellationToken).ConfigureAwait(false);
```

with:

```csharp
		// A caller that runs the tests elsewhere, such as a workflow that fans them across a
		// matrix, still needs everything around them: metadata, the version gate, a compilation
		// inside the SonarQube begin and end window, and the step outputs.
		if (!options.NoTest)
		{
			await dotNetService.TestAsync(options.Workspace, options.Configuration, "coverage", cancellationToken).ConfigureAwait(false);
		}
```

The argument list is unchanged from the line being replaced. Only the receiver names change, from the old parameters to the record's properties, and the call is wrapped in the `if`.

- [ ] **Step 7: Suppress the release step**

Replace the release condition Task 1 introduced:

```csharp
		if (CiReleaseDecision.ShouldExecuteRelease(buildConfig.ShouldRelease, skipRelease, suppressedByFlag: false))
```

with:

```csharp
		if (CiReleaseDecision.ShouldExecuteRelease(buildConfig.ShouldRelease, skipRelease, suppressedByFlag: options.NoRelease))
```

Leave `WriteStepOutputs(buildConfig, releaseSkipped: skipRelease)` exactly as it is. `options.NoRelease` must not reach it. That is the whole contract: this run does not release, and later jobs are still told a release is warranted.

- [ ] **Step 8: Wire the options in Program.cs**

Replace the body of `AddCiCommand`:

```csharp
	private static void AddCiCommand(RootCommand rootCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		CiCommand command = new();
		Func<CiCommand.CiOptions, CancellationToken, Task<int>> handler = CiCommand.CreateHandler(processRunner, logger);
		command.SetAction(async (parseResult, ct) =>
		{
			CiCommand.CiOptions options = new(
				Workspace: parseResult.GetValue(GlobalOptions.Workspace)!,
				Configuration: parseResult.GetValue(GlobalOptions.Configuration)!,
				Verbose: parseResult.GetValue(GlobalOptions.Verbose),
				DryRun: parseResult.GetValue(GlobalOptions.DryRun),
				VersionBump: parseResult.GetValue(GlobalOptions.VersionBump)!,
				NoTest: parseResult.GetValue(GlobalOptions.NoTest),
				NoRelease: parseResult.GetValue(GlobalOptions.NoRelease));
			return await handler(options, ct).ConfigureAwait(false);
		});
		rootCommand.Subcommands.Add(command);
	}
```

Named arguments are required here, not optional style. They are what makes a transposition visible in review.

- [ ] **Step 9: Build and run the suite**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet build
dotnet test
```

Expected: build succeeds with 0 warnings and 0 errors, tests PASS at 388.

- [ ] **Step 10: Verify each flag alone, not just together**

This is the step that catches a transposed pair. Passing both flags at once cannot tell `NoTest` from `NoRelease`, because the observable result is identical either way. Each flag has to be exercised on its own.

Run against a throwaway clone of this repository on the feature branch, not against your working tree and not against a synthetic project. A real repository exercises the real `BuildConfigurationProvider`, and a clone keeps the metadata files `ci` regenerates out of the tree you are about to commit.

On the feature branch `IsMain` is false, so `ShouldRelease` is false and `shouldCommitMetadata` is false. Nothing packs, publishes, tags, commits, or pushes:

```bash
SCRATCH="C:/Users/matth/AppData/Local/Temp/claude/ci-flags"
rm -rf "$SCRATCH"
git clone --branch feat/ci-skip-flags C:/dev/ktsu-dev/KtsuBuild "$SCRATCH"
git -C "$SCRATCH" remote set-url origin https://github.com/ktsu-dev/KtsuBuild.git
git -C "$SCRATCH" log --oneline -1
```

The `remote set-url` matters. `IsOfficial` is derived from the remote, and a clone of a local path would not look like the real repository.

Then run each case, capturing the step outputs the way GitHub Actions does:

```bash
EXE="C:/dev/ktsu-dev/KtsuBuild/KtsuBuild.Tool/bin/Debug/net10.0/ktsu.KtsuBuild.Tool.exe"
for CASE in "" "--no-test" "--no-release" "--no-test --no-release"; do
  OUT="$SCRATCH/out-$(echo "$CASE" | tr -d ' -' | sed 's/^$/plain/').txt"
  : > "$OUT"
  GITHUB_OUTPUT="$OUT" "$EXE" ci --workspace "$SCRATCH" $CASE > "$SCRATCH/log.txt" 2>&1
  echo "=== case '${CASE:-none}' exit=$? ==="
  grep -c "=== Running Tests with Coverage ===" "$SCRATCH/log.txt" | sed 's/^/  test header count: /'
  echo "  outputs:"; sed 's/^/    /' "$OUT"
done
```

Expected, and all four must hold:

| Case | Test header | `should_release` output |
| --- | --- | --- |
| no flags | present | `false` |
| `--no-test` | absent | `false` |
| `--no-release` | **present** | `false` |
| `--no-test --no-release` | absent | `false` |

The discriminating row is `--no-release` on its own. If the test header disappears there, the two flags are transposed. Every case must also emit all four outputs (`version`, `release_hash`, `should_release`, `build_skipped`), because a workflow reads them whether or not this run released.

`should_release` is `false` in every row because the clone is on a feature branch, so `IsMain` is false. That is the correct value here and it is not evidence about either flag. The proof that `--no-release` leaves the output alone comes from Task 1's truth table, which covers the `shouldRelease: true` case this machine cannot reproduce without actually publishing.

The no-flag row runs the full suite inside the clone and takes a few minutes, including a restore. That is the cost of a real positive control, and it is the row proving the test step runs when nothing suppresses it.

Report the actual table, not the expected one.

- [ ] **Step 11: Confirm the flags appear in help**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
./KtsuBuild.Tool/bin/Debug/net10.0/ktsu.KtsuBuild.Tool.exe ci --help
```

Expected: both `--no-test` and `--no-release` listed with their descriptions. Also run `./KtsuBuild.Tool/bin/Debug/net10.0/ktsu.KtsuBuild.Tool.exe release --help` and confirm `--no-release` is **not** there, proving the new option did not leak onto other commands.

- [ ] **Step 12: Commit**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git add KtsuBuild.Tool/Commands/GlobalOptions.cs KtsuBuild.Tool/Commands/CiCommand.cs KtsuBuild.Tool/Program.cs
git commit -m "feat: add --no-test and --no-release to the ci command [minor]"
```

---

### Task 3: Document the flags

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: both flags from Task 2.
- Produces: nothing consumed later.

- [ ] **Step 1: Read the existing entry**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
grep -n "### \`ci\`" -A 30 README.md
```

The `ci` section already has an `**Options:**` list with `--dry-run` and `--version-bump`, and a numbered "Pipeline steps" list. Match that shape and heading level. `README.md` is CRLF in the working tree, so preserve CRLF when editing.

- [ ] **Step 2: Add the two options and say what they are for**

Add to the existing `**Options:**` list under `### \`ci\``:

```markdown
- `--no-test`: Skip the test step, for a pipeline that runs tests elsewhere
- `--no-release`: Skip the release step, leaving the release to a later step or job
```

Then add this paragraph after the "Pipeline steps" numbered list:

```markdown
`--no-test` and `--no-release` exist so a workflow can split the pipeline across jobs without
losing the parts that only `ci` performs: the metadata update and commit, the repository topics,
the version gate that makes `[skip ci]` work, and the `version`, `release_hash`, `should_release`,
and `build_skipped` step outputs. A workflow that fans tests across a matrix runs
`ktsubuild ci --no-test --no-release` for everything around the tests, then `ktsubuild release`
once its quality gate passes.

`--no-release` stops this run from releasing. It does not change what the `should_release` output
reports, because the job that reads that output is the one performing the release.
```

- [ ] **Step 3: Commit**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git add README.md
git commit -m "docs: document the ci skip flags [patch]"
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

Expected: a clean tree, three commits, a build with 0 warnings and 0 errors, and 388 passing tests.

- [ ] **Step 2: Open the pull request**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git push -u origin feat/ci-skip-flags
gh pr create --base main --head feat/ci-skip-flags \
  --title "Add ci skip flags for a split pipeline" \
  --body "Adds --no-test and --no-release to ktsubuild ci so a workflow can run the pipeline across several jobs without losing the metadata update, the repository topics, the version gate, or the step outputs, which only ci performs. Default behavior is unchanged, so repos still calling plain ci are unaffected. --no-release suppresses this run's release without changing the should_release output, because the job reading that output is the one that releases."
```

- [ ] **Step 3: Stop and report**

Merging and releasing are the repository owner's calls. Report the PR URL and stop.

State plainly in the report that the ImGuiApp workflow restructure is still blocked, now on this release rather than the previous one, and that its plan gets written against the released tool.

---

## Notes for the workflow plan that follows

Recorded here so they are not rediscovered:

- The release job shape is: `sonarscanner begin`, then `ktsubuild ci --no-test --no-release`, then `sonarscanner end`, then `ktsubuild release` gated on the `should_release` step output that `ci` wrote.
- `ImGuiApp`'s `dotnet.yml` has a `security` job as well as `winget`. Both are `needs: build`, both gate on `should_release`, and both check out `release_hash`. The spec's job graph omits `security`. Whatever job replaces `build` must expose all three outputs under a name both jobs are updated to reference.
- The `End SonarQube` step currently gates on `steps.pipeline.outputs.build_skipped != 'true'`. `ci` always writes `build_skipped=false`, and it still will.
- `ktsubuild test list` writes its errors to stdout, not stderr. Check the exit code before parsing stdout.
- `test run` passes the project path positionally to `dotnet test`. A path that does not exist reports zero tests rather than failing, so the matrix must come from `test list` rather than from hand-written paths.
- `ImGuiApp` has 14 test projects, not 15. `ImGui.App.iOS.SmokeTest` is a console executable with no test framework and is correctly excluded. `ios.yml` runs it separately.
- `ci` runs an iOS validation build on macOS hosts. `--no-test` does not suppress it, because it is a build rather than a test. The release job runs on Linux, where it reports and skips.
