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

/// <summary>
/// CI command that runs the full CI/CD pipeline.
/// </summary>
public class CiCommand : Command
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CiCommand"/> class.
	/// </summary>
	public CiCommand() : base("ci", "Run full CI/CD pipeline")
	{
		AddOption(GlobalOptions.Workspace);
		AddOption(GlobalOptions.Configuration);
		AddOption(GlobalOptions.Verbose);
		AddOption(GlobalOptions.DryRun);
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

			logger.WriteStepHeader("Starting CI/CD Pipeline");

			var gitService = new GitService(processRunner, logger);
			var gitHubService = new GitHubService(processRunner, gitService, logger);
			var configProvider = new BuildConfigurationProvider(gitService, gitHubService, logger);
			var dotNetService = new DotNetService(processRunner, logger);
			var metadataService = new MetadataService(gitService, logger);
			var nugetPublisher = new NuGetPublisher(processRunner, logger);

			try
			{
				// Create build configuration
				var buildConfig = await configProvider.CreateFromEnvironmentAsync(workspace, cancellationToken).ConfigureAwait(false);
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
				var metadataResult = await metadataService.UpdateAllAsync(new MetadataUpdateOptions
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

				// Check for skip condition
				var versionCalculator = new VersionCalculator(gitService, logger);
				var versionInfo = await versionCalculator.GetVersionInfoAsync(workspace, buildConfig.ReleaseHash, cancellationToken: cancellationToken).ConfigureAwait(false);

				if (versionInfo.VersionIncrement == VersionType.Skip)
				{
					logger.WriteInfo($"Skipping release: {versionInfo.IncrementReason}");
					return 0;
				}

				// Build workflow
				await dotNetService.RestoreAsync(workspace, cancellationToken: cancellationToken).ConfigureAwait(false);
				await dotNetService.BuildAsync(workspace, configuration, buildConfig.BuildArgs, cancellationToken).ConfigureAwait(false);
				await dotNetService.TestAsync(workspace, configuration, "coverage", cancellationToken).ConfigureAwait(false);

				// Release workflow
				if (buildConfig.ShouldRelease)
				{
					// Pack
					await dotNetService.PackAsync(workspace, buildConfig.StagingPath, configuration, buildConfig.LatestChangelogFile, cancellationToken).ConfigureAwait(false);

					// Publish applications
					var projectFiles = dotNetService.GetProjectFiles(workspace);
					foreach (string project in projectFiles.Where(p => dotNetService.IsExecutableProject(p)))
					{
						string projectName = Path.GetFileNameWithoutExtension(project);
						string[] runtimes = ["win-x64", "win-x86", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"];

						foreach (string runtime in runtimes)
						{
							string outputDir = Path.Combine(buildConfig.OutputPath, $"{projectName}-{runtime}");
							await dotNetService.PublishAsync(workspace, project, outputDir, runtime, configuration, cancellationToken: cancellationToken).ConfigureAwait(false);

							// Create zip archive
							string zipPath = Path.Combine(buildConfig.StagingPath, $"{projectName}-{buildConfig.Version}-{runtime}.zip");
							if (Directory.Exists(outputDir))
							{
								System.IO.Compression.ZipFile.CreateFromDirectory(outputDir, zipPath);
								logger.WriteInfo($"Created: {zipPath}");
							}
						}
					}

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

					// Create GitHub release
					var releaseOptions = new ReleaseOptions
					{
						Version = buildConfig.Version,
						CommitHash = buildConfig.ReleaseHash,
						GithubToken = buildConfig.GithubToken,
						ChangelogFile = buildConfig.ChangelogFile,
						LatestChangelogFile = buildConfig.LatestChangelogFile,
						AssetPaths = buildConfig.AssetPatterns,
						IsPrerelease = buildConfig.Version.Contains("-pre") || buildConfig.Version.Contains("-alpha") || buildConfig.Version.Contains("-beta"),
						WorkingDirectory = workspace,
					};

					await gitHubService.CreateReleaseAsync(releaseOptions, cancellationToken).ConfigureAwait(false);
				}

				logger.WriteSuccess("CI/CD pipeline completed successfully!");
				return 0;
			}
			catch (Exception ex)
			{
				logger.WriteError($"CI/CD pipeline failed: {ex.Message}");
				return 1;
			}
		};
	}
}
