// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tool.Commands;

using System.CommandLine;
using KtsuBuild.Abstractions;
using KtsuBuild.Configuration;
using KtsuBuild.Git;
using KtsuBuild.Pipeline;

/// <summary>
/// Release command that runs pack, publish, and release.
/// </summary>
#pragma warning disable CA1010 // System.CommandLine.Command implements IEnumerable for collection initializer support
public class ReleaseCommand : Command
#pragma warning restore CA1010
{
	/// <summary>
	/// The inputs to a release run. A record rather than positional parameters because adjacent
	/// booleans in a delegate signature can be transposed without a compiler error.
	/// </summary>
	/// <param name="Workspace">The workspace or repository path.</param>
	/// <param name="Configuration">The build configuration, Debug or Release.</param>
	/// <param name="Verbose">Whether to enable verbose logging.</param>
	/// <param name="DryRun">Whether to report what would happen without making changes.</param>
	/// <param name="VersionBump">The forced version bump type, or <c>auto</c> to detect it.</param>
	public sealed record ReleaseOptions(
		string Workspace,
		string Configuration,
		bool Verbose,
		bool DryRun,
		string VersionBump);

	/// <summary>
	/// Initializes a new instance of the <see cref="ReleaseCommand"/> class.
	/// </summary>
	public ReleaseCommand() : base("release", "Release workflow: pack, publish, release")
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
	public static Func<ReleaseOptions, CancellationToken, Task<int>> CreateHandler(
		IProcessRunner processRunner,
		IBuildLogger logger)
	{
		return async (options, cancellationToken) =>
		{
			Ensure.NotNull(options);
			logger.VerboseEnabled = options.Verbose;

			if (options.DryRun)
			{
				logger.WriteWarning("DRY RUN MODE - No changes will be made");
			}

			logger.WriteStepHeader("Starting Release Workflow");

			PipelineService pipeline = new(processRunner, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				PipelineContext context = await pipeline.PrepareAsync(options.Workspace, options.Configuration, cancellationToken).ConfigureAwait(false);
				BuildConfiguration buildConfig = context.Configuration;

				if (!buildConfig.ShouldRelease)
				{
					logger.WriteWarning("Not a release build (not on main, is tagged, or not official repo)");
					logger.WriteInfo($"Is Main: {buildConfig.IsMain}, Is Tagged: {buildConfig.IsTagged}, Is Official: {buildConfig.IsOfficial}");
					return 0;
				}

				if (options.DryRun)
				{
					logger.WriteInfo("Would pack, publish NuGet packages, and create GitHub release");
					return 0;
				}

				// `ci` releases the metadata commit, because UpdateMetadataAsync overwrites this before
				// resolution runs. A standalone release has no metadata stage, so it would otherwise
				// target GITHUB_SHA, the commit that triggered the run, which in a split pipeline is the
				// commit before the metadata commit. The tag would then point at a tree whose VERSION.md
				// predates the version being published. Setting it here, before resolution, gives this
				// path the same relationship between the release target and the analysis boundary that
				// `ci` has.
				GitService gitService = new(processRunner, logger);
				buildConfig.ReleaseHash = await gitService.GetCurrentCommitHashAsync(buildConfig.WorkspacePath, cancellationToken).ConfigureAwait(false);

				// The bump has to match the one ci --no-release was given in the same run. ci wrote that
				// version to VERSION.md and committed it, and a forced bump also opens the version gate
				// over commits that all carry [skip ci]. Resolving as auto here would close the gate
				// again, leaving the forced version committed and never released.
				await pipeline.ResolveVersionAsync(context, options.VersionBump, cancellationToken).ConfigureAwait(false);

				// Resolving deliberately leaves Configuration.Version alone, because in ci it is
				// assigned from the metadata result instead. A standalone release has no metadata
				// result, so this is the caller that establishes the version itself. Without this
				// line the service refuses to publish rather than shipping the placeholder, which
				// is the defect this command exists to fix.
				pipeline.ApplyResolvedVersion(context);

				// The version gate is how [skip ci] and a run with no meaningful changes suppress
				// a release. A standalone release honors it the same way ci does, so without a forced
				// bump a run whose commits all carry the skip marker does not publish here either.
				// Nothing is suppressed by a flag, because this command has no --no-release.
				await pipeline.ReleaseIfPermittedAsync(context, suppressedByFlag: false, cancellationToken).ConfigureAwait(false);

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
