// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Pipeline;

using System.Diagnostics.CodeAnalysis;
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
/// The stages a CI/CD run is made of, each one runnable on its own.
/// </summary>
/// <remarks>
/// The stages live here rather than in the <c>ci</c> command so that <c>release</c> and
/// <c>build</c> run the same code paths instead of near copies. A near copy is what published a
/// release under the placeholder version <c>1.0.0-pre.0</c>: <c>ci</c> overwrote the placeholder
/// that <see cref="BuildConfigurationProvider"/> seeds and <c>release</c> did not.
/// <para>
/// This class lives in the library rather than the tool because only the library is referenced by
/// the test project, so this is what makes the stages testable at all.
/// </para>
/// </remarks>
public sealed class PipelineService
{
	private readonly IProcessRunner _processRunner;
	private readonly IBuildLogger _logger;
	private readonly GitService _gitService;
	private readonly GitHubService _gitHubService;
	private readonly BuildConfigurationProvider _configProvider;
	private readonly DotNetService _dotNetService;
	private readonly MetadataService _metadataService;
	private readonly ReleaseService _releaseService;

	/// <summary>
	/// Initializes a new instance of the <see cref="PipelineService"/> class.
	/// </summary>
	/// <param name="processRunner">The process runner every external command goes through.</param>
	/// <param name="logger">The build logger.</param>
	public PipelineService(IProcessRunner processRunner, IBuildLogger logger)
	{
		Ensure.NotNull(processRunner);
		Ensure.NotNull(logger);

		_processRunner = processRunner;
		_logger = logger;
		_gitService = new GitService(processRunner, logger);
		_gitHubService = new GitHubService(processRunner, _gitService, logger);
		_configProvider = new BuildConfigurationProvider(_gitService, _gitHubService);
		_dotNetService = new DotNetService(processRunner, logger);
		_metadataService = new MetadataService(_gitService, logger);
		NuGetPublisher nugetPublisher = new(processRunner, logger);
		_releaseService = new ReleaseService(_dotNetService, nugetPublisher, _gitHubService, logger);
	}

	/// <summary>
	/// Builds the configuration for a run from the environment and reports what it decided.
	/// </summary>
	/// <remarks>
	/// Preparation does no version work. Resolving the version is <see cref="ResolveVersionAsync"/>,
	/// a separate stage because <c>ci</c> resolves against the metadata commit, which does not exist
	/// yet at this point. A caller that publishes runs both.
	/// </remarks>
	/// <param name="workspace">The workspace or repository path.</param>
	/// <param name="configuration">The build configuration, Debug or Release.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The prepared context, with no version resolved yet.</returns>
	public async Task<PipelineContext> PrepareAsync(string workspace, string configuration, CancellationToken cancellationToken)
	{
		Ensure.NotNull(workspace);
		Ensure.NotNull(configuration);

		BuildConfiguration buildConfig = await _configProvider.CreateFromEnvironmentAsync(workspace, cancellationToken).ConfigureAwait(false);
		buildConfig.Configuration = configuration;

		_logger.WriteInfo($"Is Official: {buildConfig.IsOfficial}");
		_logger.WriteInfo($"Is Main: {buildConfig.IsMain}");
		_logger.WriteInfo($"Should Release: {buildConfig.ShouldRelease}");

		return new PipelineContext
		{
			Configuration = buildConfig,
		};
	}

	/// <summary>
	/// Resolves the version this run would produce and decides whether the version gate suppresses
	/// the release.
	/// </summary>
	/// <remarks>
	/// The analysis runs against <c>Configuration.ReleaseHash</c>, which is why this is a stage of
	/// its own rather than part of preparation. In <c>ci</c> the metadata stage has already moved
	/// that hash onto the metadata commit, so the range analyzed here includes it, and the answer
	/// is about the commit the release is cut against rather than the one the run started on.
	/// <para>
	/// Nothing here writes <c>Configuration.Version</c> or <c>Configuration.ReleaseHash</c>. In
	/// <c>ci</c> both come from the metadata result. A caller that publishes without a metadata
	/// commit sets <c>Configuration.Version</c> from <see cref="PipelineContext.VersionInfo"/>
	/// itself.
	/// </para>
	/// </remarks>
	/// <param name="context">The context this run was prepared with.</param>
	/// <param name="versionBump">The forced version bump type, or anything else to detect it.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public async Task ResolveVersionAsync(PipelineContext context, string versionBump, CancellationToken cancellationToken)
	{
		Ensure.NotNull(context);
		Ensure.NotNull(versionBump);

		BuildConfiguration buildConfig = context.Configuration;

		// Parse version bump option
		VersionType? forcedVersionType = ParseVersionBump(versionBump);

		// Check for skip condition. This gates the release only; build and test still run.
		// Returning early here instead would leave a workspace whose commits all carry [skip ci]
		// never compiled and never tested, which hollows out scheduled runs, and breaks any job
		// that wraps the pipeline in a step pair expecting a compilation between them, such as
		// the SonarQube scanner's begin/end.
		VersionCalculator versionCalculator = new(_gitService, _logger);
		VersionInfo versionInfo = await versionCalculator.GetVersionInfoAsync(buildConfig.WorkspacePath, buildConfig.ReleaseHash, forcedVersionType: forcedVersionType, cancellationToken: cancellationToken).ConfigureAwait(false);

		bool skipRelease = versionInfo.VersionIncrement == VersionType.Skip;
		if (skipRelease)
		{
			_logger.WriteInfo($"Skipping release: {versionInfo.IncrementReason}");
		}

		context.VersionInfo = versionInfo;
		context.ReleaseSuppressedByVersionGate = skipRelease;
	}

