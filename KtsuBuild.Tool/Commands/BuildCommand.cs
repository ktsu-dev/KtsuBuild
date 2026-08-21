// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tool.Commands;

using System.CommandLine;
using KtsuBuild.Abstractions;
using KtsuBuild.DotNet;
using KtsuBuild.Utilities;

/// <summary>
/// Build command that runs restore, build, and test.
/// </summary>
#pragma warning disable CA1010 // System.CommandLine.Command implements IEnumerable for collection initializer support
public class BuildCommand : Command
#pragma warning restore CA1010
{
	/// <summary>
	/// Initializes a new instance of the <see cref="BuildCommand"/> class.
	/// </summary>
	public BuildCommand() : base("build", "Build workflow: restore, build, test")
	{
		Options.Add(GlobalOptions.Workspace);
		Options.Add(GlobalOptions.Configuration);
		Options.Add(GlobalOptions.Verbose);
		Options.Add(GlobalOptions.NoTest);
	}

	/// <summary>
	/// Creates the handler for this command.
	/// </summary>
	/// <param name="processRunner">The process runner.</param>
	/// <param name="logger">The build logger.</param>
	/// <returns>The command handler action.</returns>
	public static Func<string, string, bool, bool, CancellationToken, Task<int>> CreateHandler(
		IProcessRunner processRunner,
		IBuildLogger logger)
	{
		return async (workspace, configuration, verbose, noTest, cancellationToken) =>
		{
			logger.VerboseEnabled = verbose;
			BuildEnvironment.Initialize();
			logger.WriteStepHeader("Starting Build Workflow");

			DotNetService dotNetService = new(processRunner, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				// Install dotnet-script if .csx files are present
				string? buildArgs = null;
				if (Directory.GetFiles(workspace, "*.csx", SearchOption.AllDirectories).Length > 0)
				{
					logger.WriteInfo("Installing dotnet-script tool...");
					await processRunner.RunWithCallbackAsync(
						"dotnet",
						"tool install -g dotnet-script",
						workspace,
						logger.WriteInfo,
						logger.WriteInfo, // Ignore errors (tool may already be installed)
						cancellationToken).ConfigureAwait(false);
					buildArgs = "-maxCpuCount:1";
				}

				await dotNetService.RestoreAsync(workspace, cancellationToken: cancellationToken).ConfigureAwait(false);
				await dotNetService.BuildAsync(workspace, configuration, buildArgs, cancellationToken).ConfigureAwait(false);

				if (!noTest)
				{
					await dotNetService.TestAsync(workspace, configuration, "coverage", cancellationToken).ConfigureAwait(false);
				}

				logger.WriteSuccess("Build workflow completed successfully!");
				return 0;
			}
			catch (Exception ex)
			{
				logger.WriteError($"Build workflow failed: {ex.Message}");
				return 1;
			}
#pragma warning restore CA1031
		};
	}
}
