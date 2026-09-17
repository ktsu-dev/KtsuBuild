// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Commands;

using System.Runtime.InteropServices;
using System.Text.Json;
using KtsuBuild.Abstractions;
using KtsuBuild.Tests.Helpers;
using KtsuBuild.Tests.Mocks;
using KtsuBuild.Tool.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

/// <summary>
/// Drives the <c>test</c> command's handler factories directly, against a workspace on disk and a
/// substituted process runner.
/// </summary>
/// <remarks>
/// The service layer these handlers call is tested with its own mocks, which cannot reach the
/// orchestration the handlers do themselves: deciding what this host can build, turning those
/// decisions into a solution filter, reporting each skip, and cleaning the filter up afterwards. A
/// project dropped from a run looks exactly like a project that passed, so that logic is asserted
/// here rather than left to the end-to-end run to reveal.
/// </remarks>
[TestClass]
public class TestCommandTests
{
	private IProcessRunner _processRunner = null!;
	private RecordingBuildLogger _logger = null!;
	private string _workspace = null!;
	private readonly List<string> _dotnetArguments = [];
	private readonly List<IReadOnlyList<string>?> _filteredProjects = [];
	private int _testExitCode;

	[TestInitialize]
	public void Setup()
	{
		_dotnetArguments.Clear();
		_filteredProjects.Clear();
		_testExitCode = 0;
		_logger = new RecordingBuildLogger();
		_workspace = TestHelpers.CreateTempDir("TestCommand");
		_processRunner = Substitute.For<IProcessRunner>();

		// The filter is written before the run and deleted after it, so it only exists while the
		// invocation is in flight. Reading it here is the only way to see what the run was actually
		// scoped to.
		_processRunner.RunWithCallbackAsync(
				Arg.Any<string>(),
				Arg.Any<string>(),
				Arg.Any<string?>(),
				Arg.Any<Action<string>?>(),
				Arg.Any<Action<string>?>(),
				Arg.Any<CancellationToken>())
			.Returns(call =>
			{
				string arguments = (string)call[1];
				_dotnetArguments.Add(arguments);
				_filteredProjects.Add(ReadSolutionFilter(arguments));
				return Task.FromResult(_testExitCode);
			});
	}

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_workspace))
		{
			Directory.Delete(_workspace, recursive: true);
		}
	}

	private string FilterPath => Path.Combine(_workspace, "ktsubuild.filtered.slnf");

	/// <summary>
	/// A target framework this host cannot build, so the skip path is exercised wherever the suite
	/// runs. Only macOS builds iOS, and only Windows builds Windows.
	/// </summary>
	private static string UnbuildableTargetFramework =>
		RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "net10.0-ios" : "net10.0-windows";

	private static string UnbuildablePlatformName =>
		RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "iOS" : "Windows";

	private string WriteProject(string name, string targetFramework = "net10.0")
	{
		string directory = Path.Combine(_workspace, name);
		Directory.CreateDirectory(directory);
		string path = Path.Combine(directory, $"{name}.csproj");
		File.WriteAllText(path, $"""
			<Project Sdk="Microsoft.NET.Sdk">
			  <PropertyGroup>
			    <TargetFramework>{targetFramework}</TargetFramework>
			  </PropertyGroup>
			</Project>
			""");
		return path;
	}

	private void WriteSolution(params string[] projectNames)
	{
		string projects = string.Join(
			"\n  ",
			projectNames.Select(n => $"""<Project Path="{n}/{n}.csproj" />"""));
		File.WriteAllText(Path.Combine(_workspace, "Sample.slnx"), $"<Solution>\n  {projects}\n</Solution>\n");
	}

	private Task<int> RunAll(params string[] exclude) =>
		TestCommand.CreateAllHandler(_processRunner, _logger)(_workspace, "Release", exclude, false, CancellationToken.None);

	/// <summary>
	/// Reads the solution filter a <c>dotnet test</c> invocation was scoped to, as project paths.
	/// </summary>
	/// <param name="arguments">The arguments the invocation was given.</param>
	/// <returns>The projects the filter kept, or null when the run carried no filter.</returns>
	private static IReadOnlyList<string>? ReadSolutionFilter(string arguments)
	{
		const string marker = "--solution \"";
		int start = arguments.IndexOf(marker, StringComparison.Ordinal);

		if (start < 0)
		{
			return null;
		}

		start += marker.Length;
		string path = arguments[start..arguments.IndexOf('"', start)];

		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

		return [.. document.RootElement
			.GetProperty("solution")
			.GetProperty("projects")
			.EnumerateArray()
			.Select(p => p.GetString()!.Replace('\\', '/'))];
	}

	// CreateAllHandler

	[TestMethod]
	public async Task CreateAllHandler_WorkspaceWithNoTestProjects_SkipsTheRun()
	{
		WriteProject("Library");
		WriteSolution("Library");

		Assert.AreEqual(0, await RunAll().ConfigureAwait(false));
		Assert.IsEmpty(_dotnetArguments);
		Assert.IsTrue(RecordingBuildLogger.Any(_logger.Infos, "No test projects found in workspace."));
	}

	[TestMethod]
	public async Task CreateAllHandler_WithNoExclusions_RunsTheWholeWorkspaceUnfiltered()
	{
		WriteProject("Alpha.Test");
		WriteSolution("Alpha.Test");

		Assert.AreEqual(0, await RunAll().ConfigureAwait(false));
		Assert.HasCount(1, _dotnetArguments);
		Assert.IsNull(_filteredProjects[0], "Nothing was excluded, so the run should not have been narrowed by a filter.");
		Assert.Contains("All 1 test project(s) passed!", _logger.Successes);
	}

	[TestMethod]
	public async Task CreateAllHandler_ExcludedProject_IsLeftOutOfTheSolutionFilter()
	{
		WriteProject("Alpha.Test");
		WriteProject("Beta.Test");
		WriteSolution("Alpha.Test", "Beta.Test");

		Assert.AreEqual(0, await RunAll("Beta.Test/**").ConfigureAwait(false));

		IReadOnlyList<string> kept = _filteredProjects[0]!;
		Assert.Contains("Alpha.Test/Alpha.Test.csproj", kept);
		Assert.DoesNotContain("Beta.Test/Beta.Test.csproj", kept);
		Assert.IsTrue(RecordingBuildLogger.Any(_logger.Infos, "Excluding Beta.Test/Beta.Test.csproj from the test run."));
	}

	[TestMethod]
	public async Task CreateAllHandler_ExcludedProject_IsCountedInTheSuccessMessage()
	{
		WriteProject("Alpha.Test");
		WriteProject("Beta.Test");
		WriteSolution("Alpha.Test", "Beta.Test");

		await RunAll("Beta.Test/**").ConfigureAwait(false);

		Assert.Contains("All 1 test project(s) passed! (1 excluded by --exclude.)", _logger.Successes);
	}

	[TestMethod]
	public async Task CreateAllHandler_ExcludePatternMatchingNothing_ReportsItWithoutWarning()
	{
		WriteProject("Alpha.Test");
		WriteSolution("Alpha.Test");

		Assert.AreEqual(0, await RunAll("Nowhere.Test/**").ConfigureAwait(false));

		// One workflow file passes the same patterns to every repository, so a pattern that matches
		// nothing is the ordinary case and must not be a warning.
		Assert.IsTrue(RecordingBuildLogger.Any(_logger.Infos, "--exclude matched no projects in Sample.slnx"));
		Assert.IsFalse(RecordingBuildLogger.Any(_logger.Warnings, "--exclude"));
		Assert.Contains("All 1 test project(s) passed!", _logger.Successes);
	}

	[TestMethod]
	public async Task CreateAllHandler_EveryTestProjectExcluded_SkipsTheRun()
	{
		WriteProject("Alpha.Test");
		WriteSolution("Alpha.Test");

		Assert.AreEqual(0, await RunAll("**/*.Test.csproj").ConfigureAwait(false));
		Assert.IsEmpty(_dotnetArguments);
		Assert.IsTrue(RecordingBuildLogger.Any(_logger.Infos, "Every test project was excluded. Nothing to run."));
	}

	[TestMethod]
	public async Task CreateAllHandler_WorkspaceWithoutASolution_WarnsAndRunsUnfiltered()
	{
		WriteProject("Alpha.Test");

		Assert.AreEqual(0, await RunAll("Alpha.Test/**").ConfigureAwait(false));

		Assert.IsTrue(RecordingBuildLogger.Any(_logger.Warnings, "The workspace has no solution file"));
		Assert.HasCount(1, _dotnetArguments);
		Assert.IsNull(_filteredProjects[0]);
	}

	[TestMethod]
	public async Task CreateAllHandler_ProjectThisHostCannotBuild_IsNamedWithItsReason()
	{
		WriteProject("Alpha.Test");
		WriteProject("Platform.Test", UnbuildableTargetFramework);
		WriteSolution("Alpha.Test", "Platform.Test");

		Assert.AreEqual(0, await RunAll().ConfigureAwait(false));

		Assert.IsTrue(RecordingBuildLogger.Any(
			_logger.Warnings,
			$"Skipping Platform.Test/Platform.Test.csproj: platform is {UnbuildablePlatformName}, which this host cannot build."));
		Assert.IsTrue(RecordingBuildLogger.Any(_logger.Infos, "Running 1 test project(s), skipping 1."));
	}

	[TestMethod]
	public async Task CreateAllHandler_ProjectThisHostCannotBuild_IsAlsoKeptOutOfTheRun()
	{
		WriteProject("Alpha.Test");
		WriteProject("Platform.Test", UnbuildableTargetFramework);
		WriteSolution("Alpha.Test", "Platform.Test");

		await RunAll().ConfigureAwait(false);

		// Naming the skip is not enough. Without the filter the workspace-wide invocation builds and
		// tests the project anyway, turning a reasoned skip into a build failure.
		IReadOnlyList<string> kept = _filteredProjects[0]!;
		Assert.Contains("Alpha.Test/Alpha.Test.csproj", kept);
		Assert.DoesNotContain("Platform.Test/Platform.Test.csproj", kept);
	}

	[TestMethod]
	public async Task CreateAllHandler_PlatformSkip_IsNotRepeatedAsAnExclusion()
	{
		WriteProject("Alpha.Test");
		WriteProject("Platform.Test", UnbuildableTargetFramework);
		WriteSolution("Alpha.Test", "Platform.Test");

		await RunAll().ConfigureAwait(false);

		// The skip was already reported as a warning, with its reason. Reporting it again as a plain
		// exclusion would describe one decision twice, in two different terms.
		Assert.IsFalse(RecordingBuildLogger.Any(_logger.Infos, "Excluding Platform.Test/Platform.Test.csproj"));
	}

	[TestMethod]
	public async Task CreateAllHandler_NothingTheHostCanBuild_SkipsTheRun()
	{
		WriteProject("Platform.Test", UnbuildableTargetFramework);
		WriteSolution("Platform.Test");

		Assert.AreEqual(0, await RunAll().ConfigureAwait(false));
		Assert.IsEmpty(_dotnetArguments);
		Assert.IsTrue(RecordingBuildLogger.Any(_logger.Infos, "No test projects can build on this host. Nothing to run."));
	}

	[TestMethod]
	public async Task CreateAllHandler_SuccessfulRun_LeavesNoSolutionFilterBehind()
	{
		WriteProject("Alpha.Test");
		WriteProject("Beta.Test");
		WriteSolution("Alpha.Test", "Beta.Test");

		await RunAll("Beta.Test/**").ConfigureAwait(false);

		Assert.IsNotNull(_filteredProjects[0], "The run should have carried a filter while it was in flight.");
		Assert.IsFalse(File.Exists(FilterPath), "The generated filter belongs to the run, not to the workspace.");
	}

	[TestMethod]
	public async Task CreateAllHandler_FailedRun_ReturnsOneAndStillDeletesTheSolutionFilter()
	{
		WriteProject("Alpha.Test");
		WriteProject("Beta.Test");
		WriteSolution("Alpha.Test", "Beta.Test");
		_testExitCode = 2;

		Assert.AreEqual(1, await RunAll("Beta.Test/**").ConfigureAwait(false));

		Assert.IsFalse(File.Exists(FilterPath));
		Assert.IsTrue(RecordingBuildLogger.Any(_logger.Errors, "Test run failed:"));
		Assert.IsEmpty(_logger.Successes);
	}

	[TestMethod]
	public async Task CreateAllHandler_Verbose_TurnsOnVerboseLogging()
	{
		WriteProject("Alpha.Test");
		WriteSolution("Alpha.Test");

		await TestCommand.CreateAllHandler(_processRunner, _logger)(_workspace, "Release", [], true, CancellationToken.None)
			.ConfigureAwait(false);

		Assert.IsTrue(_logger.VerboseEnabled);
	}

	// CreateRunHandler

	[TestMethod]
	public async Task CreateRunHandler_SuccessfulRun_ReturnsZero()
	{
		string project = WriteProject("Alpha.Test");

		int exitCode = await TestCommand.CreateRunHandler(_processRunner, _logger)(
			_workspace, "Release", Path.GetRelativePath(_workspace, project), false, false, CancellationToken.None)
			.ConfigureAwait(false);

		Assert.AreEqual(0, exitCode);
		Assert.Contains("Test run completed successfully!", _logger.Successes);
		Assert.HasCount(1, _dotnetArguments);
	}

	[TestMethod]
	public async Task CreateRunHandler_RelativeProject_IsResolvedAgainstTheWorkspace()
	{
		string project = WriteProject("Alpha.Test");

		await TestCommand.CreateRunHandler(_processRunner, _logger)(
			_workspace, "Release", Path.GetRelativePath(_workspace, project), false, false, CancellationToken.None)
			.ConfigureAwait(false);

		// A relative path left unresolved would be read against the process directory, where it does
		// not exist.
		Assert.Contains(project, _dotnetArguments[0]);
	}

	[TestMethod]
	public async Task CreateRunHandler_FailedRun_ReturnsOneAndReportsIt()
	{
		string project = WriteProject("Alpha.Test");
		_testExitCode = 2;

		int exitCode = await TestCommand.CreateRunHandler(_processRunner, _logger)(
			_workspace, "Release", project, false, false, CancellationToken.None)
			.ConfigureAwait(false);

		Assert.AreEqual(1, exitCode);
		Assert.IsTrue(RecordingBuildLogger.Any(_logger.Errors, "Test run failed:"));
	}

	[TestMethod]
	public async Task CreateRunHandler_NoBuild_PassesTheFlagThrough()
	{
		string project = WriteProject("Alpha.Test");

		await TestCommand.CreateRunHandler(_processRunner, _logger)(
			_workspace, "Release", project, true, false, CancellationToken.None)
			.ConfigureAwait(false);

		Assert.Contains("--no-build", _dotnetArguments[0]);
	}

	// CreateListHandler

	[TestMethod]
	public async Task CreateListHandler_ListableWorkspace_ReturnsZero()
	{
		WriteProject("Alpha.Test");

		int exitCode = await TestCommand.CreateListHandler(_processRunner, _logger)(_workspace, false, CancellationToken.None)
			.ConfigureAwait(false);

		Assert.AreEqual(0, exitCode);
		Assert.IsEmpty(_logger.Errors);
	}

	[TestMethod]
	public async Task CreateListHandler_MissingWorkspace_ReturnsOneAndReportsIt()
	{
		string missing = Path.Combine(_workspace, "does-not-exist");

		int exitCode = await TestCommand.CreateListHandler(_processRunner, _logger)(missing, false, CancellationToken.None)
			.ConfigureAwait(false);

		Assert.AreEqual(1, exitCode);
		Assert.IsTrue(RecordingBuildLogger.Any(_logger.Errors, "Listing test projects failed:"));
	}

	[TestMethod]
	public async Task CreateListHandler_Verbose_TurnsOnVerboseLogging()
	{
		WriteProject("Alpha.Test");

		await TestCommand.CreateListHandler(_processRunner, _logger)(_workspace, true, CancellationToken.None)
			.ConfigureAwait(false);

		Assert.IsTrue(_logger.VerboseEnabled);
	}
}
