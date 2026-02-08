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
#pragma warning disable CA1010 // System.CommandLine.Command implements IEnumerable for collection initializer support
public class WingetCommand : Command
#pragma warning restore CA1010
{
	/// <summary>
	/// Initializes a new instance of the <see cref="WingetCommand"/> class.
	/// </summary>
	public WingetCommand() : base("winget", "Winget manifest commands")
	{
		Subcommands.Add(new GenerateCommand());
		Subcommands.Add(new UploadCommand());
	}

#pragma warning disable CA1010
	private sealed class GenerateCommand : Command
#pragma warning restore CA1010
	{
		private static readonly Option<string> VersionOption = new(
			"--version", "-V")
		{
			Description = "The version to generate manifests for",
			Required = true,
		};

		private static readonly Option<string?> GitHubRepoOption = new(
			"--repo", "-r")
		{
			Description = "The GitHub repository (owner/repo)",
		};

		private static readonly Option<string?> PackageIdOption = new(
			"--package-id", "-p")
		{
			Description = "The package identifier",
		};

		private static readonly Option<string?> StagingOption = new(
			"--staging", "-s")
		{
			Description = "The staging directory with hashes.txt",
		};

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

				WingetService wingetService = new(processRunner, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
				try
				{
					WingetOptions options = new()
					{
						Version = version,
						GitHubRepo = gitHubRepo,
						PackageId = packageId,
						RootDirectory = workspace,
						OutputDirectory = Path.Combine(workspace, "winget"),
						StagingDirectory = staging ?? Path.Combine(workspace, "staging"),
					};

					WingetManifestResult result = await wingetService.GenerateManifestsAsync(options, cancellationToken).ConfigureAwait(false);

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
#pragma warning restore CA1031
			};
		}
	}

#pragma warning disable CA1010
	private sealed class UploadCommand : Command
#pragma warning restore CA1010
	{
		private static readonly Option<string> VersionOption = new(
			"--version", "-V")
		{
			Description = "The version to upload manifests for",
			Required = true,
		};

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

				WingetService wingetService = new(processRunner, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
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
#pragma warning restore CA1031
			};
		}
	}
}
