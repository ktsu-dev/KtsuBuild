# Scoped test commands implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose three commands so a CI workflow can enumerate test projects, run one project at a time, and build without testing.

**Architecture:** Each command is a thin wrapper over service methods that already exist. `DotNetService` gains two members: one that lists test projects with the platform each is tied to, and one that runs a single project. `KtsuBuild.Tool` gains a `test` command group and a `--no-test` option on `build`. Nothing changes what `ci` does.

**Tech Stack:** C# / .NET 10, System.CommandLine, MSTest with NSubstitute.

**Spec:** `C:\dev\ktsu-dev\ImGuiApp\docs\superpowers\specs\2026-08-21-ci-parallel-test-matrix-design.md`

## Why this exists

The consuming workflow fans out over platform and test project, so it needs the list of test projects before any build runs. Reimplementing the detection rules in YAML would put a second copy of `IsTestProject` in a file that nobody tests. The five `*.UITests` projects in ImGuiApp are the case that proves the point: they don't end in `.Test` or `.Tests`, so a name-based filter drops all 107 of their tests and reports green.

## Global constraints

- Tabs for indentation. The repo stores LF line endings, so don't convert them.
- File-scoped namespaces, `using` directives after the namespace declaration, braces on all control flow, explicit accessibility modifiers, no `this.` qualifiers.
- Nullable reference types on, warnings as errors.
- US English in code and prose.
- `Ensure.NotNull` for parameter validation, matching the surrounding code.
- Public members carry XML documentation. The build fails without it.
- Never commit `.editorconfig`, `.gitattributes`, or `.gitignore`. Run `git checkout .editorconfig` before committing.
- Tests are MSTest, with `NSubstitute` for `IProcessRunner` and `MockBuildLogger` for `IBuildLogger`. Follow `KtsuBuild.Tests/DotNet/DotNetServiceTests.cs`.
- Run the suite with `dotnet test KtsuBuild.Tests`. Don't pass `--nologo`, which reports zero tests while everything passes.

## File structure

| File | Responsibility |
| --- | --- |
| `KtsuBuild/Abstractions/TestProjectInfo.cs` | **Create.** A project path paired with the platform it's tied to. |
| `KtsuBuild/Abstractions/IDotNetService.cs` | **Modify.** Declare `GetTestProjects` and `TestProjectAsync`. |
| `KtsuBuild/DotNet/DotNetService.cs` | **Modify.** Implement both. |
| `KtsuBuild.Tool/Commands/TestCommand.cs` | **Create.** The `test` group with `list` and `run` subcommands. |
| `KtsuBuild.Tool/Commands/BuildCommand.cs` | **Modify.** Add `--no-test`. |
| `KtsuBuild.Tool/Commands/GlobalOptions.cs` | **Modify.** Add the shared `--no-test` option. |
| `KtsuBuild.Tool/Program.cs` | **Modify.** Wire the new command and option. |
| `KtsuBuild.Tests/DotNet/DotNetServiceTests.cs` | **Modify.** Cover both service members. |
| `README.md` | **Modify.** Document the three commands. |

---

### Task 1: List test projects with their platform

**Files:**
- Create: `KtsuBuild/Abstractions/TestProjectInfo.cs`
- Modify: `KtsuBuild/Abstractions/IDotNetService.cs`
- Modify: `KtsuBuild/DotNet/DotNetService.cs`
- Test: `KtsuBuild.Tests/DotNet/DotNetServiceTests.cs`

**Interfaces:**
- Consumes: `IsTestProject(string)`, `GetProjectPlatform(string)`, and `GetProjectFiles(string)`, all existing on `DotNetService`.
- Produces: `IReadOnlyList<TestProjectInfo> GetTestProjects(string workingDirectory)` and the record `TestProjectInfo(string Project, ProjectPlatform Platform)`.

**The one design point:** `GetTestProjects` must **not** filter by the current host, unlike `GetBuildableProjects`. The caller is building a matrix that includes hosts other than the one it's running on, so it needs every test project with its platform, and decides for itself which pairs are valid.

- [ ] **Step 1: Write the failing tests**

Add to `KtsuBuild.Tests/DotNet/DotNetServiceTests.cs`, inside the existing class:

