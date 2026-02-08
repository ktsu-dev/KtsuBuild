// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.CLI.Commands;

using System.CommandLine;
using KtsuBuild.Abstractions;
using KtsuBuild.Winget;

/// <summary>
/// Winget command for manifest operations.
/// </summary>
public class WingetCommand : Command
{
	/// <summary>
	/// Initializes a new instance of the <see cref="WingetCommand"/> class.
	/// </summary>
	public WingetCommand() : base("winget", "Winget manifest commands")
	{
		Subcommands.Add(new GenerateCommand());
		Subcommands.Add(new UploadCommand());
	}

	private sealed class GenerateCommand : Command
	{
		private static readonly Option<string> VersionOption = new(
			["--version", "-V"],
			"The version to generate manifests for")
		{ IsRequired = true };

		private static readonly Option<string?> GitHubRepoOption = new(
			["--repo", "-r"],
			"The GitHub repository (owner/repo)");

		private static readonly Option<string?> PackageIdOption = new(
			["--package-id", "-p"],
			"The package identifier");

		private static readonly Option<string?> StagingOption = new(
			["--staging", "-s"],
			"The staging directory with hashes.txt");

		public GenerateCommand() : base("generate", "Generate manifests for a version")
		{
			Options.Add(GlobalOptions.Workspace);
			Options.Add(GlobalOptions.Verbose);
			Options.Add(VersionOption);
			Options.Add(GitHubRepoOption);
			Options.Add(PackageIdOption);
			Options.Add(StagingOption);
		}

		public static Func<string, bool, string, string?, string?, string?, CancellationToken, Task<int>> CreateHandler(
			IProcessRunner processRunner,
			IBuildLogger logger)
		{
			return async (workspace, verbose, version, gitHubRepo, packageId, staging, cancellationToken) =>
			{
				logger.VerboseEnabled = verbose;
				logger.WriteStepHeader("Generating Winget Manifests");

				var wingetService = new WingetService(processRunner, logger);

				try
				{
					var options = new WingetOptions
					{
						Version = version,
						GitHubRepo = gitHubRepo,
						PackageId = packageId,
						RootDirectory = workspace,
						OutputDirectory = Path.Combine(workspace, "winget"),
						StagingDirectory = staging ?? Path.Combine(workspace, "staging"),
					};

					var result = await wingetService.GenerateManifestsAsync(options, cancellationToken).ConfigureAwait(false);

					if (result.IsLibraryOnly)
					{
						logger.WriteInfo("Library-only project - no manifests generated");
						return 0;
					}

					if (result.Success)
					{
						logger.WriteSuccess($"Generated manifests for {result.PackageId}");
						return 0;
					}
					else
					{
						logger.WriteError($"Failed to generate manifests: {result.Error}");
						return 1;
					}
				}
				catch (Exception ex)
				{
					logger.WriteError($"Failed to generate manifests: {ex.Message}");
					return 1;
				}
			};
		}
	}

	private sealed class UploadCommand : Command
	{
		private static readonly Option<string> VersionOption = new(
			["--version", "-V"],
			"The version to upload manifests for")
		{ IsRequired = true };

		public UploadCommand() : base("upload", "Upload manifests to GitHub release")
		{
			Options.Add(GlobalOptions.Workspace);
			Options.Add(GlobalOptions.Verbose);
			Options.Add(VersionOption);
		}

		public static Func<string, bool, string, CancellationToken, Task<int>> CreateHandler(
			IProcessRunner processRunner,
			IBuildLogger logger)
		{
			return async (workspace, verbose, version, cancellationToken) =>
			{
				logger.VerboseEnabled = verbose;

				var wingetService = new WingetService(processRunner, logger);

				try
				{
					string manifestDir = Path.Combine(workspace, "winget");
					await wingetService.UploadManifestsAsync(version, manifestDir, cancellationToken).ConfigureAwait(false);

					logger.WriteSuccess("Manifests uploaded!");
					return 0;
				}
				catch (Exception ex)
				{
					logger.WriteError($"Failed to upload manifests: {ex.Message}");
					return 1;
				}
			};
		}
	}
}
