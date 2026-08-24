// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tool.Commands;

using System.CommandLine;
using KtsuBuild.Abstractions;
using KtsuBuild.Configuration;
using KtsuBuild.Pipeline;

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

			PipelineService pipeline = new(processRunner, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				PipelineContext context = await pipeline.PrepareAsync(workspace, configuration, cancellationToken).ConfigureAwait(false);
				BuildConfiguration buildConfig = context.Configuration;

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

				// ReleaseHash already equals GitSha at this point (BuildConfigurationProvider seeds
				// it from GITHUB_SHA, falling back to the current commit), so this restates an
				// invariant the provider already establishes rather than changing it. It is
				// restated here because resolving the version below reads it to pick the commit
				// range, and this is the one caller with no metadata stage to set it later.
				buildConfig.ReleaseHash = buildConfig.GitSha;

				await pipeline.ResolveVersionAsync(context, "auto", cancellationToken).ConfigureAwait(false);

				// Resolving deliberately leaves Configuration.Version alone, because in ci it is
				// assigned from the metadata result instead. A standalone release has no metadata
				// result, so this is the one caller that must assign it here. Falling back to
				// anything else would risk publishing under an unresolved placeholder, which is
				// the defect this command exists to fix.
				if (context.VersionInfo is null)
				{
					throw new InvalidOperationException("Version resolution did not produce a version to release.");
				}

				buildConfig.Version = context.VersionInfo.Version;

				// The version gate is how [skip ci] and a run with no meaningful changes suppress
				// a release. A standalone release must honor it the same way ci does, so a run
				// whose commits all carry the skip marker does not publish here either.
				if (CiReleaseDecision.ShouldExecuteRelease(buildConfig.ShouldRelease, context.ReleaseSuppressedByVersionGate, suppressedByFlag: false))
				{
					await pipeline.ReleaseAsync(context, cancellationToken).ConfigureAwait(false);
				}

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
