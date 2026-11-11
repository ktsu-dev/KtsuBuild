// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tool.Commands;

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

	/// <summary>
	/// Reports the outcome of <c>winget generate</c> and chooses its exit code.
	/// </summary>
	/// <param name="result">The result manifest generation returned.</param>
	/// <param name="logger">The logger to report the outcome to.</param>
	/// <returns>0 when the run succeeded or was skipped, 1 when it failed.</returns>
	/// <remarks>
	/// A successful run that wrote no manifests, because nothing executable was found to hash, is a skip
	/// rather than a failure. It used to be reported as "Generated manifests for " with an empty id
	/// (ktsu-dev/KtsuBuild#300).
	/// </remarks>
	internal static int ReportGenerateResult(WingetManifestResult result, IBuildLogger logger)
	{
		Ensure.NotNull(result);
		Ensure.NotNull(logger);

		if (result.IsLibraryOnly)
		{
			logger.WriteInfo("Library-only project - no manifests generated");
			return 0;
		}

		if (!result.Success)
		{
			logger.WriteError($"Failed to generate manifests: {result.Error}");
			return 1;
		}

		if (result.ManifestFiles.Count == 0)
		{
			logger.WriteWarning("No manifests generated");
			return 0;
		}

		logger.WriteSuccess($"Generated manifests for {result.PackageId}");
		return 0;
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
	}
}