```csharp
	// GetTestProjects

	[TestMethod]
	public void GetTestProjects_ReturnsProjectsMatchingOnName()
	{
		string dir = Path.Combine(_tempDir, "Foo.Tests");
		Directory.CreateDirectory(dir);
		File.WriteAllText(Path.Combine(dir, "Foo.Tests.csproj"), "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

		IReadOnlyList<TestProjectInfo> result = _service.GetTestProjects(_tempDir);

		Assert.HasCount(1, result);
		Assert.AreEqual(ProjectPlatform.Neutral, result[0].Platform);
	}

	[TestMethod]
	public void GetTestProjects_ReturnsProjectsMatchingOnContentOnly()
	{
		// The name ends in neither .Test nor .Tests. ImGuiApp's five *.UITests projects are exactly
		// this shape, and a name-based filter silently drops every test they contain.
		string dir = Path.Combine(_tempDir, "Demo.UITests");
		Directory.CreateDirectory(dir);
		File.WriteAllText(Path.Combine(dir, "Demo.UITests.csproj"), "<Project><PropertyGroup><IsTestProject>true</IsTestProject><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

		IReadOnlyList<TestProjectInfo> result = _service.GetTestProjects(_tempDir);

		Assert.HasCount(1, result);
		Assert.Contains("Demo.UITests", result[0].Project);
	}

	[TestMethod]
	public void GetTestProjects_ExcludesNonTestProjects()
	{
		string dir = Path.Combine(_tempDir, "Library");
		Directory.CreateDirectory(dir);
		File.WriteAllText(Path.Combine(dir, "Library.csproj"), "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

		IReadOnlyList<TestProjectInfo> result = _service.GetTestProjects(_tempDir);

		Assert.IsEmpty(result);
	}

	[TestMethod]
	public void GetTestProjects_ReportsAWindowsProjectRegardlessOfHost()
	{
		// Deliberately not host-filtered: the caller builds a matrix covering hosts it is not
		// running on, so it needs every test project and decides which pairs are valid itself.
		string dir = Path.Combine(_tempDir, "Win.Tests");
		Directory.CreateDirectory(dir);
		File.WriteAllText(Path.Combine(dir, "Win.Tests.csproj"), "<Project><PropertyGroup><TargetFramework>net10.0-windows</TargetFramework></PropertyGroup></Project>");

		IReadOnlyList<TestProjectInfo> result = _service.GetTestProjects(_tempDir);

		Assert.HasCount(1, result);
		Assert.AreEqual(ProjectPlatform.Windows, result[0].Platform);
	}

	[TestMethod]
	public void GetTestProjects_ReturnsEmptyWhenThereAreNone()
	{
		IReadOnlyList<TestProjectInfo> result = _service.GetTestProjects(_tempDir);

		Assert.IsEmpty(result);
	}
```

- [ ] **Step 2: Run the tests and verify they fail**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet build KtsuBuild.Tests
```

Expected: build failure, `CS0246: The type or namespace name 'TestProjectInfo' could not be found` and `CS1061` for `GetTestProjects`.

- [ ] **Step 3: Create the record**

Create `KtsuBuild/Abstractions/TestProjectInfo.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Abstractions;

/// <summary>
/// A test project and the platform its target frameworks tie it to.
/// </summary>
/// <param name="Project">The absolute path to the project file.</param>
/// <param name="Platform">The platform the project can be restored and built on.</param>
public sealed record TestProjectInfo(string Project, ProjectPlatform Platform);
```

- [ ] **Step 4: Declare it on the interface**

Add to `KtsuBuild/Abstractions/IDotNetService.cs`, beside the other discovery members:

```csharp
	/// <summary>
	/// Gets the test projects in a directory, each with the platform it is tied to.
	/// </summary>
	/// <param name="workingDirectory">The directory to search.</param>
	/// <returns>Every test project found, whatever host it needs.</returns>
	/// <remarks>
	/// Deliberately not filtered by the current host, unlike <see cref="GetBuildableProjects"/>.
	/// The caller builds a matrix that spans hosts other than the one this runs on, so it needs the
	/// full list with each project's platform and decides which pairs are valid itself.
	/// </remarks>
	public IReadOnlyList<TestProjectInfo> GetTestProjects(string workingDirectory);
```

- [ ] **Step 5: Implement it**

Add to `KtsuBuild/DotNet/DotNetService.cs`, next to `GetBuildableProjects`:

```csharp
	/// <inheritdoc/>
	public IReadOnlyList<TestProjectInfo> GetTestProjects(string workingDirectory)
	{
		Ensure.NotNull(workingDirectory);
		return [.. GetProjectFiles(workingDirectory)
			.Where(IsTestProject)
			.Select(p => new TestProjectInfo(p, GetProjectPlatform(p)))];
	}
