// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.CLI.Commands;

using System.CommandLine;
using KtsuBuild.Abstractions;
using KtsuBuild.Configuration;
using KtsuBuild.DotNet;
using KtsuBuild.Git;
using KtsuBuild.Publishing;

/// <summary>
/// Release command that runs pack, publish, and release.
/// </summary>
#pragma warning disable CA1010 // System.CommandLine.Command implements IEnumerable for collection initializer support
public class ReleaseCommand : Command
#pragma warning restore CA1010
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ReleaseCommand"/> class.
	/// </summary>
	public ReleaseCommand() : base("release", "Release workflow: pack, publish, release")
	{
		Options.Add(GlobalOptions.Workspace);
		Options.Add(GlobalOptions.Configuration);
		Options.Add(GlobalOptions.Verbose);
		Options.Add(GlobalOptions.DryRun);
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
		return async (workspace, configuration, verbose, dryRun, cancellationToken) =>
		{
			logger.VerboseEnabled = verbose;

			if (dryRun)
			{
				logger.WriteWarning("DRY RUN MODE - No changes will be made");
			}

			logger.WriteStepHeader("Starting Release Workflow");

			GitService gitService = new(processRunner, logger);
			GitHubService gitHubService = new(processRunner, gitService, logger);
			BuildConfigurationProvider configProvider = new(gitService, gitHubService);
			DotNetService dotNetService = new(processRunner, logger);
			NuGetPublisher nugetPublisher = new(processRunner, logger);
			ReleaseService releaseService = new(dotNetService, nugetPublisher, gitHubService, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				BuildConfiguration buildConfig = await configProvider.CreateFromEnvironmentAsync(workspace, cancellationToken).ConfigureAwait(false);
				buildConfig.Configuration = configuration;

				if (!buildConfig.ShouldRelease)
				{
					logger.WriteWarning("Not a release build (not on main, is tagged, or not official repo)");
					logger.WriteInfo($"Is Main: {buildConfig.IsMain}, Is Tagged: {buildConfig.IsTagged}, Is Official: {buildConfig.IsOfficial}");
					return 0;
				}

				if (dryRun)
				{
					logger.WriteInfo("Would pack, publish NuGet packages, and create GitHub release");
					return 0;
				}

				await releaseService.ExecuteReleaseAsync(buildConfig, workspace, configuration, cancellationToken).ConfigureAwait(false);

				logger.WriteSuccess("Release workflow completed successfully!");
				return 0;
			}
			catch (Exception ex)
			{
				logger.WriteError($"Release workflow failed: {ex.Message}");
				return 1;
			}
#pragma warning restore CA1031
		};
	}
}
