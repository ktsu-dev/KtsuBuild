// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.CLI.Commands;

using System.CommandLine;
using KtsuBuild.Abstractions;
using KtsuBuild.Configuration;
using KtsuBuild.Git;
using KtsuBuild.Metadata;
using KtsuBuild.Publishing;

/// <summary>
/// Metadata command for metadata file management.
/// </summary>
#pragma warning disable CA1010 // System.CommandLine.Command implements IEnumerable for collection initializer support
public class MetadataCommand : Command
#pragma warning restore CA1010
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MetadataCommand"/> class.
	/// </summary>
	public MetadataCommand() : base("metadata", "Metadata file management")
	{
		Subcommands.Add(new UpdateCommand());
		Subcommands.Add(new LicenseCommand());
		Subcommands.Add(new ChangelogCommand());
	}

#pragma warning disable CA1010
	private sealed class UpdateCommand : Command
#pragma warning restore CA1010
	{
		public UpdateCommand() : base("update", "Update all metadata files")
		{
			Options.Add(GlobalOptions.Workspace);
			Options.Add(GlobalOptions.Verbose);
			Options.Add(new Option<bool>("--no-commit", "Don't commit changes"));
		}

		public static Func<string, bool, bool, CancellationToken, Task<int>> CreateHandler(
			IProcessRunner processRunner,
			IBuildLogger logger)
		{
			return async (workspace, verbose, noCommit, cancellationToken) =>
			{
				logger.VerboseEnabled = verbose;
				logger.WriteStepHeader("Updating Metadata Files");

				GitService gitService = new(processRunner, logger);
				GitHubService gitHubService = new(processRunner, gitService, logger);
				BuildConfigurationProvider configProvider = new(gitService, gitHubService);
				MetadataService metadataService = new(gitService, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
				try
				{
					BuildConfiguration buildConfig = await configProvider.CreateFromEnvironmentAsync(workspace, cancellationToken).ConfigureAwait(false);

					MetadataUpdateResult result = await metadataService.UpdateAllAsync(new MetadataUpdateOptions
					{
						BuildConfiguration = buildConfig,
						CommitChanges = !noCommit,
					}, cancellationToken).ConfigureAwait(false);

					if (result.Success)
					{
						logger.WriteSuccess($"Metadata updated successfully! Version: {result.Version}");
						return 0;
					}
					else
					{
						logger.WriteError($"Metadata update failed: {result.Error}");
						return 1;
					}
				}
				catch (Exception ex)
				{
					logger.WriteError($"Failed to update metadata: {ex.Message}");
					return 1;
				}
#pragma warning restore CA1031
			};
		}
	}

#pragma warning disable CA1010
	private sealed class LicenseCommand : Command
#pragma warning restore CA1010
	{
		public LicenseCommand() : base("license", "Generate LICENSE.md and COPYRIGHT.md")
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
				GitHubService gitHubService = new(processRunner, gitService, logger);
				BuildConfigurationProvider configProvider = new(gitService, gitHubService);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
				try
				{
					BuildConfiguration buildConfig = await configProvider.CreateFromEnvironmentAsync(workspace, cancellationToken).ConfigureAwait(false);
					string lineEnding = await gitService.GetLineEndingAsync(workspace, cancellationToken).ConfigureAwait(false);

					await LicenseGenerator.GenerateAsync(
						buildConfig.ServerUrl,
						buildConfig.GitHubOwner,
						buildConfig.GitHubRepo,
						workspace,
						lineEnding,
						cancellationToken).ConfigureAwait(false);

					logger.WriteSuccess("License files generated!");
					return 0;
				}
				catch (Exception ex)
				{
					logger.WriteError($"Failed to generate license: {ex.Message}");
					return 1;
				}
#pragma warning restore CA1031
			};
		}
	}

#pragma warning disable CA1010
	private sealed class ChangelogCommand : Command
#pragma warning restore CA1010
	{
		public ChangelogCommand() : base("changelog", "Generate CHANGELOG.md")
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
				ChangelogGenerator changelogGenerator = new(gitService, logger);
				VersionCalculator versionCalculator = new(gitService, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
				try
				{
					string commitHash = await gitService.GetCurrentCommitHashAsync(workspace, cancellationToken).ConfigureAwait(false);
					VersionInfo versionInfo = await versionCalculator.GetVersionInfoAsync(workspace, commitHash, cancellationToken: cancellationToken).ConfigureAwait(false);
					string lineEnding = await gitService.GetLineEndingAsync(workspace, cancellationToken).ConfigureAwait(false);

					await changelogGenerator.GenerateAsync(
						versionInfo.Version,
						commitHash,
						workspace,
						workspace,
						lineEnding,
						cancellationToken: cancellationToken).ConfigureAwait(false);

					logger.WriteSuccess("Changelog generated!");
					return 0;
				}
				catch (Exception ex)
				{
					logger.WriteError($"Failed to generate changelog: {ex.Message}");
					return 1;
				}
#pragma warning restore CA1031
			};
		}
	}
}