	/// <summary>
	/// Regenerates the metadata files, commits them when the run is official and on main, and
	/// updates the repository topics from <c>TAGS.md</c>.
	/// </summary>
	/// <remarks>
	/// The version and release hash on the configuration are taken from the metadata result,
	/// because the metadata commit is a new commit and it is the one a release is cut against.
	/// <para>
	/// The result is returned rather than turned into an exception so that a caller reports a
	/// metadata failure in its own words. Throwing would put a catch-all handler's wording in
	/// front of the message, which anything reading the log can see.
	/// </para>
	/// </remarks>
	/// <param name="context">The context this run was prepared with.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The metadata result, carrying its error message when the update failed.</returns>
	public async Task<MetadataUpdateResult> UpdateMetadataAsync(PipelineContext context, CancellationToken cancellationToken)
	{
		Ensure.NotNull(context);

		BuildConfiguration buildConfig = context.Configuration;

		bool shouldCommitMetadata = buildConfig.IsOfficial && buildConfig.IsMain;
		if (!shouldCommitMetadata)
		{
			_logger.WriteInfo("Skipping metadata commit (not official or not main branch)");
		}

		_logger.WriteInfo("Updating metadata...");
		MetadataUpdateResult metadataResult = await _metadataService.UpdateAllAsync(new MetadataUpdateOptions
		{
			BuildConfiguration = buildConfig,
			CommitChanges = shouldCommitMetadata,
		}, cancellationToken).ConfigureAwait(false);

		if (!metadataResult.Success)
		{
			return metadataResult;
		}

		buildConfig.Version = metadataResult.Version;
		buildConfig.ReleaseHash = metadataResult.ReleaseHash;

		// Update GitHub repository topics from TAGS.md
		if (shouldCommitMetadata)
		{
			await UpdateRepositoryTopicsAsync(buildConfig.WorkspacePath, cancellationToken).ConfigureAwait(false);
		}

		return metadataResult;
	}

