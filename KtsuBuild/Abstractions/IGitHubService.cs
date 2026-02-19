// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Abstractions;

using KtsuBuild.Publishing;

/// <summary>
/// Interface for GitHub operations.
/// </summary>
public interface IGitHubService
{
	/// <summary>
	/// Creates a GitHub release.
	/// </summary>
	/// <param name="options">The release options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task CreateReleaseAsync(ReleaseOptions options, CancellationToken cancellationToken = default);

	/// <summary>
	/// Uploads assets to an existing release.
	/// </summary>
	/// <param name="version">The release version/tag.</param>
	/// <param name="assetPaths">The paths to asset files.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task UploadReleaseAssetsAsync(string version, IEnumerable<string> assetPaths, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets repository information.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>Repository info including owner, name, and fork status.</returns>
	public Task<RepositoryInfo?> GetRepositoryInfoAsync(string workingDirectory, CancellationToken cancellationToken = default);

	/// <summary>
	/// Sets the repository topics on GitHub.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="topics">The topics to set.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task SetRepositoryTopicsAsync(string workingDirectory, IReadOnlyList<string> topics, CancellationToken cancellationToken = default);

	/// <summary>
	/// Checks if the repository is official (not a fork and owned by expected owner).
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="expectedOwner">The expected owner/organization.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>True if this is the official repository.</returns>
	public Task<bool> IsOfficialRepositoryAsync(string workingDirectory, string expectedOwner, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents repository information from GitHub.
/// </summary>
public record RepositoryInfo(string Owner, string Name, bool IsFork);
