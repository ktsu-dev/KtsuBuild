// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Abstractions;

/// <summary>
/// Read-only access to the GitHub REST API for the data the organization profile generator needs.
/// </summary>
/// <remarks>
/// Kept separate from <see cref="IGitHubService"/>, which writes to a single repository the build is
/// running in. This interface reads across every repository in an organization and never mutates.
/// </remarks>
public interface IGitHubApiClient
{
	/// <summary>
	/// Lists the public repositories in an organization, ordered by full name ascending.
	/// </summary>
	/// <param name="organization">The organization login.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>Every public repository, including archived ones.</returns>
	public Task<IReadOnlyList<GitHubRepository>> ListOrganizationRepositoriesAsync(string organization, CancellationToken cancellationToken = default);

	/// <summary>
	/// Lists the releases for a repository, newest first.
	/// </summary>
	/// <param name="organization">The repository owner.</param>
	/// <param name="repository">The repository name.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The releases, or an empty list when the repository has none.</returns>
	public Task<IReadOnlyList<GitHubRelease>> ListReleasesAsync(string organization, string repository, CancellationToken cancellationToken = default);

	/// <summary>
	/// Lists every file path in a branch through a single recursive tree request.
	/// </summary>
	/// <param name="organization">The repository owner.</param>
	/// <param name="repository">The repository name.</param>
	/// <param name="branch">The branch to read.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The blob paths in the tree. Empty when the tree cannot be read.</returns>
	/// <remarks>
	/// GitHub truncates trees above roughly 100,000 entries. A truncated tree is returned as far as it
	/// was read, so callers that need completeness should treat a large result as suspect.
	/// </remarks>
	public Task<IReadOnlyList<string>> ListTreePathsAsync(string organization, string repository, string branch, CancellationToken cancellationToken = default);

	/// <summary>
	/// Reads a text file from the default branch of a repository.
	/// </summary>
	/// <param name="organization">The repository owner.</param>
	/// <param name="repository">The repository name.</param>
	/// <param name="path">The path within the repository.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The decoded file content, or <see langword="null"/> when the file does not exist.</returns>
	public Task<string?> GetFileTextAsync(string organization, string repository, string path, CancellationToken cancellationToken = default);

	/// <summary>
	/// Counts the commits pushed to a repository since a point in time.
	/// </summary>
	/// <param name="organization">The repository owner.</param>
	/// <param name="repository">The repository name.</param>
	/// <param name="since">The earliest commit timestamp to count.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The commit count, capped at one page of 100.</returns>
	public Task<int> CountCommitsSinceAsync(string organization, string repository, DateTimeOffset since, CancellationToken cancellationToken = default);

	/// <summary>
	/// Lists the file names of the active workflows in a repository.
	/// </summary>
	/// <param name="organization">The repository owner.</param>
	/// <param name="repository">The repository name.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The workflow file names, without their directory prefix.</returns>
	public Task<IReadOnlyList<string>> ListActiveWorkflowFileNamesAsync(string organization, string repository, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the most recent run of a workflow on a branch.
	/// </summary>
	/// <param name="organization">The repository owner.</param>
	/// <param name="repository">The repository name.</param>
	/// <param name="workflowFileName">The workflow file name, such as <c>dotnet.yml</c>.</param>
	/// <param name="branch">The branch to filter runs by. Without it the API returns runs from any branch,
	/// so a failing feature branch would misreport the repository's status.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The latest run, or <see langword="null"/> when the workflow has never run on that branch.</returns>
	public Task<GitHubWorkflowRun?> GetLatestWorkflowRunAsync(string organization, string repository, string workflowFileName, string branch, CancellationToken cancellationToken = default);
}

/// <summary>
/// A repository as returned by the organization listing endpoint.
/// </summary>
/// <param name="Name">The repository name.</param>
/// <param name="DefaultBranch">The default branch name.</param>
/// <param name="IsArchived">Whether the repository is archived.</param>
/// <param name="Stars">The stargazer count, which the listing endpoint already returns.</param>
public record GitHubRepository(string Name, string DefaultBranch, bool IsArchived, int Stars = 0);

/// <summary>
/// A published release.
/// </summary>
/// <param name="TagName">The git tag the release points at, such as <c>v1.2.3</c>.</param>
public record GitHubRelease(string TagName);

/// <summary>
/// The outcome of a workflow run.
/// </summary>
/// <param name="Status">The run status, such as <c>completed</c>.</param>
/// <param name="Conclusion">The run conclusion, such as <c>success</c>, or <see langword="null"/> while in progress.</param>
public record GitHubWorkflowRun(string? Status, string? Conclusion);
