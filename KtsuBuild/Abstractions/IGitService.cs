// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Abstractions;

using KtsuBuild.Git;

/// <summary>
/// Interface for Git operations.
/// </summary>
public interface IGitService
{
	/// <summary>
	/// Gets all tags sorted by version in descending order.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A list of tags.</returns>
	public Task<IReadOnlyList<string>> GetTagsAsync(string workingDirectory, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the current commit hash.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The commit hash.</returns>
	public Task<string> GetCurrentCommitHashAsync(string workingDirectory, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the commit hash for a specific tag.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="tag">The tag name.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The commit hash, or null if not found.</returns>
	public Task<string?> GetTagCommitHashAsync(string workingDirectory, string tag, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the remote URL for the repository.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="remoteName">The remote name (default: origin).</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The remote URL.</returns>
	public Task<string?> GetRemoteUrlAsync(string workingDirectory, string remoteName = "origin", CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the configured line ending style.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The line ending string.</returns>
	public Task<string> GetLineEndingAsync(string workingDirectory, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets commit messages in a range.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="range">The commit range (e.g., "tag1..tag2").</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A list of commit messages.</returns>
	public Task<IReadOnlyList<string>> GetCommitMessagesAsync(string workingDirectory, string range, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets commits with details in a range.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="range">The commit range.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A list of commit info objects.</returns>
	public Task<IReadOnlyList<CommitInfo>> GetCommitsAsync(string workingDirectory, string range, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the diff for a range.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="range">The commit range.</param>
	/// <param name="pathSpec">Optional path specification (e.g., "*.cs").</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The diff output.</returns>
	public Task<string> GetDiffAsync(string workingDirectory, string range, string? pathSpec = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Checks if a commit is tagged.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="commitHash">The commit hash to check.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>True if the commit is tagged.</returns>
	public Task<bool> IsCommitTaggedAsync(string workingDirectory, string commitHash, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the first commit in the repository.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The first commit hash.</returns>
	public Task<string> GetFirstCommitAsync(string workingDirectory, CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates and pushes a tag.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="tagName">The tag name.</param>
	/// <param name="commitHash">The commit to tag.</param>
	/// <param name="message">The tag message.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task CreateAndPushTagAsync(string workingDirectory, string tagName, string commitHash, string message, CancellationToken cancellationToken = default);

	/// <summary>
	/// Stages files for commit.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="files">The files to stage.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task StageFilesAsync(string workingDirectory, IEnumerable<string> files, CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates a commit.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="message">The commit message.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The new commit hash.</returns>
	public Task<string> CommitAsync(string workingDirectory, string message, CancellationToken cancellationToken = default);

	/// <summary>
	/// Pushes to the remote.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task PushAsync(string workingDirectory, CancellationToken cancellationToken = default);

	/// <summary>
	/// Checks if there are uncommitted changes.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>True if there are uncommitted changes.</returns>
	public Task<bool> HasUncommittedChangesAsync(string workingDirectory, CancellationToken cancellationToken = default);

	/// <summary>
	/// Sets the git user identity for commits.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="name">The user name.</param>
	/// <param name="email">The user email.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task SetIdentityAsync(string workingDirectory, string name, string email, CancellationToken cancellationToken = default);
}
