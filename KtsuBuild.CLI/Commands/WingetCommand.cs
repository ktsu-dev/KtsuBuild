// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.CLI.Commands;

using System.CommandLine;

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
