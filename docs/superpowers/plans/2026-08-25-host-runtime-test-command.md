# Test against the host runtime only

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `ktsubuild test all`, which restores, builds, and tests every test project the host can build, pinned to the host's runtime identifier, so a test run stops copying native assets for fifteen platforms it will never load.

**Architecture:** One command that resolves the host runtime identifier, enumerates the host-buildable test projects, and runs each through a RID-pinned `dotnet test`. `release` is untouched and keeps publishing for all seven runtimes it already names explicitly. `build` is untouched and stays runtime-agnostic.

**Tech Stack:** C#, .NET 10, System.CommandLine, MSTest, NSubstitute.

**Spec:** none. This comes from measurements taken on 2026-08-25, recorded below.

## Why

ImGuiApp's CI test job takes 22.5 minutes on Windows against 8.0 on Linux for identical work. The cause is file copying, not compilation. `Setup .NET SDK`, a step that only downloads and extracts an archive and compiles nothing, runs 3.5 to 9 times slower on the hosted Windows runner across four measured runs.

That penalty lands hard here because the build is unusually copy-heavy. Measured on ImGuiApp: 13 GB of `bin` against 109 MB of `obj`. Almost none of it is compiler output. Every project's output carries the native binaries for all sixteen runtime identifiers the ImGui packages ship, `android-arm64` and `android-x64` among them, duplicated across Debug and Release. That is roughly 230 MB per project per configuration, and the smallest test project in the repository weighs 115 MB.

A test run needs the host's natives and nothing else. Pinning one test project to the host runtime took it from 115 MB to 39 MB with all 20 tests still passing, from a clean tree.

**This has to live in the tool rather than in each repository.** Passing a runtime identifier to a solution build fails outright:

```
error NETSDK1134: Building a solution with a specific RuntimeIdentifier is not supported.
If you would like to publish for a single RID, specify the RID at the individual project level instead.
```

So the pin has to be applied per project, which is what this command does, once, for every repository that installs the tool.

## Global Constraints

- Tabs for indentation in C# files. Match the line endings of the file being edited.
- File-scoped namespaces. Using directives inside the namespace. Braces on every control flow statement. Explicit accessibility modifiers. No `this.` qualifiers. Nullable reference types enabled. Warnings as errors, and unused parameters (IDE0060) are errors.
- US English. Prose in comments and XML docs must not use em dashes, en dashes, or semicolons joining clauses, and must not coin hyphenated labels.
- MSTest with semantic asserts. Assert counts with `Assert.AreEqual(n, x.Count)`. `Assert.HasCount` and `Assert.IsEmpty` appear nowhere in this suite and must not be introduced.
- No global warning suppressions. Only narrow, justified ones matching the existing `#pragma warning disable CA1031` style.
- **Never pass `--nologo` to `dotnet test`.** It reports `total: 0` with exit code 5 while every test passes.
- **Building rewrites `.editorconfig`.** Run `git checkout .editorconfig` before every commit, stage files by name, never `git add -A`.
- Work on branch `feat/host-runtime-test-command`, cut from `main`.
- The suite stands at 410 tests.
- **`ci`, `build`, and `release` must not change.** A differential harness proves `ci` byte-identical across 17 cases and any change here must keep it there.
- Commit messages carry a version tag. No Co-Authored-By lines.

## What must not change

`release` publishes for seven runtimes it names explicitly in `ReleaseService.PublishRuntimes`, passing `-r <runtime>` per publish. That is the multi-runtime path and it stays exactly as it is. This plan touches only how tests are built and run.

`build` stays runtime-agnostic, because it builds the whole solution and a solution build cannot take a runtime identifier at all.

---

### Task 1: Resolve the host runtime and pin a project test run

**Files:**
- Modify: `KtsuBuild/Abstractions/IDotNetService.cs`, `KtsuBuild/DotNet/DotNetService.cs`
- Modify: `KtsuBuild.Tests/DotNet/DotNetServiceTests.cs`

**Interfaces:**
- Produces: `TestProjectAsync` gains `bool hostRuntimeOnly = false` before the `CancellationToken`, and when set emits `-p:RuntimeIdentifier=<host> -p:SelfContained=false`. Task 2 calls it with that name.

