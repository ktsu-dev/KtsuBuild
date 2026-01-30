// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.CLI.Commands;

using System.CommandLine;
using KtsuBuild.Abstractions;
using KtsuBuild.DotNet;

/// <summary>
/// Build command that runs restore, build, and test.
/// </summary>
public class BuildCommand : Command
{
	/// <summary>
	/// Initializes a new instance of the <see cref="BuildCommand"/> class.
	/// </summary>
	public BuildCommand() : base("build", "Build workflow: restore, build, test")
	{
		AddOption(GlobalOptions.Workspace);
		AddOption(GlobalOptions.Configuration);
		AddOption(GlobalOptions.Verbose);
	}

	/// <summary>
	/// Creates the handler for this command.
	/// </summary>
	/// <param name="processRunner">The process runner.</param>
	/// <param name="logger">The build logger.</param>
	/// <returns>The command handler action.</returns>
	public static Func<string, string, bool, CancellationToken, Task<int>> CreateHandler(
		IProcessRunner processRunner,
		IBuildLogger logger)
	{
		return async (workspace, configuration, verbose, cancellationToken) =>
		{
			logger.VerboseEnabled = verbose;
			logger.WriteStepHeader("Starting Build Workflow");

			var dotNetService = new DotNetService(processRunner, logger);

			try
			{
				await dotNetService.RestoreAsync(workspace, cancellationToken: cancellationToken).ConfigureAwait(false);
				await dotNetService.BuildAsync(workspace, configuration, cancellationToken: cancellationToken).ConfigureAwait(false);
				await dotNetService.TestAsync(workspace, configuration, "coverage", cancellationToken).ConfigureAwait(false);

				logger.WriteSuccess("Build workflow completed successfully!");
				return 0;
			}
			catch (Exception ex)
			{
				logger.WriteError($"Build workflow failed: {ex.Message}");
				return 1;
			}
		};
	}
}
