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

				// Check for skip condition
				VersionCalculator versionCalculator = new(gitService, logger);
				VersionInfo versionInfo = await versionCalculator.GetVersionInfoAsync(workspace, buildConfig.ReleaseHash, cancellationToken: cancellationToken).ConfigureAwait(false);

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
					// Pack
					await dotNetService.PackAsync(workspace, buildConfig.StagingPath, configuration, buildConfig.LatestChangelogFile, cancellationToken).ConfigureAwait(false);

					// Publish applications
					IReadOnlyList<string> projectFiles = dotNetService.GetProjectFiles(workspace);
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
								await System.IO.Compression.ZipFile.CreateFromDirectoryAsync(outputDir, zipPath, cancellationToken).ConfigureAwait(false);
								logger.WriteInfo($"Created: {zipPath}");
							}
						}
					}

					// Generate SHA256 hashes for all zip archives
					string[] zipFiles = Directory.GetFiles(buildConfig.StagingPath, "*.zip");
					if (zipFiles.Length > 0)
					{
						string hashesPath = Path.Combine(buildConfig.StagingPath, "hashes.txt");
						List<string> hashEntries = [];
						foreach (string zipFile in zipFiles)
						{
							byte[] fileBytes = await File.ReadAllBytesAsync(zipFile, cancellationToken).ConfigureAwait(false);
							byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(fileBytes);
							string hash = Convert.ToHexString(hashBytes);
							string fileName = Path.GetFileName(zipFile);
							hashEntries.Add($"{fileName}={hash}");
							logger.WriteInfo($"SHA256: {fileName} = {hash}");
						}

						await File.WriteAllLinesAsync(hashesPath, hashEntries, cancellationToken).ConfigureAwait(false);
						logger.WriteInfo($"Hashes written to: {hashesPath}");
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
					ReleaseOptions releaseOptions = new()
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
#pragma warning restore CA1031
		};
	}
}
