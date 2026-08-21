// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tool.Commands;

using System.CommandLine;
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
	/// Initializes a new instance of the <see cref="TestCommand"/> class.
	/// </summary>
	public TestCommand() : base("test", "Test project discovery and execution")
	{
		Subcommands.Add(new ListCommand());
		Subcommands.Add(new RunCommand());
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
		}
	}
}
