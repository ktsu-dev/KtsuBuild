# Test without building, and release the commit that carries the version

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give `ktsubuild test run` a `--no-build` option so a CI matrix can build once per platform and reuse that output, and make a standalone `ktsubuild release` target the commit that actually carries the version it publishes.

**Architecture:** Two independent changes in one round, both small, both in areas the previous round already covered with a differential harness. The first threads an option through `TestProjectAsync` into the `dotnet test` argument string. The second sets `Configuration.ReleaseHash` from the current commit in the release path, before version resolution, so a standalone release behaves the way `ci` already does.

**Tech Stack:** C#, .NET 10, System.CommandLine, MSTest, NSubstitute.

**Spec:** none. Both items were parked findings from `docs/superpowers/plans/2026-08-24-pipeline-extraction.md`.

## Why these two

**Build duplication.** ImGuiApp's parallel test matrix was measured on 2026-08-24: 28 cells, 108.9 cell-minutes, roughly 193 billed. The cheapest Windows cell still costs several minutes while doing almost no testing, so most of the 84.5 Windows cell-minutes is the same tree rebuilt 14 times. The spec for that work already described the fix, building once per platform and having that platform's cells download the output, and said not to pull the lever until the numbers demanded it. They now do. The lever needs `dotnet test --no-build`, which the tool does not currently expose, and reimplementing the coverage flags in YAML was explicitly rejected because the second copy is the one that goes stale.

**Release target.** In the split workflow shape, `ci --no-test --no-release` commits the metadata and `release` then publishes. `release` targets `Configuration.ReleaseHash`, which `BuildConfigurationProvider` seeds from `GITHUB_SHA`, the commit that triggered the run. That is the commit *before* the metadata commit, so the tag points at a tree whose VERSION.md predates the bump. ImGuiApp's `v3.11.0` has this today. `ci` does not have the problem, because `UpdateMetadataAsync` overwrites `ReleaseHash` with the metadata commit.

## Global Constraints

- Tabs for indentation in C# files. Match the line endings of the file being edited.
- File-scoped namespaces. Using directives inside the namespace. Braces on every control flow statement. Explicit accessibility modifiers. No `this.` qualifiers. Nullable reference types enabled. Warnings as errors, and unused parameters (IDE0060) are errors.
- US English. Prose in comments and XML docs must not use em dashes, en dashes, or semicolons joining clauses, and must not coin hyphenated labels.
- MSTest with semantic asserts. Assert counts with `Assert.AreEqual(n, x.Count)`. `Assert.HasCount` and `Assert.IsEmpty` appear nowhere in this suite and must not be introduced.
- No global warning suppressions. Only narrow, justified ones matching the existing `#pragma warning disable CA1031` style.
- **Never pass `--nologo` to `dotnet test`.** It reports `total: 0` with exit code 5 while every test passes.
- **Building rewrites `.editorconfig`.** Run `git checkout .editorconfig` before every commit, stage files by name, never `git add -A`.
- Work on branch `feat/no-build-and-release-target`, cut from `main`.
- The suite stands at 405 tests.
- **`ci` must not change.** A differential harness proved it byte-identical across 17 cases in the previous round. Task 2 changes code near the release path, so it must be re-gated on that harness.
- Commit messages carry a version tag. No Co-Authored-By lines.

## File Structure

| File | Responsibility |
| --- | --- |
| `KtsuBuild/Abstractions/IDotNetService.cs` (modify) | `TestProjectAsync` gains the option. |
| `KtsuBuild/DotNet/DotNetService.cs` (modify) | Thread it into the argument string. |
| `KtsuBuild.Tests/DotNet/DotNetServiceTests.cs` (modify) | Cover the argument in both directions. |
| `KtsuBuild.Tool/Commands/TestCommand.cs` (modify) | Add the CLI option and pass it. |
| `KtsuBuild.Tool/Program.cs` (modify) | Read the option. |
| `KtsuBuild.Tool/Commands/ReleaseCommand.cs` (modify) | Target the commit carrying the version. |
| `README.md` (modify) | Document both. |

---

### Task 1: Add --no-build to test run

**Files:**
- Modify: `KtsuBuild/Abstractions/IDotNetService.cs`, `KtsuBuild/DotNet/DotNetService.cs`, `KtsuBuild.Tests/DotNet/DotNetServiceTests.cs`, `KtsuBuild.Tool/Commands/TestCommand.cs`, `KtsuBuild.Tool/Program.cs`

**Interfaces:**
- Produces: `ktsubuild test run --project <path> --no-build`, and `IDotNetService.TestProjectAsync(..., bool noBuild = false, ...)`. Task 3 documents both.

