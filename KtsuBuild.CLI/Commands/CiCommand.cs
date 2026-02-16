// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.CLI.Commands;

using System.CommandLine;
using KtsuBuild.Abstractions;
using KtsuBuild.Configuration;
using KtsuBuild.DotNet;
using KtsuBuild.Git;
using KtsuBuild.Metadata;
using KtsuBuild.Publishing;
using KtsuBuild.Utilities;

/// <summary>
/// CI command that runs the full CI/CD pipeline.
/// </summary>
#pragma warning disable CA1010 // System.CommandLine.Command implements IEnumerable for collection initializer support
public class CiCommand : Command
#pragma warning restore CA1010
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CiCommand"/> class.
	/// </summary>
	public CiCommand() : base("ci", "Run full CI/CD pipeline")
	{
		Options.Add(GlobalOptions.Workspace);
		Options.Add(GlobalOptions.Configuration);
		Options.Add(GlobalOptions.Verbose);
		Options.Add(GlobalOptions.DryRun);
		Options.Add(GlobalOptions.VersionBump);
	}

	/// <summary>
	/// Creates the handler for this command.
	/// </summary>
	/// <param name="processRunner">The process runner.</param>
	/// <param name="logger">The build logger.</param>
	/// <returns>The command handler action.</returns>
	public static Func<string, string, bool, bool, string, CancellationToken, Task<int>> CreateHandler(
		IProcessRunner processRunner,
		IBuildLogger logger)
	{
		return async (workspace, configuration, verbose, dryRun, versionBump, cancellationToken) =>
		{
			logger.VerboseEnabled = verbose;
			BuildEnvironment.Initialize();

			if (dryRun)
			{
				logger.WriteWarning("DRY RUN MODE - No changes will be made");
			}

			logger.WriteStepHeader("Starting CI/CD Pipeline");

			GitService gitService = new(processRunner, logger);
			GitHubService gitHubService = new(processRunner, gitService, logger);
			BuildConfigurationProvider configProvider = new(gitService, gitHubService);
			DotNetService dotNetService = new(processRunner, logger);
			MetadataService metadataService = new(gitService, logger);
			NuGetPublisher nugetPublisher = new(processRunner, logger);
			ReleaseService releaseService = new(dotNetService, nugetPublisher, gitHubService, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				// Create build configuration
				BuildConfiguration buildConfig = await configProvider.CreateFromEnvironmentAsync(workspace, cancellationToken).ConfigureAwait(false);
				buildConfig.Configuration = configuration;

				logger.WriteInfo($"Is Official: {buildConfig.IsOfficial}");
				logger.WriteInfo($"Is Main: {buildConfig.IsMain}");
				logger.WriteInfo($"Should Release: {buildConfig.ShouldRelease}");

				if (dryRun)
				{
					logger.WriteInfo("Would update metadata, build, test, and create release");
					return 0;
				}

				// Update metadata
				logger.WriteInfo("Updating metadata...");
				MetadataUpdateResult metadataResult = await metadataService.UpdateAllAsync(new MetadataUpdateOptions
				{
					BuildConfiguration = buildConfig,
				}, cancellationToken).ConfigureAwait(false);

				if (!metadataResult.Success)
				{
					logger.WriteError($"Metadata update failed: {metadataResult.Error}");
					return 1;
				}

				buildConfig.Version = metadataResult.Version;
				buildConfig.ReleaseHash = metadataResult.ReleaseHash;

				// Parse version bump option
				VersionType? forcedVersionType = ParseVersionBump(versionBump);

				// Check for skip condition
				VersionCalculator versionCalculator = new(gitService, logger);
				VersionInfo versionInfo = await versionCalculator.GetVersionInfoAsync(workspace, buildConfig.ReleaseHash, forcedVersionType: forcedVersionType, cancellationToken: cancellationToken).ConfigureAwait(false);

				if (versionInfo.VersionIncrement == VersionType.Skip)
				{
					logger.WriteInfo($"Skipping release: {versionInfo.IncrementReason}");
					return 0;
				}

				// Install dotnet-script if .csx files are present
				if (buildConfig.UseDotnetScript)
				{
					logger.WriteInfo("Installing dotnet-script tool...");
					await processRunner.RunWithCallbackAsync(
						"dotnet",
						"tool install -g dotnet-script",
						workspace,
						logger.WriteInfo,
						logger.WriteInfo, // Ignore errors (tool may already be installed)
						cancellationToken).ConfigureAwait(false);
				}

				// Build workflow
				await dotNetService.RestoreAsync(workspace, cancellationToken: cancellationToken).ConfigureAwait(false);
				await dotNetService.BuildAsync(workspace, configuration, buildConfig.BuildArgs, cancellationToken).ConfigureAwait(false);
				await dotNetService.TestAsync(workspace, configuration, "coverage", cancellationToken).ConfigureAwait(false);

				// Release workflow
				if (buildConfig.ShouldRelease)
				{
					await releaseService.ExecuteReleaseAsync(buildConfig, workspace, configuration, cancellationToken).ConfigureAwait(false);
				}

				logger.WriteSuccess("CI/CD pipeline completed successfully!");
				return 0;
			}
			catch (Exception ex)
			{
				logger.WriteError($"CI/CD pipeline failed: {ex.Message}");
				return 1;
			}
#pragma warning restore CA1031
		};
	}

	private static VersionType? ParseVersionBump(string versionBump) => versionBump.ToLowerInvariant() switch
	{
		"major" => VersionType.Major,
		"minor" => VersionType.Minor,
		"patch" => VersionType.Patch,
		_ => null,
	};
}
