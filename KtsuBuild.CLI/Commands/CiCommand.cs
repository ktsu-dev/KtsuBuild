// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.CLI.Commands;

using System.CommandLine;
using System.Runtime.InteropServices;
using KtsuBuild.Abstractions;
using KtsuBuild.Configuration;
using KtsuBuild.DotNet;
using KtsuBuild.Git;
using KtsuBuild.Ios;
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

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				return await ExecutePipelineAsync(processRunner, logger, workspace, configuration, dryRun, versionBump, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				logger.WriteError($"CI/CD pipeline failed: {ex.Message}");
				return 1;
			}
#pragma warning restore CA1031
		};
	}

	private static async Task<int> ExecutePipelineAsync(
		IProcessRunner processRunner,
		IBuildLogger logger,
		string workspace,
		string configuration,
		bool dryRun,
		string versionBump,
		CancellationToken cancellationToken)
	{
		GitService gitService = new(processRunner, logger);
		GitHubService gitHubService = new(processRunner, gitService, logger);
		BuildConfigurationProvider configProvider = new(gitService, gitHubService);
		DotNetService dotNetService = new(processRunner, logger);
		MetadataService metadataService = new(gitService, logger);
		NuGetPublisher nugetPublisher = new(processRunner, logger);
		ReleaseService releaseService = new(dotNetService, nugetPublisher, gitHubService, logger);

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
		bool shouldCommitMetadata = buildConfig.IsOfficial && buildConfig.IsMain;
		if (!shouldCommitMetadata)
		{
			logger.WriteInfo("Skipping metadata commit (not official or not main branch)");
		}

		logger.WriteInfo("Updating metadata...");
		MetadataUpdateResult metadataResult = await metadataService.UpdateAllAsync(new MetadataUpdateOptions
		{
			BuildConfiguration = buildConfig,
			CommitChanges = shouldCommitMetadata,
		}, cancellationToken).ConfigureAwait(false);

		if (!metadataResult.Success)
		{
			logger.WriteError($"Metadata update failed: {metadataResult.Error}");
			return 1;
		}

		buildConfig.Version = metadataResult.Version;
		buildConfig.ReleaseHash = metadataResult.ReleaseHash;

		// Update GitHub repository topics from TAGS.md
		if (shouldCommitMetadata)
		{
			await UpdateRepositoryTopicsAsync(gitHubService, logger, workspace, cancellationToken).ConfigureAwait(false);
		}

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

		// iOS validation: when the workspace contains an iOS head, build it unsigned
		// for the simulator and device runtimes as part of CI, the same pull-request
		// validation `ios build` performs. Running it automatically means a consumer
		// with an iOS head does not need a separate invocation. iOS builds only on
		// macOS, so on any other host the step reports the detected heads and skips.
		if (!await ExecuteIosValidationAsync(dotNetService, logger, workspace, configuration, cancellationToken).ConfigureAwait(false))
		{
			return 1;
		}

		// Release workflow
		if (buildConfig.ShouldRelease)
		{
			await releaseService.ExecuteReleaseAsync(buildConfig, workspace, configuration, cancellationToken).ConfigureAwait(false);
		}

		logger.WriteSuccess("CI/CD pipeline completed successfully!");
		return 0;
	}

	/// <summary>
	/// Runs the unsigned iOS validation build when the workspace contains an iOS head
	/// and the host is macOS, mirroring the <c>ios build</c> command. Returns false only
	/// when an iOS build actually ran and failed; detecting no heads, or skipping on a
	/// non-macOS host, both report cleanly and return true.
	/// </summary>
	private static async Task<bool> ExecuteIosValidationAsync(
		DotNetService dotNetService,
		IBuildLogger logger,
		string workspace,
		string configuration,
		CancellationToken cancellationToken)
	{
		IReadOnlyList<string> iosHeads = dotNetService.GetIosHeads(workspace);
		IosCiDisposition disposition = IosBuildService.ClassifyForCi(
			iosHeads.Count,
			RuntimeInformation.IsOSPlatform(OSPlatform.OSX));

		switch (disposition)
		{
			case IosCiDisposition.NoHeads:
				logger.WriteVerbose("No iOS heads detected in workspace. Skipping iOS validation.");
				return true;

			case IosCiDisposition.SkipNotMacOs:
				logger.WriteInfo($"Detected {iosHeads.Count} iOS head(s), but iOS builds require a macOS host. Skipping iOS validation on this platform (it runs on a macOS CI job).");
				return true;

			case IosCiDisposition.Build:
			default:
				logger.WriteStepHeader("Validating iOS Head(s)");
				IosBuildService iosBuildService = new(dotNetService, logger);
				bool success = await iosBuildService.BuildAsync(new IosBuildOptions
				{
					WorkingDirectory = workspace,
					Configuration = configuration,
				}, cancellationToken).ConfigureAwait(false);

				if (success)
				{
					logger.WriteSuccess("iOS validation build(s) completed successfully!");
				}
				else
				{
					logger.WriteError("iOS validation build failed.");
				}

				return success;
		}
	}

	private static async Task UpdateRepositoryTopicsAsync(
		GitHubService gitHubService,
		IBuildLogger logger,
		string workspace,
		CancellationToken cancellationToken)
	{
		string tagsFile = Path.Combine(workspace, "TAGS.md");
		if (!File.Exists(tagsFile))
		{
			logger.WriteVerbose("No TAGS.md found, skipping repository topic update.");
			return;
		}

#pragma warning disable CA1031 // Topic update is non-fatal
		try
		{
			IReadOnlyList<string> topics = await TagsParser.ParseAsync(tagsFile, cancellationToken).ConfigureAwait(false);
			if (topics.Count > 0)
			{
				await gitHubService.SetRepositoryTopicsAsync(workspace, topics, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			logger.WriteWarning($"Failed to update repository topics: {ex.Message}");
		}
#pragma warning restore CA1031
	}

	private static VersionType? ParseVersionBump(string versionBump) => versionBump.ToLowerInvariant() switch
	{
		"major" => VersionType.Major,
		"minor" => VersionType.Minor,
		"patch" => VersionType.Patch,
		_ => null,
	};
}