```

- [ ] **Step 6: Run the tests and verify they pass**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet test KtsuBuild.Tests
```

Expected: all pass, including the five new ones. Record the total.

- [ ] **Step 7: Commit**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git add KtsuBuild/Abstractions/TestProjectInfo.cs KtsuBuild/Abstractions/IDotNetService.cs KtsuBuild/DotNet/DotNetService.cs KtsuBuild.Tests/DotNet/DotNetServiceTests.cs
git commit -m "feat: list test projects with their platform [minor]"
```

---

### Task 2: Run a single test project

**Files:**
- Modify: `KtsuBuild/Abstractions/IDotNetService.cs`
- Modify: `KtsuBuild/DotNet/DotNetService.cs`
- Test: `KtsuBuild.Tests/DotNet/DotNetServiceTests.cs`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: `Task TestProjectAsync(string projectPath, string workingDirectory, string configuration, string? coverageOutputPath, CancellationToken cancellationToken)`.

Read the existing `TestAsync` first. It builds a `dotnet test` argument string with coverage flags and retries exit code 7, which is the coverage collector dropping its instrumentation pipe. Keep the same flags and the same retry, and scope the invocation to one project.

- [ ] **Step 1: Write the failing tests**

Add to `KtsuBuild.Tests/DotNet/DotNetServiceTests.cs`:

```csharp
	// TestProjectAsync

	[TestMethod]
	public async Task TestProjectAsync_PassesTheProjectPathToDotnet()
	{
		string project = Path.Combine(_tempDir, "Foo.Tests", "Foo.Tests.csproj");
		string? captured = null;
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Do<string>(a => captured = a), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _service.TestProjectAsync(project, _tempDir, "Release", "coverage").ConfigureAwait(false);

		Assert.IsNotNull(captured);
		Assert.Contains(project, captured);
		Assert.Contains("--coverage", captured);
	}

	[TestMethod]
	public async Task TestProjectAsync_RetriesTheCoverageCollectorFlake()
	{
		// Exit code 7 is the coverage collector dropping its instrumentation pipe during teardown,
		// not a test failure. A genuine failure is exit code 2 and must not be retried.
		string project = Path.Combine(_tempDir, "Foo.Tests", "Foo.Tests.csproj");
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(7, 0);

		await _service.TestProjectAsync(project, _tempDir, "Release", "coverage").ConfigureAwait(false);

		await _processRunner.Received(2).RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task TestProjectAsync_ThrowsOnAGenuineTestFailure()
	{
		string project = Path.Combine(_tempDir, "Foo.Tests", "Foo.Tests.csproj");
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(2);

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.TestProjectAsync(project, _tempDir, "Release", "coverage")).ConfigureAwait(false);
	}
```

If `TestAsync` throws a different exception type on failure, match it here rather than changing the production behavior. Read it before writing this test.

- [ ] **Step 2: Run the tests and verify they fail**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet build KtsuBuild.Tests
```

Expected: `CS1061`, `TestProjectAsync` is not defined.

- [ ] **Step 3: Declare it on the interface**