- [ ] **Step 1: Read the argument construction and the existing option**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
sed -n '135,200p' KtsuBuild/DotNet/DotNetService.cs
grep -n "TestProjectAsync" KtsuBuild/Abstractions/IDotNetService.cs
```

Note `RunTestsAsync` is shared with `TestAsync`. **Only `TestProjectAsync` gains the option.** `TestAsync` builds the whole workspace at once and must stay runtime-agnostic, and it is covered by the differential harness.

- [ ] **Step 2: Write the failing tests**

Append to `KtsuBuild.Tests/DotNet/DotNetServiceTests.cs`, following the existing `CaptureTestProjectArgsAsync` pattern:

```csharp
	// A test run needs the host's native assets and nothing else. Without the pin, every project's
	// output carries natives for all sixteen runtime identifiers the packages ship, which measured
	// 115 MB for the smallest test project in ImGuiApp against 39 MB pinned.
	[TestMethod]
	public async Task TestProjectAsyncPinsTheHostRuntimeWhenAsked()
	{
		string captured = await CaptureTestProjectArgsAsync(hostRuntimeOnly: true).ConfigureAwait(false);

		StringAssert.Contains(captured, $"-p:RuntimeIdentifier={RuntimeInformation.RuntimeIdentifier}");
		StringAssert.Contains(captured, "-p:SelfContained=false");
	}

	// The default must stay runtime-agnostic. Pinning by default would change what every existing
	// caller produces, including the whole-workspace run that `ci` depends on.
	[TestMethod]
	public async Task TestProjectAsyncIsRuntimeAgnosticByDefault()
	{
		string captured = await CaptureTestProjectArgsAsync(hostRuntimeOnly: false).ConfigureAwait(false);

		Assert.IsFalse(captured.Contains("RuntimeIdentifier", StringComparison.Ordinal), captured);
		Assert.IsFalse(captured.Contains("SelfContained", StringComparison.Ordinal), captured);
	}
```

Extend the existing `CaptureTestProjectArgsAsync` helper to take the new argument rather than writing a second helper. Add `using System.Runtime.InteropServices;` inside the namespace if it is not already there.

- [ ] **Step 3: Run the tests to verify they fail**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet test
```

Expected: a compile error, because the parameter does not exist yet.

- [ ] **Step 4: Implement**

Add `bool hostRuntimeOnly = false` to `TestProjectAsync` on the interface and the implementation, placed after `noBuild` and before the `CancellationToken`. Thread it into `RunTestsAsync` and append, only when set:

```
 -p:RuntimeIdentifier={RuntimeInformation.RuntimeIdentifier} -p:SelfContained=false
```

`SelfContained=false` is required. Setting a runtime identifier without it makes the build self-contained, which pulls the whole framework into the output and would make the problem worse rather than better.

Document on the interface why the option exists and what it changes: the output goes to a runtime-specific directory, and only the host's native assets are copied.

- [ ] **Step 5: Run the tests to verify they pass**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet test
```

Expected: PASS, 412 total.

- [ ] **Step 6: Verify by reversal**

Invert the condition so the pin is applied when the flag is false, run the two new tests, and confirm both fail for the reason their names claim. Restore. Report what you broke and which assertions fired.

- [ ] **Step 7: Commit**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git add KtsuBuild/Abstractions/IDotNetService.cs KtsuBuild/DotNet/DotNetService.cs KtsuBuild.Tests/DotNet/DotNetServiceTests.cs
git commit -m "feat: allow a test run to pin the host runtime [minor]"
```

---

### Task 2: Add the test all command

**Files:**
- Modify: `KtsuBuild.Tool/Commands/TestCommand.cs`, `KtsuBuild.Tool/Program.cs`

**Interfaces:**
- Consumes: `TestProjectAsync(..., hostRuntimeOnly: true, ...)` from Task 1, and the existing `GetTestProjects`.
- Produces: `ktsubuild test all`.

- [ ] **Step 1: Read the existing subcommands**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
cat KtsuBuild.Tool/Commands/TestCommand.cs
grep -n "AddTestCommand" -A 40 KtsuBuild.Tool/Program.cs
```

`list` and `run` are already there. `all` joins them and follows the same shape: a nested private `Command` class, a static handler factory, and wiring in `Program.cs`.

- [ ] **Step 2: Add the subcommand**

`all` restores, builds, and tests every test project the host can build, pinned to the host runtime. It takes `--workspace`, `--configuration`, and `--verbose`, and no `--project`.

The handler must:

1. Call `BuildEnvironment.Initialize()`, as `run` does.
2. Get the test projects with `GetTestProjects(workspace)`.
3. **Filter to what the host can build.** `GetTestProjects` deliberately does not filter by host, because the command layer decides. Use the same platform rule the `discover` side uses: a `Neutral` project runs anywhere, a `Windows` project only on Windows, an `Ios` project only on macOS. Read `ProjectPlatform` and `RuntimeInformation` and filter accordingly.
4. Report how many projects will run and how many were skipped, naming the skipped ones and why. A project silently dropped is the failure this whole area keeps producing.
5. Run each through `TestProjectAsync(project, workspace, configuration, "coverage", noBuild: false, hostRuntimeOnly: true, ct)`.
6. **A failing project must not stop the ones after it.** Collect failures, continue, and return 1 at the end listing every project that failed. One run must report all of them.
7. Return 0 when every project passed, and 0 with a clear message when there were no test projects at all.

- [ ] **Step 3: Wire it in Program.cs**

Follow how `AddTestCommand` wires `list` and `run`. Read the option values and pass them to the handler.

- [ ] **Step 4: Build and check the surface**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet build
dotnet test
./KtsuBuild.Tool/bin/Debug/net10.0/ktsu.KtsuBuild.Tool.exe test --help
./KtsuBuild.Tool/bin/Debug/net10.0/ktsu.KtsuBuild.Tool.exe test all --help
```