	/// <summary>
	/// Restores and builds the workspace, installing dotnet-script first when the workspace
	/// contains any <c>.csx</c> files.
	/// </summary>
	/// <remarks>
	/// The <c>.csx</c> check is made here rather than read off a configuration so that a caller
	/// with no git repository and no GitHub token can still run this stage. It is the same test
	/// <see cref="BuildConfigurationProvider"/> makes, over the same directory.
	/// </remarks>
	/// <param name="workspace">The workspace or repository path.</param>
	/// <param name="configuration">The build configuration, Debug or Release.</param>
	/// <param name="buildArgs">Additional arguments to pass to the build, or null for none.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public async Task RestoreAndBuildAsync(string workspace, string configuration, string? buildArgs, CancellationToken cancellationToken)
	{
		Ensure.NotNull(workspace);
		Ensure.NotNull(configuration);

		// Install dotnet-script if .csx files are present
		if (Directory.GetFiles(workspace, "*.csx", SearchOption.AllDirectories).Length > 0)
		{
			_logger.WriteInfo("Installing dotnet-script tool...");
			await _processRunner.RunWithCallbackAsync(
				"dotnet",
				"tool install -g dotnet-script",
				workspace,
				_logger.WriteInfo,
				_logger.WriteInfo, // Ignore errors (tool may already be installed)
				cancellationToken).ConfigureAwait(false);
		}

		// Build workflow
		await _dotNetService.RestoreAsync(workspace, cancellationToken: cancellationToken).ConfigureAwait(false);
		await _dotNetService.BuildAsync(workspace, configuration, buildArgs, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Runs the tests with coverage collection.
	/// </summary>
	/// <param name="workspace">The workspace or repository path.</param>
	/// <param name="configuration">The build configuration, Debug or Release.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public async Task RunTestsAsync(string workspace, string configuration, CancellationToken cancellationToken)
	{
		Ensure.NotNull(workspace);
		Ensure.NotNull(configuration);

		await _dotNetService.TestAsync(workspace, configuration, "coverage", cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Runs the unsigned iOS validation build when the workspace contains an iOS head
	/// and the host is macOS, mirroring the <c>ios build</c> command. Returns false only
	/// when an iOS build actually ran and failed; detecting no heads, or skipping on a
	/// non-macOS host, both report cleanly and return true.
	/// </summary>
	/// <param name="workspace">The workspace or repository path.</param>
	/// <param name="configuration">The build configuration, Debug or Release.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>False only when an iOS build ran and failed.</returns>
	public async Task<bool> ValidateIosAsync(
		string workspace,
		string configuration,
		CancellationToken cancellationToken)
	{
		Ensure.NotNull(workspace);
		Ensure.NotNull(configuration);

		IReadOnlyList<string> iosHeads = _dotNetService.GetIosHeads(workspace);
		IosCiDisposition disposition = IosBuildService.ClassifyForCi(
			iosHeads.Count,
			RuntimeInformation.IsOSPlatform(OSPlatform.OSX));

		switch (disposition)
		{
			case IosCiDisposition.NoHeads:
				_logger.WriteVerbose("No iOS heads detected in workspace. Skipping iOS validation.");
				return true;

			case IosCiDisposition.SkipNotMacOs:
				_logger.WriteInfo($"Detected {iosHeads.Count} iOS head(s), but iOS builds require a macOS host. Skipping iOS validation on this platform (it runs on a macOS CI job).");
				return true;

			// IosCiDisposition.Build, and any disposition added later, should build rather
			// than silently skip the validation.
			default:
				_logger.WriteStepHeader("Validating iOS Head(s)");
				IosBuildService iosBuildService = new(_dotNetService, _logger);
				bool success = await iosBuildService.BuildAsync(new IosBuildOptions
				{
					WorkingDirectory = workspace,
					Configuration = configuration,
				}, cancellationToken).ConfigureAwait(false);

				if (success)
				{
					_logger.WriteSuccess("iOS validation build(s) completed successfully!");
				}
				else
				{
					_logger.WriteError("iOS validation build failed.");
				}

				return success;
		}
	}

	/// <summary>
	/// Packs, publishes and tags the release the context describes.
	/// </summary>
	/// <param name="context">The context this run was prepared with.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public async Task ReleaseAsync(PipelineContext context, CancellationToken cancellationToken)
	{
		Ensure.NotNull(context);

		BuildConfiguration buildConfig = context.Configuration;
		await _releaseService.ExecuteReleaseAsync(buildConfig, buildConfig.WorkspacePath, buildConfig.Configuration, cancellationToken).ConfigureAwait(false);
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
	/// <param name="context">The context this run was prepared with.</param>
	[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Every stage is an instance member so a caller holds one service and calls them in order. Making this one static alone would change how callers spell the last line of a pipeline for no gain.")]
	public void WriteStepOutputs(PipelineContext context)
	{
		Ensure.NotNull(context);

		BuildConfiguration buildConfig = context.Configuration;
		GitHubActionsOutput.Write(
		[
			new("version", buildConfig.Version),
			new("release_hash", buildConfig.ReleaseHash),
			new("should_release", CiReleaseDecision.ShouldReleaseOutput(buildConfig.ShouldRelease, context.ReleaseSuppressedByVersionGate)),
			new("build_skipped", "false"),
		]);
	}

	private async Task UpdateRepositoryTopicsAsync(
		string workspace,
		CancellationToken cancellationToken)
	{
		string tagsFile = Path.Combine(workspace, "TAGS.md");
		if (!File.Exists(tagsFile))
		{
			_logger.WriteVerbose("No TAGS.md found, skipping repository topic update.");
			return;
		}

#pragma warning disable CA1031 // Topic update is non-fatal
		try
		{
			IReadOnlyList<string> topics = await TagsParser.ParseAsync(tagsFile, cancellationToken).ConfigureAwait(false);
			if (topics.Count > 0)
			{
				await _gitHubService.SetRepositoryTopicsAsync(workspace, topics, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			_logger.WriteWarning($"Failed to update repository topics: {ex.Message}");
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
