// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tool.Commands;

using System.CommandLine;
using KtsuBuild.Abstractions;
using KtsuBuild.Configuration;
using KtsuBuild.Pipeline;
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

	/// <summary>
	/// Orders the pipeline stages and applies the flags that gate them. Each stage itself lives in
	/// <see cref="PipelineService"/>, so <c>ci</c>, <c>release</c> and <c>build</c> run the same
	/// code rather than three copies that drift apart.
	/// </summary>
	private static async Task<int> ExecutePipelineAsync(
		IProcessRunner processRunner,
		IBuildLogger logger,
		CiOptions options,
		CancellationToken cancellationToken)
	{
		PipelineService pipeline = new(processRunner, logger);

		PipelineContext context = await pipeline.PrepareAsync(options.Workspace, options.Configuration, options.VersionBump, cancellationToken).ConfigureAwait(false);

		if (options.DryRun)
		{
			logger.WriteInfo("Would update metadata, build, test, and create release");
			return 0;
		}

		await pipeline.UpdateMetadataAsync(context, cancellationToken).ConfigureAwait(false);

		await pipeline.RestoreAndBuildAsync(options.Workspace, options.Configuration, context.Configuration.BuildArgs, cancellationToken).ConfigureAwait(false);

		// A caller that runs the tests elsewhere, such as a workflow that fans them across a
		// matrix, still needs everything around them: metadata, the version gate, a compilation
		// inside the SonarQube begin and end window, and the step outputs.
		if (!options.NoTest)
		{
			await pipeline.RunTestsAsync(options.Workspace, options.Configuration, cancellationToken).ConfigureAwait(false);
		}

		// iOS validation: when the workspace contains an iOS head, build it unsigned
		// for the simulator and device runtimes as part of CI, the same pull-request
		// validation `ios build` performs. Running it automatically means a consumer
		// with an iOS head does not need a separate invocation. iOS builds only on
		// macOS, so on any other host the step reports the detected heads and skips.
		if (!await pipeline.ValidateIosAsync(options.Workspace, options.Configuration, cancellationToken).ConfigureAwait(false))
		{
			return 1;
		}

		// Release workflow
		if (CiReleaseDecision.ShouldExecuteRelease(context.Configuration.ShouldRelease, context.ReleaseSuppressedByVersionGate, suppressedByFlag: options.NoRelease))
		{
			await pipeline.ReleaseAsync(context, cancellationToken).ConfigureAwait(false);
		}

		pipeline.WriteStepOutputs(context);

		logger.WriteSuccess("CI/CD pipeline completed successfully!");
		return 0;
	}
}
