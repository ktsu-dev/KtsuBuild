// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Metadata;

/// <summary>
/// Result of a metadata update operation.
/// </summary>
public class MetadataUpdateResult
{
	/// <summary>
	/// Gets or sets whether the update was successful.
	/// </summary>
	public bool Success { get; set; }

	/// <summary>
	/// Gets or sets the error message if update failed.
	/// </summary>
	public string? Error { get; set; }

	/// <summary>
	/// Gets or sets the version that was written.
	/// </summary>
	public string Version { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the release commit hash.
	/// </summary>
	public string ReleaseHash { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets whether there were changes to commit.
	/// </summary>
	public bool HasChanges { get; set; }
}
