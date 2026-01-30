// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Configuration;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Options for creating a build configuration.
/// </summary>
public class BuildConfigurationOptions
{
	/// <summary>
	/// Gets or sets the GitHub server URL.
	/// </summary>
	[SuppressMessage("Design", "CA1056:URI properties should not be strings", Justification = "URL is used for string concatenation throughout the codebase")]
	public string ServerUrl { get; set; } = "https://github.com";

	/// <summary>
	/// Gets or sets the Git reference (branch/tag).
	/// </summary>
	public required string GitRef { get; set; }

	/// <summary>
	/// Gets or sets the Git commit SHA.
	/// </summary>
	public required string GitSha { get; set; }

	/// <summary>
	/// Gets or sets the GitHub owner/organization.
	/// </summary>
	public required string GitHubOwner { get; set; }

	/// <summary>
	/// Gets or sets the GitHub repository name.
	/// </summary>
	public required string GitHubRepo { get; set; }

	/// <summary>
	/// Gets or sets the GitHub token.
	/// </summary>
	public required string GithubToken { get; set; }

	/// <summary>
	/// Gets or sets the NuGet API key (optional).
	/// </summary>
	public string NuGetApiKey { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the Ktsu package key (optional).
	/// </summary>
	public string KtsuPackageKey { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the workspace path.
	/// </summary>
	public required string WorkspacePath { get; set; }

	/// <summary>
	/// Gets or sets the expected owner for official builds.
	/// </summary>
	public required string ExpectedOwner { get; set; }

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