```csharp
	/// <summary>
	/// Runs a single test project with coverage.
	/// </summary>
	/// <param name="projectPath">The project file to test.</param>
	/// <param name="workingDirectory">The directory to run from.</param>
	/// <param name="configuration">The build configuration.</param>
	/// <param name="coverageOutputPath">Where coverage output is written. Defaults to <c>coverage</c> when null.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that completes when the run succeeds.</returns>
	/// <remarks>
	/// Scoping a run to one project also removes the condition behind the coverage collector's
	/// exit-code-7 flake, which only appears when several test assemblies run in one invocation.
	/// The retry is kept anyway, since the caller decides how many projects an invocation covers.
	/// </remarks>
	public Task TestProjectAsync(string projectPath, string workingDirectory, string configuration = "Release", string? coverageOutputPath = null, CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Extract the shared body**

Two copies of the argument string and the retry loop is the duplication this change exists to remove. Keep `TestAsync`'s discovery and early return where they are, and replace everything from `string resultsPath = ...` to the end of the method with a call to a new private method.

Copy the argument string and the retry loop verbatim from the current `TestAsync` rather than retyping them, so the quoting and the flags stay byte-identical. The only edit to that block is the `scope` insertion:

```csharp
		logger.WriteInfo($"Found {testProjects.Count} test project(s)");

		await RunTestsAsync(target: string.Empty, workingDirectory, configuration, coverageOutputPath, cancellationToken).ConfigureAwait(false);
	}

	// Shared by TestAsync, which tests everything the host can build, and TestProjectAsync, which
	// tests one project. `target` is the project path, or empty to let `dotnet test` discover.
	private async Task RunTestsAsync(string target, string workingDirectory, string configuration, string? coverageOutputPath, CancellationToken cancellationToken)
	{
		string resultsPath = coverageOutputPath ?? "coverage";
		string testResultsPath = Path.Combine(resultsPath, "TestResults");
		Directory.CreateDirectory(testResultsPath);

		string scope = string.IsNullOrEmpty(target) ? string.Empty : $"\"{target}\" ";
		string args = $"test {scope}--configuration {configuration} --coverage --coverage-output-format xml " +
			$"--coverage-output \"coverage.xml\" --results-directory \"{testResultsPath}\" " +
			$"--report-trx --report-trx-filename TestResults.trx";

		// ... the coverageFlakeExitCode retry loop, the exit-code check, and CopyCoverageFile,
		// all moved unchanged from TestAsync.
	}

	/// <inheritdoc/>
	public async Task TestProjectAsync(string projectPath, string workingDirectory, string configuration = DefaultConfiguration, string? coverageOutputPath = null, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(projectPath);
		Ensure.NotNull(workingDirectory);
		logger.WriteStepHeader($"Running Tests with Coverage: {Path.GetFileNameWithoutExtension(projectPath)}");

		await RunTestsAsync(projectPath, workingDirectory, configuration, coverageOutputPath, cancellationToken).ConfigureAwait(false);
	}
```

`TestAsync` keeps its own `WriteStepHeader`, its `GetBuildableProjects` discovery, and its "No test projects found" early return. Only the execution half moves.

- [ ] **Step 5: Run the whole suite**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet test KtsuBuild.Tests
```

Expected: every test passes, including the pre-existing `TestAsync` tests, which must not have changed behavior. If any existing `TestAsync` test fails, stop and report rather than adjusting it.

- [ ] **Step 6: Commit**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git add KtsuBuild/Abstractions/IDotNetService.cs KtsuBuild/DotNet/DotNetService.cs KtsuBuild.Tests/DotNet/DotNetServiceTests.cs
git commit -m "feat: run a single test project with coverage [minor]"
```

---

### Task 3: The test command group

**Files:**
- Create: `KtsuBuild.Tool/Commands/TestCommand.cs`
- Modify: `KtsuBuild.Tool/Program.cs`

**Interfaces:**
- Consumes: `GetTestProjects` from Task 1 and `TestProjectAsync` from Task 2.
- Produces: `ktsubuild test list --json` and `ktsubuild test run --project <path>`.

Model the group on `VersionCommand`, which nests private subcommand classes and is wired in `Program.AddVersionCommand`. Read both before starting.

`list` writes JSON to stdout so a workflow can read it. Paths are emitted relative to the workspace, because absolute runner paths are noise in a matrix label:

```json
[{"project":"tests/Foo.Tests/Foo.Tests.csproj","platform":"neutral"}]
```

Lowercase the platform. `System.Text.Json` is already used in `KtsuBuild/Publishing/GitHubService.cs`.

- [ ] **Step 1: Create the command group**

Create `KtsuBuild.Tool/Commands/TestCommand.cs` with a `TestCommand : Command` named `test`, description `Test project discovery and execution`, holding two private nested subcommands:

- `ListCommand`, named `list`, description `List test projects as JSON`, with `GlobalOptions.Workspace` and `GlobalOptions.Verbose`.
- `RunCommand`, named `run`, description `Run one test project with coverage`, with `GlobalOptions.Workspace`, `GlobalOptions.Configuration`, `GlobalOptions.Verbose`, and a required `--project` option of type `string`.

Follow `VersionCommand`'s structure exactly, including the `CA1010` pragma pairs around each class declaration.

Add a required `--project` option as a `public static Option<string> Project` on `TestCommand`, matching the constructor shape `GlobalOptions.cs` already uses for its options.

Add two static handler factories, following `BuildCommand.CreateHandler`. The list handler:

```csharp
	public static Func<string, bool, CancellationToken, Task<int>> CreateListHandler(
		IProcessRunner processRunner,
		IBuildLogger logger)
	{
		return (workspace, verbose, cancellationToken) =>
		{
			logger.VerboseEnabled = verbose;
			DotNetService dotNetService = new(processRunner, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				IReadOnlyList<TestProjectInfo> projects = dotNetService.GetTestProjects(workspace);
				var payload = projects
					.Select(p => new
					{
						project = Path.GetRelativePath(workspace, p.Project).Replace(Path.DirectorySeparatorChar, '/'),
						platform = p.Platform.ToString().ToLowerInvariant(),
					})
					.OrderBy(p => p.project, StringComparer.Ordinal)
					.ToList();

				Console.WriteLine(JsonSerializer.Serialize(payload));
				return Task.FromResult(0);
			}
			catch (Exception ex)
			{
				logger.WriteError($"Listing test projects failed: {ex.Message}");
				return Task.FromResult(1);
			}
#pragma warning restore CA1031
		};
	}