- [ ] **Step 1: Read what you are threading through**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
sed -n '130,200p' KtsuBuild/DotNet/DotNetService.cs
grep -n "TestProjectAsync" KtsuBuild/Abstractions/IDotNetService.cs
```

`RunTestsAsync` is shared by `TestAsync`, which tests the whole workspace, and `TestProjectAsync`, which tests one project. **Only `TestProjectAsync` gains the option.** `TestAsync` keeps its current signature and behavior, because nothing needs the option there and widening it would change a call `ci` depends on.

- [ ] **Step 2: Write the failing tests**

Append to `KtsuBuild.Tests/DotNet/DotNetServiceTests.cs`, following the file's existing pattern for capturing the emitted argument string:

```csharp
	[TestMethod]
	public async Task TestProjectAsyncAddsNoBuildWhenAsked()
	{
		string captured = await CaptureTestProjectArgsAsync(noBuild: true).ConfigureAwait(false);

		StringAssert.Contains(captured, "--no-build");
	}

	// The default must stay a building run. A caller that forgets the option and silently skips
	// the build would test whatever happened to be in bin from a previous run.
	[TestMethod]
	public async Task TestProjectAsyncOmitsNoBuildByDefault()
	{
		string captured = await CaptureTestProjectArgsAsync(noBuild: false).ConfigureAwait(false);

		Assert.IsFalse(captured.Contains("--no-build", StringComparison.Ordinal), captured);
	}
```

Write `CaptureTestProjectArgsAsync` as a private helper in that class if no equivalent exists, matching how the file's other tests substitute `IProcessRunner` and read back the argument string. Read the surrounding tests first and follow them rather than inventing a new approach.

- [ ] **Step 3: Run the tests to verify they fail**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet test
```

Expected: a compile error, because `TestProjectAsync` has no `noBuild` parameter yet.

- [ ] **Step 4: Implement**

Add `bool noBuild = false` to `TestProjectAsync` on both the interface and the implementation, placed **before** the `CancellationToken` parameter and after `coverageOutputPath`, so existing positional callers keep compiling. Thread it into `RunTestsAsync` and append `--no-build` to the argument string only when it is set.

Keep the argument order stable: append `--no-build` at the end of the existing string rather than inserting it, so the diff to the emitted command is additive and easy to read in a CI log.

Document on the interface what the option means and what it requires of the caller, in the `<remarks>`: the project must already be built for the configuration being tested, and the outputs must be present in the same workspace path, because `dotnet test --no-build` reads `obj/project.assets.json`, which holds absolute paths.

- [ ] **Step 5: Run the tests to verify they pass**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet test
```

Expected: PASS, 407 total (405 plus 2).

- [ ] **Step 6: Verify the tests discriminate**

Invert the condition in the implementation so `--no-build` is appended when `noBuild` is false, run the two tests, and confirm both fail. Restore and confirm both pass. Report what you broke and which assertions fired.

- [ ] **Step 7: Add the CLI option**

In `KtsuBuild.Tool/Commands/TestCommand.cs`, add an option to the `run` subcommand only:

```csharp
	/// <summary>
	/// Gets the option that skips building before the test run.
	/// </summary>
	public static Option<bool> NoBuild { get; } = new("--no-build")
	{
		Description = "Skip building before running the tests, for a caller that has already built this project",
		DefaultValueFactory = _ => false,
	};
```

Register it on `RunCommand`, read it in `Program.cs`'s run handler, and pass it to `TestProjectAsync`. Do **not** add it to `test list`, which builds nothing.

- [ ] **Step 8: Verify live, each state separately**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet build
./KtsuBuild.Tool/bin/Debug/net10.0/ktsu.KtsuBuild.Tool.exe test run --help
./KtsuBuild.Tool/bin/Debug/net10.0/ktsu.KtsuBuild.Tool.exe test list --help
```

Expected: `--no-build` present on `run`, absent on `list`.

Then prove it reaches `dotnet`. Run the tool against this repository's own test project twice, once with the flag and once without, with `--verbose`, and grep the emitted `dotnet test` command line in each. Report both command strings. The one with the flag must carry `--no-build` and the one without must not.

