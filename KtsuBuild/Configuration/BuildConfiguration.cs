// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Configuration;

/// <summary>
/// Represents the complete build configuration.
/// </summary>
public class BuildConfiguration
{
	/// <summary>
	/// Gets or sets whether this is an official repository (not a fork and owned by expected owner).
	/// </summary>
	public bool IsOfficial { get; set; }

	/// <summary>
	/// Gets or sets whether this is the main branch.
	/// </summary>
	public bool IsMain { get; set; }

	/// <summary>
	/// Gets or sets whether the current commit is already tagged.
	/// </summary>
	public bool IsTagged { get; set; }

	/// <summary>
	/// Gets or sets whether a release should be created.
	/// </summary>
	public bool ShouldRelease { get; set; }

	/// <summary>
	/// Gets or sets whether dotnet-script is needed.
	/// </summary>
	public bool UseDotnetScript { get; set; }

	/// <summary>
	/// Gets or sets the output path for published applications.
	/// </summary>
	public string OutputPath { get; set; } = "output";

	/// <summary>
	/// Gets or sets the staging path for packages.
	/// </summary>
	public string StagingPath { get; set; } = "staging";

	/// <summary>
	/// Gets or sets the package pattern for NuGet packages.
	/// </summary>
	public string PackagePattern { get; set; } = "staging/*.nupkg";

	/// <summary>
	/// Gets or sets the symbols pattern for symbol packages.
	/// </summary>
	public string SymbolsPattern { get; set; } = "staging/*.snupkg";

	/// <summary>
	/// Gets or sets the application pattern for published apps.
	/// </summary>
	public string ApplicationPattern { get; set; } = "staging/*.zip";

	/// <summary>
	/// Gets or sets additional build arguments.
	/// </summary>
	public string BuildArgs { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the workspace path.
	/// </summary>
	public string WorkspacePath { get; set; } = ".";

	/// <summary>
	/// Gets or sets the GitHub server URL.
	/// </summary>
	public string ServerUrl { get; set; } = "https://github.com";

	/// <summary>
	/// Gets or sets the Git reference.
	/// </summary>
	public string GitRef { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the Git commit SHA.
	/// </summary>
	public string GitSha { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the GitHub owner/organization.
	/// </summary>
	public string GitHubOwner { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the GitHub repository name.
	/// </summary>
	public string GitHubRepo { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the GitHub token.
	/// </summary>
	public string GithubToken { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the NuGet API key.
	/// </summary>
	public string NuGetApiKey { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the Ktsu package key.
	/// </summary>
	public string KtsuPackageKey { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the expected owner for official builds.
	/// </summary>
	public string ExpectedOwner { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the current version.
	/// </summary>
	public string Version { get; set; } = "1.0.0-pre.0";

	/// <summary>
	/// Gets or sets the release commit hash.
	/// </summary>
	public string ReleaseHash { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the changelog file path.
	/// </summary>
	public string ChangelogFile { get; set; } = "CHANGELOG.md";

	/// <summary>
	/// Gets or sets the latest changelog file path.
	/// </summary>
	public string LatestChangelogFile { get; set; } = "LATEST_CHANGELOG.md";

	/// <summary>
	/// Gets or sets the asset patterns for release.
	/// </summary>
	public IReadOnlyList<string> AssetPatterns { get; set; } = [];

	/// <summary>
	/// Gets or sets the build configuration name (Debug/Release).
	/// </summary>
	public string Configuration { get; set; } = "Release";
}