```

`list` writes with `Console.WriteLine` rather than through the logger, because a workflow parses stdout and logger output would corrupt it. Paths are relative with forward slashes so a matrix label reads the same on every runner.

The run handler resolves the project against the workspace when it isn't already absolute, then calls `TestProjectAsync`:

```csharp
	public static Func<string, string, string, bool, CancellationToken, Task<int>> CreateRunHandler(
		IProcessRunner processRunner,
		IBuildLogger logger)
	{
		return async (workspace, configuration, project, verbose, cancellationToken) =>
		{
			logger.VerboseEnabled = verbose;
			BuildEnvironment.Initialize();
			DotNetService dotNetService = new(processRunner, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				string projectPath = Path.IsPathRooted(project) ? project : Path.Combine(workspace, project);
				await dotNetService.TestProjectAsync(projectPath, workspace, configuration, "coverage", cancellationToken).ConfigureAwait(false);
				logger.WriteSuccess("Test run completed successfully!");
				return 0;
			}
			catch (Exception ex)
			{
				logger.WriteError($"Test run failed: {ex.Message}");
				return 1;
			}
#pragma warning restore CA1031
		};
	}
```

Check `BuildEnvironment.Initialize()` and the `Option<string>` constructor against the existing files, and follow those if they differ.

- [ ] **Step 2: Wire it in Program.cs**

Add an `AddTestCommand(RootCommand, IProcessRunner, IBuildLogger)` method modelled on `AddVersionCommand`, resolving each subcommand with `testCommand.Subcommands.First(c => c.Name == "list")` and `"run"`, calling `SetAction` on each, and adding the group with `rootCommand.Subcommands.Add(testCommand)`. Call it from wherever the other `Add*Command` methods are called.

- [ ] **Step 3: Build and verify the commands appear**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet build
dotnet run --project KtsuBuild.Tool -- test --help
dotnet run --project KtsuBuild.Tool -- test list --help
```

Expected: `test` lists `list` and `run`, and `list` shows the workspace option.

- [ ] **Step 4: Verify list against a real repository**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet run --project KtsuBuild.Tool -- test list --workspace /c/dev/ktsu-dev/ImGuiApp
```

Expected: valid JSON naming ten test projects, including all five `*.UITests` ones, every platform `neutral` except `ImGui.App.iOS.SmokeTest`, which is `ios`. That repository is the reason this command exists, so confirm the count rather than eyeballing the shape.

- [ ] **Step 5: Verify run against a real project**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet run --project KtsuBuild.Tool -- test run --workspace /c/dev/ktsu-dev/ImGuiApp --project tests/ImGui.Widgets.Tests/ImGui.Widgets.Tests.csproj
```

Expected: the run reports 256 tests, matching what that project produces on its own. A run that reports zero and exits zero is the specific failure this design is most exposed to, so check the number.

- [ ] **Step 6: Commit**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git add KtsuBuild.Tool/Commands/TestCommand.cs KtsuBuild.Tool/Program.cs
git commit -m "feat: add the test list and test run commands [minor]"
```

---

### Task 4: Build without testing

**Files:**
- Modify: `KtsuBuild.Tool/Commands/GlobalOptions.cs`
- Modify: `KtsuBuild.Tool/Commands/BuildCommand.cs`
- Modify: `KtsuBuild.Tool/Program.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `ktsubuild build --no-test`.

