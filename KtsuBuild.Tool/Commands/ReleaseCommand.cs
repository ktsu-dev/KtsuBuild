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

				await pipeline.ResolveVersionAsync(context, "auto", cancellationToken).ConfigureAwait(false);

				// Resolving deliberately leaves Configuration.Version alone, because in ci it is
				// assigned from the metadata result instead. A standalone release has no metadata
				// result, so this is the caller that establishes the version itself. Without this
				// line the service refuses to publish rather than shipping the placeholder, which
				// is the defect this command exists to fix.
				pipeline.ApplyResolvedVersion(context);

				// The version gate is how [skip ci] and a run with no meaningful changes suppress
				// a release. A standalone release honors it the same way ci does, so a run whose
				// commits all carry the skip marker does not publish here either. Nothing is
				// suppressed by a flag, because this command has no --no-release.
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
