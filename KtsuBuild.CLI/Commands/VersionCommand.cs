// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.CLI.Commands;

using System.CommandLine;
using KtsuBuild.Abstractions;
using KtsuBuild.Git;

/// <summary>
/// Version command for version management operations.
/// </summary>
#pragma warning disable CA1010 // System.CommandLine.Command implements IEnumerable for collection initializer support
public class VersionCommand : Command
#pragma warning restore CA1010
{
	/// <summary>
	/// Initializes a new instance of the <see cref="VersionCommand"/> class.
	/// </summary>
	public VersionCommand() : base("version", "Version management")
	{
		Subcommands.Add(new ShowCommand());
		Subcommands.Add(new BumpCommand());
		Subcommands.Add(new CreateCommand());
	}

#pragma warning disable CA1010
	private sealed class ShowCommand : Command
#pragma warning restore CA1010
	{
		public ShowCommand() : base("show", "Show current version info")
		{
			Options.Add(GlobalOptions.Workspace);
			Options.Add(GlobalOptions.Verbose);
		}

		public static Func<string, bool, CancellationToken, Task<int>> CreateHandler(
			IProcessRunner processRunner,
			IBuildLogger logger)
		{
			return async (workspace, verbose, cancellationToken) =>
			{
				logger.VerboseEnabled = verbose;

				GitService gitService = new(processRunner, logger);
				VersionCalculator versionCalculator = new(gitService, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
				try
				{
					string commitHash = await gitService.GetCurrentCommitHashAsync(workspace, cancellationToken).ConfigureAwait(false);
					VersionInfo versionInfo = await versionCalculator.GetVersionInfoAsync(workspace, commitHash, cancellationToken: cancellationToken).ConfigureAwait(false);

					Console.WriteLine($"Current Version: {versionInfo.Version}");
					Console.WriteLine($"Last Tag: {versionInfo.LastTag}");
					Console.WriteLine($"Last Version: {versionInfo.LastVersion}");
					Console.WriteLine($"Version Increment: {versionInfo.VersionIncrement}");
					Console.WriteLine($"Reason: {versionInfo.IncrementReason}");
					Console.WriteLine($"Is Prerelease: {versionInfo.IsPrerelease}");

					return 0;
				}
				catch (Exception ex)
				{
					logger.WriteError($"Failed to get version info: {ex.Message}");
					return 1;
				}
#pragma warning restore CA1031
			};
		}
	}

#pragma warning disable CA1010
	private sealed class BumpCommand : Command
#pragma warning restore CA1010
	{
		public BumpCommand() : base("bump", "Calculate next version")
		{
			Options.Add(GlobalOptions.Workspace);
			Options.Add(GlobalOptions.Verbose);
		}

		public static Func<string, bool, CancellationToken, Task<int>> CreateHandler(
			IProcessRunner processRunner,
			IBuildLogger logger)
		{
			return async (workspace, verbose, cancellationToken) =>
			{
				logger.VerboseEnabled = verbose;

				GitService gitService = new(processRunner, logger);
				VersionCalculator versionCalculator = new(gitService, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
				try
				{
					string commitHash = await gitService.GetCurrentCommitHashAsync(workspace, cancellationToken).ConfigureAwait(false);
					VersionInfo versionInfo = await versionCalculator.GetVersionInfoAsync(workspace, commitHash, cancellationToken: cancellationToken).ConfigureAwait(false);

					Console.WriteLine(versionInfo.Version);
					return 0;
				}
				catch (Exception ex)
				{
					logger.WriteError($"Failed to calculate version: {ex.Message}");
					return 1;
				}
#pragma warning restore CA1031
			};
		}
	}

#pragma warning disable CA1010
	private sealed class CreateCommand : Command
#pragma warning restore CA1010
	{
		public CreateCommand() : base("create", "Create VERSION.md")
		{
			Options.Add(GlobalOptions.Workspace);
			Options.Add(GlobalOptions.Verbose);
		}

		public static Func<string, bool, CancellationToken, Task<int>> CreateHandler(
			IProcessRunner processRunner,
			IBuildLogger logger)
		{
			return async (workspace, verbose, cancellationToken) =>
			{
				logger.VerboseEnabled = verbose;

				GitService gitService = new(processRunner, logger);
				VersionCalculator versionCalculator = new(gitService, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
				try
				{
					string commitHash = await gitService.GetCurrentCommitHashAsync(workspace, cancellationToken).ConfigureAwait(false);
					VersionInfo versionInfo = await versionCalculator.GetVersionInfoAsync(workspace, commitHash, cancellationToken: cancellationToken).ConfigureAwait(false);
					string lineEnding = await gitService.GetLineEndingAsync(workspace, cancellationToken).ConfigureAwait(false);

					await KtsuBuild.Metadata.VersionFileWriter.WriteAsync(versionInfo.Version, workspace, lineEnding, cancellationToken).ConfigureAwait(false);

					logger.WriteSuccess($"Created VERSION.md with version {versionInfo.Version}");
					return 0;
				}
				catch (Exception ex)
				{
					logger.WriteError($"Failed to create VERSION.md: {ex.Message}");
					return 1;
				}
#pragma warning restore CA1031
			};
		}
	}
}