- [ ] **Step 9: Commit**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git add KtsuBuild/Abstractions/IDotNetService.cs KtsuBuild/DotNet/DotNetService.cs KtsuBuild.Tests/DotNet/DotNetServiceTests.cs KtsuBuild.Tool/Commands/TestCommand.cs KtsuBuild.Tool/Program.cs
git commit -m "feat: add --no-build to test run [minor]"
```

---

### Task 2: Release the commit that carries the version

**Files:**
- Modify: `KtsuBuild.Tool/Commands/ReleaseCommand.cs`

**Interfaces:**
- Consumes: nothing from Task 1. The two tasks are independent.
- Produces: a standalone `release` that targets the current commit.

- [ ] **Step 1: Understand the hazard before writing anything**

`Configuration.ReleaseHash` serves two roles at once, and this is the thing to get right:

1. It is the **release target**, becoming `--target` on `gh release create`.
2. It is the **version analysis boundary**, passed to `GetVersionInfoAsync` by `ResolveVersionAsync`.

In `ci` both roles land on the metadata commit, because `UpdateMetadataAsync` overwrites `ReleaseHash` before resolution runs. That ordering was proved load-bearing by a differential harness in the previous round, and an earlier attempt to reorder it produced 15 divergences.

So the fix for `release` must set `ReleaseHash` **before** `ResolveVersionAsync`, giving a standalone release the same relationship between the two roles that `ci` already has. Setting it afterwards would move the target without moving the boundary, which is a third behavior nobody has reasoned about.

```bash
cd /c/dev/ktsu-dev/KtsuBuild
cat KtsuBuild.Tool/Commands/ReleaseCommand.cs
grep -n "ReleaseHash" KtsuBuild/Pipeline/PipelineService.cs
```

- [ ] **Step 2: Set the target from the current commit**

In `ReleaseCommand`, after the `ShouldRelease` and `dryRun` early returns and **before** `ResolveVersionAsync`, set `Configuration.ReleaseHash` from the current commit hash. Use the git service the command already has access to, or obtain one the way the surrounding code does.

Comment it with the reason, not the mechanism:

```csharp
			// `ci` releases the metadata commit, because UpdateMetadataAsync overwrites this before
			// resolution runs. A standalone release has no metadata stage, so it would otherwise
			// target GITHUB_SHA, the commit that triggered the run, which in a split pipeline is the
			// commit before the metadata commit. The tag would then point at a tree whose VERSION.md
			// predates the version being published. Setting it here, before resolution, gives this
			// path the same relationship between the release target and the analysis boundary that
			// `ci` has.
```

- [ ] **Step 3: Confirm `ci` did not move**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet build
dotnet test
```

Then run the differential harness from the previous round, at
`C:\Users\matth\AppData\Local\Temp\claude\C--dev-ktsu-dev-ImageGui\33d4d4e9-691c-4940-af2b-b84aba29be91\scratchpad\diffharness`.
It compares the pre-refactor `CiCommand` against the current one across 17 cases. Expected: 0 divergences, because this task touches only `ReleaseCommand`.

**Run one positive control** so a zero is not a broken harness, and report what you chose and what it produced. If the harness no longer builds because the tree moved under it, say so plainly rather than skipping the check.

- [ ] **Step 4: Prove the behavior by execution**

Build a scratch repository that reaches the release path, the way the previous round's reviews did: a tag on an ancestor commit, `GITHUB_REF=refs/heads/main`, and a stub `gh` on PATH that logs its arguments and reports a non-fork `ktsu-dev` repository so `IsOfficial` passes.

Then simulate the split pipeline. Set `GITHUB_SHA` to the commit *before* an extra commit you add on top, so `GITHUB_SHA` and HEAD deliberately differ the way they do after a metadata commit. Run `release` and read the stub's log.

Expected: `gh release create` receives `--target <HEAD>`, not `--target <GITHUB_SHA>`. Report both hashes and which one the tag targeted.

**Positive control:** revert your change, rebuild, run the identical scenario, and confirm the target **is** `GITHUB_SHA`. Report both runs. Without this the check cannot distinguish a fix from a scenario that never differed.

- [ ] **Step 5: Commit**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git add KtsuBuild.Tool/Commands/ReleaseCommand.cs
git commit -m "fix: release the commit that carries the version [patch]"
```

---

### Task 3: Document and open the pull request

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Document both changes**

`README.md` line endings disagree between the working tree and the committed blobs in this repository, so read the file and match what you find rather than assuming.

Under `#### \`test run\``, add `--no-build` to the options list and add a short paragraph saying what the caller must guarantee: the project must already be built for the configuration being tested, in the same workspace path, because `dotnet test --no-build` reads `obj/project.assets.json` and that file holds absolute paths. Say that this exists so a CI matrix can build once per platform and reuse the output across that platform's test cells.

Under `### \`release\``, extend the existing paragraph to say it publishes against the current commit, and why that matters after a metadata commit.

- [ ] **Step 2: Verify the branch**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git status --short
git log --oneline main..HEAD
dotnet build
dotnet test
```

Expected: clean tree, three commits, 0 warnings, 0 errors, 407 passing.

- [ ] **Step 3: Open the pull request**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git push -u origin feat/no-build-and-release-target
gh pr create --base main --head feat/no-build-and-release-target \
  --title "Add --no-build to test run, and release the commit carrying the version" \
  --body "Two parked findings from the pipeline extraction. test run gains --no-build so a CI matrix can build once per platform and reuse that output across the platform's cells, which is what removes the build duplication measured at roughly 70 of 94 Windows cell-minutes in ImGuiApp. A standalone release now targets the current commit rather than GITHUB_SHA, so after a metadata commit the tag points at the tree that actually carries the version being published. ci is unchanged and was re-gated on the differential harness from the previous round."
```

- [ ] **Step 4: Stop and report**

Merging and releasing are the repository owner's calls. Report the PR URL and stop.

State that the ImGuiApp workflow restructure that consumes `--no-build` is a separate change, blocked on this reaching nuget.org, and that the release-target fix applies to ImGuiApp's next release rather than retroactively to `v3.11.0`.
