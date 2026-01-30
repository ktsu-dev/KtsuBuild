// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Publishing;

/// <summary>
/// Options for creating a GitHub release.
/// </summary>
public class ReleaseOptions
{
	/// <summary>
	/// Gets or sets the version tag (without 'v' prefix).
	/// </summary>
	public required string Version { get; set; }

	/// <summary>
	/// Gets or sets the commit hash to tag.
	/// </summary>
	public required string CommitHash { get; set; }

	/// <summary>
	/// Gets or sets the GitHub token.
	/// </summary>
	public required string GithubToken { get; set; }

	/// <summary>
	/// Gets or sets the path to the changelog file for release notes.
	/// </summary>
	public string? ChangelogFile { get; set; }

	/// <summary>
	/// Gets or sets the path to the latest changelog file.
	/// </summary>
	public string? LatestChangelogFile { get; set; }

	/// <summary>
	/// Gets or sets the asset file paths to upload.
	/// </summary>
	public IReadOnlyList<string> AssetPaths { get; set; } = [];

	/// <summary>
	/// Gets or sets whether to generate release notes automatically.
	/// </summary>
	public bool GenerateNotes { get; set; } = true;

	/// <summary>
	/// Gets or sets whether this is a prerelease.
	/// </summary>
	public bool IsPrerelease { get; set; }

	/// <summary>
	/// Gets or sets the working directory.
	/// </summary>
	public string WorkingDirectory { get; set; } = ".";
}
