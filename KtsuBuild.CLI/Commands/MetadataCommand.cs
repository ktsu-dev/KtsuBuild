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
public class MetadataCommand : Command
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MetadataCommand"/> class.
	/// </summary>
	public MetadataCommand() : base("metadata", "Metadata file management")
	{
		AddCommand(new UpdateCommand());
		AddCommand(new LicenseCommand());
		AddCommand(new ChangelogCommand());
	}

	private sealed class UpdateCommand : Command
	{
		public UpdateCommand() : base("update", "Update all metadata files")
		{
			AddOption(GlobalOptions.Workspace);
			AddOption(GlobalOptions.Verbose);
			AddOption(new Option<bool>("--no-commit", "Don't commit changes"));
		}

		public static Func<string, bool, bool, CancellationToken, Task<int>> CreateHandler(
			IProcessRunner processRunner,
			IBuildLogger logger)
		{
			return async (workspace, verbose, noCommit, cancellationToken) =>
			{
				logger.VerboseEnabled = verbose;
				logger.WriteStepHeader("Updating Metadata Files");

				var gitService = new GitService(processRunner, logger);
				var gitHubService = new GitHubService(processRunner, gitService, logger);
				var configProvider = new BuildConfigurationProvider(gitService, gitHubService, logger);
				var metadataService = new MetadataService(gitService, logger);

				try
				{
					var buildConfig = await configProvider.CreateFromEnvironmentAsync(workspace, cancellationToken).ConfigureAwait(false);

					var result = await metadataService.UpdateAllAsync(new MetadataUpdateOptions
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
			};
		}
	}

	private sealed class LicenseCommand : Command
	{
		public LicenseCommand() : base("license", "Generate LICENSE.md and COPYRIGHT.md")
		{
			AddOption(GlobalOptions.Workspace);
			AddOption(GlobalOptions.Verbose);
		}

		public static Func<string, bool, CancellationToken, Task<int>> CreateHandler(
			IProcessRunner processRunner,
			IBuildLogger logger)
		{
			return async (workspace, verbose, cancellationToken) =>
			{
				logger.VerboseEnabled = verbose;

				var gitService = new GitService(processRunner, logger);
				var gitHubService = new GitHubService(processRunner, gitService, logger);
				var configProvider = new BuildConfigurationProvider(gitService, gitHubService, logger);

				try
				{
					var buildConfig = await configProvider.CreateFromEnvironmentAsync(workspace, cancellationToken).ConfigureAwait(false);
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
			};
		}
	}

	private sealed class ChangelogCommand : Command
	{
		public ChangelogCommand() : base("changelog", "Generate CHANGELOG.md")
		{
			AddOption(GlobalOptions.Workspace);
			AddOption(GlobalOptions.Verbose);
		}

		public static Func<string, bool, CancellationToken, Task<int>> CreateHandler(
			IProcessRunner processRunner,
			IBuildLogger logger)
		{
			return async (workspace, verbose, cancellationToken) =>
			{
				logger.VerboseEnabled = verbose;

				var gitService = new GitService(processRunner, logger);
				var changelogGenerator = new ChangelogGenerator(gitService, logger);
				var versionCalculator = new VersionCalculator(gitService, logger);

				try
				{
					string commitHash = await gitService.GetCurrentCommitHashAsync(workspace, cancellationToken).ConfigureAwait(false);
					var versionInfo = await versionCalculator.GetVersionInfoAsync(workspace, commitHash, cancellationToken: cancellationToken).ConfigureAwait(false);
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
			};
		}
	}
}
