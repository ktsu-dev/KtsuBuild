// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tool.Commands;

using System.CommandLine;
using System.Runtime.InteropServices;
using System.Text.Json;
using KtsuBuild.Abstractions;
using KtsuBuild.DotNet;
using KtsuBuild.Utilities;

/// <summary>
/// Test command for discovering and running test projects individually.
/// </summary>
#pragma warning disable CA1010 // System.CommandLine.Command implements IEnumerable for collection initializer support
public class TestCommand : Command
#pragma warning restore CA1010
{
	/// <summary>
	/// Gets the required path to the test project to run.
	/// </summary>
	public static Option<string> Project { get; } = new("--project")
	{
		Description = "Path to the test project to run, relative to the workspace or absolute",
		Required = true,
	};

	/// <summary>
	/// Gets the option that skips building before the test run.
	/// </summary>
	public static Option<bool> NoBuild { get; } = new("--no-build")
	{
		Description = "Skip building before running the tests, for a caller that has already built this project",
		DefaultValueFactory = _ => false,
	};

	/// <summary>
	/// Initializes a new instance of the <see cref="TestCommand"/> class.
	/// </summary>
	public TestCommand() : base("test", "Test project discovery and execution")
	{
		Subcommands.Add(new ListCommand());
		Subcommands.Add(new RunCommand());
		Subcommands.Add(new AllCommand());
	}

	/// <summary>
	/// Creates the handler for the <c>list</c> subcommand.
	/// </summary>
	/// <param name="processRunner">The process runner.</param>
	/// <param name="logger">The build logger.</param>
	/// <returns>The command handler action.</returns>
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

	/// <summary>
	/// Creates the handler for the <c>run</c> subcommand.
	/// </summary>
	/// <param name="processRunner">The process runner.</param>
	/// <param name="logger">The build logger.</param>
	/// <returns>The command handler action.</returns>
	public static Func<string, string, string, bool, bool, CancellationToken, Task<int>> CreateRunHandler(
		IProcessRunner processRunner,
		IBuildLogger logger)
	{
		return async (workspace, configuration, project, noBuild, verbose, cancellationToken) =>
		{
			logger.VerboseEnabled = verbose;
			BuildEnvironment.Initialize();
			DotNetService dotNetService = new(processRunner, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				string projectPath = Path.IsPathRooted(project) ? project : Path.Combine(workspace, project);
				await dotNetService.TestProjectAsync(projectPath, workspace, configuration, "coverage", noBuild, cancellationToken: cancellationToken).ConfigureAwait(false);
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

	/// <summary>
	/// Creates the handler for the <c>all</c> subcommand.
	/// </summary>
	/// <param name="processRunner">The process runner.</param>
	/// <param name="logger">The build logger.</param>
	/// <returns>The command handler action.</returns>
	public static Func<string, string, bool, CancellationToken, Task<int>> CreateAllHandler(
		IProcessRunner processRunner,
		IBuildLogger logger)
	{
		return async (workspace, configuration, verbose, cancellationToken) =>
		{
			logger.VerboseEnabled = verbose;
			BuildEnvironment.Initialize();
			DotNetService dotNetService = new(processRunner, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions, including per-project failures below
			try
			{
				IReadOnlyList<TestProjectInfo> allProjects = dotNetService.GetTestProjects(workspace);
				if (allProjects.Count == 0)
				{
					logger.WriteInfo("No test projects found in workspace.");
					return 0;
				}

				bool hostIsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
				bool hostIsMacOs = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

				// GetTestProjects deliberately does not filter by host platform, so the command layer
				// decides here. A neutral project runs anywhere; a Windows project only on Windows; an
				// iOS project only on macOS. A project silently dropped from the run would look identical
				// to a passing run, so every skip is named and reasoned below.
				List<TestProjectInfo> toRun = [];
				List<TestProjectInfo> skipped = [];
				foreach (TestProjectInfo project in allProjects)
				{
					if (DotNetService.CanPlatformBuildOnHost(project.Platform, hostIsWindows, hostIsMacOs))
					{
						toRun.Add(project);
					}
					else
					{
						skipped.Add(project);
					}
				}

				foreach (TestProjectInfo project in skipped)
				{
					string relativePath = Path.GetRelativePath(workspace, project.Project).Replace(Path.DirectorySeparatorChar, '/');
					logger.WriteWarning($"Skipping {relativePath}: platform is {project.Platform}, which this host cannot build.");
				}

				logger.WriteInfo($"Running {toRun.Count} test project(s), skipping {skipped.Count}.");

				if (toRun.Count == 0)
				{
					logger.WriteInfo("No test projects can build on this host. Nothing to run.");
					return 0;
				}

				List<string> failedProjects = [];
				foreach (TestProjectInfo project in toRun)
				{
					string relativePath = Path.GetRelativePath(workspace, project.Project).Replace(Path.DirectorySeparatorChar, '/');
					logger.WriteStepHeader($"Testing {relativePath}");
					try
					{
						await dotNetService.TestProjectAsync(project.Project, workspace, configuration, "coverage", noBuild: false, hostRuntimeOnly: true, cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						// A failing project must not stop the ones after it: collect the failure and continue,
						// so one run reports every project that failed rather than only the first.
						logger.WriteError($"{relativePath} failed: {ex.Message}");
						failedProjects.Add(relativePath);
					}
				}

				if (failedProjects.Count > 0)
				{
					logger.WriteError($"{failedProjects.Count} of {toRun.Count} test project(s) failed: {string.Join(", ", failedProjects)}");
					return 1;
				}

				logger.WriteSuccess($"All {toRun.Count} test project(s) passed!");
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

#pragma warning disable CA1010
	private sealed class ListCommand : Command
#pragma warning restore CA1010
	{
		public ListCommand() : base("list", "List test projects as JSON")
		{
			Options.Add(GlobalOptions.Workspace);
			Options.Add(GlobalOptions.Verbose);
		}
	}

#pragma warning disable CA1010
	private sealed class RunCommand : Command
#pragma warning restore CA1010
	{
		public RunCommand() : base("run", "Run one test project with coverage")
		{
			Options.Add(GlobalOptions.Workspace);
			Options.Add(GlobalOptions.Configuration);
			Options.Add(GlobalOptions.Verbose);
			Options.Add(Project);
			Options.Add(NoBuild);
		}
	}

#pragma warning disable CA1010
	private sealed class AllCommand : Command
#pragma warning restore CA1010
	{
		public AllCommand() : base("all", "Restore, build, and test every project the host can build, pinned to the host runtime")
		{
			Options.Add(GlobalOptions.Workspace);
			Options.Add(GlobalOptions.Configuration);
			Options.Add(GlobalOptions.Verbose);
		}
	}
}
