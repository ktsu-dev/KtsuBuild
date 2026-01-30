// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

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
