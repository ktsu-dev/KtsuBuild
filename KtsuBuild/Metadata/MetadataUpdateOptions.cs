// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Metadata;

using KtsuBuild.Configuration;
using KtsuBuild.Git;

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

	/// <summary>
	/// Gets or sets the version increment to use instead of the one detected from the commits, or
	/// <see langword="null"/> to detect it.
	/// </summary>
	/// <remarks>
	/// The metadata stage is where <c>ci</c> decides the version it writes to <c>VERSION.md</c>, packs
	/// and tags, so a forced <c>--version-bump</c> has to reach it here. Applying it only to the
	/// version gate would publish the detected version under a forced run.
	/// </remarks>
	public VersionType? ForcedVersionType { get; set; }
}
