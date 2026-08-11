// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Git;

/// <summary>
/// Represents the type of version increment.
/// </summary>
public enum VersionType
{
	/// <summary>
	/// No version bump needed (skip release).
	/// </summary>
	Skip,

	/// <summary>
	/// Prerelease version bump.
	/// </summary>
	Prerelease,

	/// <summary>
	/// Patch version bump.
	/// </summary>
	Patch,

	/// <summary>
	/// Minor version bump.
	/// </summary>
	Minor,

	/// <summary>
	/// Major version bump.
	/// </summary>
	Major,
}
