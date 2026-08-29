// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Profile;

/// <summary>
/// Everything the profile renderer needs to know about one repository.
/// </summary>
/// <remarks>
/// Gathering and rendering are kept apart on purpose. This record is the seam, so the renderer can be
/// tested against handwritten facts without touching the network.
/// </remarks>
public sealed record RepoFacts
{
	/// <summary>Gets the organization or user that owns the repository.</summary>
	public required string Owner { get; init; }

	/// <summary>Gets the repository name.</summary>
	public required string Name { get; init; }

	/// <summary>Gets what the repository ships, in a stable order.</summary>
	public IReadOnlyList<ShippedVariant> Variants { get; init; } = [];

	/// <summary>Gets the newest stable version published to NuGet, or <see langword="null"/> when the
	/// package is unpublished or has only prereleases.</summary>
	public string? NuGetStableVersion { get; init; }

	/// <summary>Gets the newest stable release version from GitHub, without its leading <c>v</c>.</summary>
	public string? ReleaseStableVersion { get; init; }

	/// <summary>Gets the ktsu SDK version the repository pins in its <c>global.json</c>, or
	/// <see langword="null"/> when it pins none.</summary>
	public string? SdkVersion { get; init; }

	/// <summary>Gets a value indicating whether <see cref="SdkVersion"/> is at least as new as the
	/// newest published SDK. A repository left behind is the thing this column exists to surface.</summary>
	public bool SdkIsCurrent { get; init; }

	/// <summary>Gets the number of people who have starred the repository.</summary>
	public int Stars { get; init; }

	/// <summary>Gets the number of commits pushed in the trailing activity window.</summary>
	public int CommitActivity { get; init; }

	/// <summary>Gets the conclusion of the most recent build workflow run, such as <c>success</c>, or
	/// <see langword="null"/> when no run was found.</summary>
	public string? WorkflowConclusion { get; init; }

	/// <summary>Gets a value indicating whether a build workflow run was found at all. Distinguishes a
	/// repository with no CI from one whose run is still in progress.</summary>
	public bool HasWorkflowRun { get; init; }
}
