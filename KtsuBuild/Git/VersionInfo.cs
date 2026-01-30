// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Git;

/// <summary>
/// Represents comprehensive version information derived from Git history.
/// </summary>
public class VersionInfo
{
	/// <summary>
	/// Gets or sets the calculated version string.
	/// </summary>
	public string Version { get; set; } = "1.0.0";

	/// <summary>
	/// Gets or sets the major version component.
	/// </summary>
	public int Major { get; set; }

	/// <summary>
	/// Gets or sets the minor version component.
	/// </summary>
	public int Minor { get; set; }

	/// <summary>
	/// Gets or sets the patch version component.
	/// </summary>
	public int Patch { get; set; }

	/// <summary>
	/// Gets or sets whether this is a prerelease version.
	/// </summary>
	public bool IsPrerelease { get; set; }

	/// <summary>
	/// Gets or sets the prerelease number.
	/// </summary>
	public int PrereleaseNumber { get; set; }

	/// <summary>
	/// Gets or sets the prerelease label (e.g., "pre", "alpha", "beta", "rc").
	/// </summary>
	public string PrereleaseLabel { get; set; } = "pre";

	/// <summary>
	/// Gets or sets the last tag found.
	/// </summary>
	public string LastTag { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the last version string.
	/// </summary>
	public string LastVersion { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets whether the last version was a prerelease.
	/// </summary>
	public bool WasPrerelease { get; set; }

	/// <summary>
	/// Gets or sets the version increment type.
	/// </summary>
	public VersionType VersionIncrement { get; set; }

	/// <summary>
	/// Gets or sets the reason for the version increment decision.
	/// </summary>
	public string IncrementReason { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the first commit hash in the repository.
	/// </summary>
	public string FirstCommit { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the current commit hash.
	/// </summary>
	public string LastCommit { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the commit hash of the last tag.
	/// </summary>
	public string LastTagCommit { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets whether a fallback tag was used.
	/// </summary>
	public bool UsingFallbackTag { get; set; }

	/// <summary>
	/// Gets or sets the commit range analyzed.
	/// </summary>
	public string CommitRange { get; set; } = string.Empty;
}
