// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tool.Commands;

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
	/// The inputs to a CI pipeline run. A record rather than positional parameters because four
	/// adjacent booleans in a delegate signature can be transposed without a compiler error, and
	/// a run that passes several of them at once cannot tell the transposition apart from correct
	/// wiring.
	/// </summary>
	/// <param name="Workspace">The workspace or repository path.</param>
	/// <param name="Configuration">The build configuration, Debug or Release.</param>
	/// <param name="Verbose">Whether to enable verbose logging.</param>
	/// <param name="DryRun">Whether to report what would happen without making changes.</param>
	/// <param name="VersionBump">The forced version bump type, or <c>auto</c> to detect it.</param>
	/// <param name="NoTest">Whether to skip the test step, for a pipeline whose tests run elsewhere.</param>
	/// <param name="NoRelease">Whether to skip the release step, leaving it to a later step or job.</param>
	public sealed record CiOptions(
		string Workspace,
		string Configuration,
		bool Verbose,
		bool DryRun,
		string VersionBump,
		bool NoTest,
		bool NoRelease);

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
		Options.Add(GlobalOptions.NoTest);
		Options.Add(GlobalOptions.NoRelease);
	}

	/// <summary>
	/// Creates the handler for this command.
	/// </summary>
	/// <param name="processRunner">The process runner.</param>
	/// <param name="logger">The build logger.</param>
	/// <returns>The command handler action.</returns>
	public static Func<CiOptions, CancellationToken, Task<int>> CreateHandler(
		IProcessRunner processRunner,
		IBuildLogger logger)
	{
		return async (options, cancellationToken) =>
		{
			logger.VerboseEnabled = options.Verbose;
			BuildEnvironment.Initialize();

			if (options.DryRun)
			{
				logger.WriteWarning("DRY RUN MODE - No changes will be made");
			}

			logger.WriteStepHeader("Starting CI/CD Pipeline");

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				return await ExecutePipelineAsync(processRunner, logger, options, cancellationToken).ConfigureAwait(false);
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
		CiOptions options,
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
		BuildConfiguration buildConfig = await configProvider.CreateFromEnvironmentAsync(options.Workspace, cancellationToken).ConfigureAwait(false);
		buildConfig.Configuration = options.Configuration;

		logger.WriteInfo($"Is Official: {buildConfig.IsOfficial}");
		logger.WriteInfo($"Is Main: {buildConfig.IsMain}");
		logger.WriteInfo($"Should Release: {buildConfig.ShouldRelease}");

		if (options.DryRun)
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
			await UpdateRepositoryTopicsAsync(gitHubService, logger, options.Workspace, cancellationToken).ConfigureAwait(false);
		}

		// Parse version bump option
		VersionType? forcedVersionType = ParseVersionBump(options.VersionBump);

		// Check for skip condition. This gates the release only; build and test still run.
		// Returning early here instead would leave a workspace whose commits all carry [skip ci]
		// never compiled and never tested, which hollows out scheduled runs, and breaks any job
		// that wraps the pipeline in a step pair expecting a compilation between them, such as
		// the SonarQube scanner's begin/end.
		VersionCalculator versionCalculator = new(gitService, logger);
		VersionInfo versionInfo = await versionCalculator.GetVersionInfoAsync(options.Workspace, buildConfig.ReleaseHash, forcedVersionType: forcedVersionType, cancellationToken: cancellationToken).ConfigureAwait(false);

		bool skipRelease = versionInfo.VersionIncrement == VersionType.Skip;
		if (skipRelease)
		{
			logger.WriteInfo($"Skipping release: {versionInfo.IncrementReason}");
		}

		// Install dotnet-script if .csx files are present
		if (buildConfig.UseDotnetScript)
		{
			logger.WriteInfo("Installing dotnet-script tool...");
			await processRunner.RunWithCallbackAsync(
				"dotnet",
				"tool install -g dotnet-script",
				options.Workspace,
				logger.WriteInfo,
				logger.WriteInfo, // Ignore errors (tool may already be installed)
				cancellationToken).ConfigureAwait(false);
		}

		// Build workflow
		await dotNetService.RestoreAsync(options.Workspace, cancellationToken: cancellationToken).ConfigureAwait(false);
		await dotNetService.BuildAsync(options.Workspace, options.Configuration, buildConfig.BuildArgs, cancellationToken).ConfigureAwait(false);

		// A caller that runs the tests elsewhere, such as a workflow that fans them across a
		// matrix, still needs everything around them: metadata, the version gate, a compilation
		// inside the SonarQube begin and end window, and the step outputs.
		if (!options.NoTest)
		{
			await dotNetService.TestAsync(options.Workspace, options.Configuration, "coverage", cancellationToken).ConfigureAwait(false);
		}

		// iOS validation: when the workspace contains an iOS head, build it unsigned
		// for the simulator and device runtimes as part of CI, the same pull-request
		// validation `ios build` performs. Running it automatically means a consumer
		// with an iOS head does not need a separate invocation. iOS builds only on
		// macOS, so on any other host the step reports the detected heads and skips.
		if (!await ExecuteIosValidationAsync(dotNetService, logger, options.Workspace, options.Configuration, cancellationToken).ConfigureAwait(false))
		{
			return 1;
		}

		// Release workflow
		if (CiReleaseDecision.ShouldExecuteRelease(buildConfig.ShouldRelease, skipRelease, suppressedByFlag: options.NoRelease))
		{
			await releaseService.ExecuteReleaseAsync(buildConfig, options.Workspace, options.Configuration, cancellationToken).ConfigureAwait(false);
		}

		WriteStepOutputs(buildConfig, releaseSkipped: skipRelease);

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

			// IosCiDisposition.Build, and any disposition added later, should build rather
			// than silently skip the validation.
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

	/// <summary>
	/// Publishes what the pipeline decided to the GitHub Actions step outputs, so the workflow
	/// gates its later steps on the run that actually happened rather than re-deriving the
	/// decision from git state. A skipped release still builds and tests, so only the jobs that
	/// publish are gated off: they would otherwise be publishing a version that already shipped.
	/// <para>
	/// <c>build_skipped</c> is now always false, because every run reaches the build. It stays in
	/// the output set because consuming workflows gate the SonarQube end step on it, and that step
	/// must run whenever a compilation happened inside the scanner's begin/end window.
	/// </para>
	/// </summary>
	/// <param name="buildConfig">The configuration the pipeline ran with.</param>
	/// <param name="releaseSkipped">Whether the version increment suppressed the release.</param>
	private static void WriteStepOutputs(BuildConfiguration buildConfig, bool releaseSkipped) =>
		GitHubActionsOutput.Write(
		[
			new("version", buildConfig.Version),
			new("release_hash", buildConfig.ReleaseHash),
			new("should_release", CiReleaseDecision.ShouldReleaseOutput(buildConfig.ShouldRelease, releaseSkipped)),
			new("build_skipped", "false"),
		]);

	private static VersionType? ParseVersionBump(string versionBump) => versionBump.ToLowerInvariant() switch
	{
		"major" => VersionType.Major,
		"minor" => VersionType.Minor,
		"patch" => VersionType.Patch,
		_ => null,
	};
}