Expected: `all` appears alongside `list` and `run`, takes no `--project`, and the suite is still at Task 1's total.

- [ ] **Step 5: Prove it against a real repository, measuring the effect**

Run it against ImGuiApp, which is the repository the measurements came from.

```bash
cd /c/dev/ktsu-dev/ImGuiApp
git status --short
rm -rf tests/ImGui.Color.Tests/bin tests/ImGui.Color.Tests/obj
/c/dev/ktsu-dev/KtsuBuild/KtsuBuild.Tool/bin/Debug/net10.0/ktsu.KtsuBuild.Tool.exe test all --workspace . --verbose 2>&1 | tail -30
```

Report the number of projects run, the number skipped and why, the reported test totals, and the exit code. Then measure what the pin achieved:

```bash
cd /c/dev/ktsu-dev/ImGuiApp
du -sm tests/ImGui.Color.Tests/bin/Release | cut -f1
```

Expected: about 39 MB, against 115 MB for the same project built runtime-agnostically. Report the actual figure.

**Do not commit anything in ImGuiApp.** It is a different repository and this is only a measurement. Confirm `git status --short` there is unchanged apart from `.editorconfig`, and restore that if it moved.

- [ ] **Step 6: Confirm `ci` did not move**

The differential harness is at
`C:\Users\matth\AppData\Local\Temp\claude\C--dev-ktsu-dev-ImageGui\33d4d4e9-691c-4940-af2b-b84aba29be91\scratchpad\diffharness`
and compares the pre-refactor `CiCommand` against the current one across 17 cases. This task adds a command and touches nothing `ci` calls, so expect 0 divergences. **Run one positive control of your own** so a zero is not simply a broken harness, and say what you chose. If the harness no longer builds, say so plainly rather than skipping the check.

- [ ] **Step 7: Commit**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git add KtsuBuild.Tool/Commands/TestCommand.cs KtsuBuild.Tool/Program.cs
git commit -m "feat: add test all, which tests against the host runtime only [minor]"
```

---

### Task 3: Document and open the pull request

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Document the command**

`README.md` line endings disagree between the working tree and the committed blobs here, so read the file and match what you find.

Add `#### \`test all\`` alongside `test list` and `test run`, matching their shape. Say what it does, that it pins the host runtime, and why that matters: a test run needs the host's native assets only, and without the pin a project's output carries natives for every runtime the packages ship. Give the measured figures, 115 MB against 39 MB for the smallest test project in ImGuiApp.

State plainly what it does not do. It does not replace `release`, which still publishes for every runtime it names. It is for testing, not for shipping.

- [ ] **Step 2: Verify the branch**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git status --short
git log --oneline main..HEAD
dotnet build
dotnet test
```

Expected: clean tree, four commits, 0 warnings, 0 errors, green suite.

- [ ] **Step 3: Open the pull request**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git push -u origin feat/host-runtime-test-command
gh pr create --base main --head feat/host-runtime-test-command \
  --title "Add test all, which tests against the host runtime only" \
  --body "Adds a command that restores, builds, and tests every host-buildable test project pinned to the host runtime identifier, so a test run stops copying native assets for the fifteen platforms it will never load. Measured on ImGuiApp: the smallest test project drops from 115 MB to 39 MB. release is untouched and still publishes for all seven runtimes it names. build is untouched and stays runtime-agnostic, because a solution build cannot take a runtime identifier at all (NETSDK1134)."
```

- [ ] **Step 4: Stop and report**

Merging and releasing are the repository owner's calls. Report the PR URL and stop.

State that ImGuiApp's workflow should switch its test job to `ktsubuild test all` once this is released, and that the expected effect is on the Windows job specifically, which was 22.5 minutes against 8.0 on Linux for the same work.
