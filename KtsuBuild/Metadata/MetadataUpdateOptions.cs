// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Metadata;

using KtsuBuild.Configuration;

/// <summary>
/// Options for updating project metadata.
/// </summary>
public class MetadataUpdateOptions
{
	/// <summary>
	/// Gets or sets the build configuration.
	/// </summary>
	public required BuildConfiguration BuildConfiguration { get; set; }

	/// <summary>
	/// Gets or sets the authors list for AUTHORS.md.
	/// </summary>
	public IReadOnlyList<string> Authors { get; set; } = [];

	/// <summary>
	/// Gets or sets the commit message for metadata updates.
	/// </summary>
	public string CommitMessage { get; set; } = "[bot][skip ci] Update Metadata";

	/// <summary>
	/// Gets or sets whether to commit and push changes.
	/// </summary>
	public bool CommitChanges { get; set; } = true;
}
