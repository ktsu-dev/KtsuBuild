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
public class ReleaseCommand : Command
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

			var gitService = new GitService(processRunner, logger);
			var gitHubService = new GitHubService(processRunner, gitService, logger);
			var configProvider = new BuildConfigurationProvider(gitService, gitHubService, logger);
			var dotNetService = new DotNetService(processRunner, logger);
			var nugetPublisher = new NuGetPublisher(processRunner, logger);

			try
			{
				var buildConfig = await configProvider.CreateFromEnvironmentAsync(workspace, cancellationToken).ConfigureAwait(false);
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

				// Pack
				await dotNetService.PackAsync(workspace, buildConfig.StagingPath, configuration, buildConfig.LatestChangelogFile, cancellationToken).ConfigureAwait(false);

				// Publish NuGet packages
				string[] packages = Directory.GetFiles(buildConfig.StagingPath, "*.nupkg");
				if (packages.Length > 0 && !string.IsNullOrEmpty(buildConfig.GithubToken))
				{
					await nugetPublisher.PublishToGitHubAsync(buildConfig.PackagePattern, buildConfig.GitHubOwner, buildConfig.GithubToken, cancellationToken).ConfigureAwait(false);

					if (!string.IsNullOrEmpty(buildConfig.NuGetApiKey))
					{
						await nugetPublisher.PublishToNuGetOrgAsync(buildConfig.PackagePattern, buildConfig.NuGetApiKey, cancellationToken).ConfigureAwait(false);
					}

					if (!string.IsNullOrEmpty(buildConfig.KtsuPackageKey))
					{
						await nugetPublisher.PublishToSourceAsync(buildConfig.PackagePattern, "https://packages.ktsu.dev/v3/index.json", buildConfig.KtsuPackageKey, cancellationToken).ConfigureAwait(false);
					}
				}

				// Create release
				var releaseOptions = new ReleaseOptions
				{
					Version = buildConfig.Version,
					CommitHash = buildConfig.ReleaseHash,
					GithubToken = buildConfig.GithubToken,
					LatestChangelogFile = buildConfig.LatestChangelogFile,
					AssetPaths = buildConfig.AssetPatterns,
					WorkingDirectory = workspace,
				};

				await gitHubService.CreateReleaseAsync(releaseOptions, cancellationToken).ConfigureAwait(false);

				logger.WriteSuccess("Release workflow completed successfully!");
				return 0;
			}
			catch (Exception ex)
			{
				logger.WriteError($"Release workflow failed: {ex.Message}");
				return 1;
			}
		};
	}
}