The release job needs a compilation between `sonarscanner begin` and `end` but runs no tests. Without this it would test everything again, which is the serial run this whole change removes.

- [ ] **Step 1: Add the option**

In `KtsuBuild.Tool/Commands/GlobalOptions.cs`, following the existing option declarations:

```csharp
	/// <summary>Skips the test step, for callers that run tests separately.</summary>
	public static Option<bool> NoTest { get; } = new("--no-test")
	{
		Description = "Skip the test step",
	};
```

Match the surrounding style. If the other options use a different constructor shape or add aliases, follow that instead.

- [ ] **Step 2: Accept it in BuildCommand**

Add `Options.Add(GlobalOptions.NoTest);` to the constructor, extend `CreateHandler`'s delegate with a `bool noTest` parameter, and guard the test call:

```csharp
				if (!noTest)
				{
					await dotNetService.TestAsync(workspace, configuration, "coverage", cancellationToken).ConfigureAwait(false);
				}
```

The handler signature becomes `Func<string, string, bool, bool, CancellationToken, Task<int>>`.

- [ ] **Step 3: Wire it in Program.cs**

In `AddBuildCommand`, read the value and pass it through:

```csharp
			bool noTest = parseResult.GetValue(GlobalOptions.NoTest);
			return await handler(workspace, configuration, verbose, noTest, ct).ConfigureAwait(false);
```

- [ ] **Step 4: Verify both paths**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet build
dotnet run --project KtsuBuild.Tool -- build --help
```

Expected: `--no-test` appears. Then confirm the flag actually skips tests by running it against a repository with a fast suite and checking the output has no test step:

```bash
dotnet run --project KtsuBuild.Tool -- build --workspace /c/dev/ktsu-dev/KtsuBuild --no-test
```

Expected: restore and build run, and the "Running Tests with Coverage" step header never appears. Run it again without the flag and confirm the header does appear, so the check discriminates.

- [ ] **Step 5: Run the suite**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
dotnet test KtsuBuild.Tests
```

Expected: every test passes.

- [ ] **Step 6: Commit**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git add KtsuBuild.Tool/Commands/GlobalOptions.cs KtsuBuild.Tool/Commands/BuildCommand.cs KtsuBuild.Tool/Program.cs
git commit -m "feat: add --no-test to the build command [minor]"
```

---

### Task 5: Document the commands

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: all three commands from Tasks 3 and 4.
- Produces: nothing consumed later.

- [ ] **Step 1: Read the existing command documentation**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
grep -n "ktsubuild " README.md | head -20
```

Match whatever form the existing commands use, including heading level and whether examples show output.

- [ ] **Step 2: Document the three commands**

Add `test list`, `test run`, and `build --no-test` alongside the existing entries. For `test list`, show the JSON shape, because a caller parses it and the field names are part of the contract:

```json
[{"project":"tests/Foo.Tests/Foo.Tests.csproj","platform":"neutral"}]
```

State that `test list` reports every test project regardless of the current host, and that the caller decides which platform pairs are valid. That is the property a CI matrix depends on, and it is not obvious from the command name.

- [ ] **Step 3: Commit**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git checkout .editorconfig
git add README.md
git commit -m "docs: document the test commands and --no-test [patch]"
```

---

### Task 6: Release to nuget.org

**Files:** none.

The consuming workflow installs this tool with `dotnet tool install ktsu.KtsuBuild.Tool`, so nothing downstream can use these commands until a release lands. That makes this task a gate rather than a formality.

- [ ] **Step 1: Confirm the branch is clean and complete**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git status --short
git log --oneline main..HEAD
dotnet test KtsuBuild.Tests
```

Expected: five commits, a clean tree apart from `.editorconfig`, and a green suite.

- [ ] **Step 2: Open the pull request**

```bash
cd /c/dev/ktsu-dev/KtsuBuild
git push -u origin <branch>
gh pr create --base main --head <branch> \
  --title "Add scoped test commands for CI fan-out" \
  --body "Adds test list, test run, and build --no-test so a CI workflow can enumerate test projects, run one at a time, and build without testing. Each is a thin wrapper over existing service methods. ktsubuild ci is unchanged."
```

- [ ] **Step 3: Stop and report**

Merging and releasing are the repository owner's calls. The workflow restructure in ImGuiApp waits on the released tool version and gets its own plan, written once the release exists.
