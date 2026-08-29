// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Profile;

using System.Globalization;
using KtsuBuild.Abstractions;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Gathers the facts behind an organization's profile README.
/// </summary>
/// <param name="gitHub">The GitHub API client.</param>
/// <param name="nuGet">The NuGet catalog client.</param>
/// <param name="logger">The build logger.</param>
public class OrgProfileService(IGitHubApiClient gitHub, INuGetCatalogClient nuGet, IBuildLogger logger)
{
	private const string WingetRepositoryOwner = "microsoft";
	private const string WingetRepositoryName = "winget-pkgs";

	/// <summary>
	/// Collects facts for every repository that belongs in the profile tables.
	/// </summary>
	/// <param name="options">The gathering settings.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The repositories to list, in organization listing order.</returns>
	/// <remarks>
	/// A repository is listed only once it has a stable release, so work in progress stays off the
	/// public profile.
	/// </remarks>
	public async Task<IReadOnlyList<RepoFacts>> GatherAsync(ProfileOptions options, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(options);

		IReadOnlyList<GitHubRepository> repositories = await gitHub
			.ListOrganizationRepositoriesAsync(options.Organization, cancellationToken)
			.ConfigureAwait(false);

		logger.WriteInfo($"Found {repositories.Count.ToString(CultureInfo.InvariantCulture)} public repositories in {options.Organization}");

		List<RepoFacts> facts = [];
		foreach (GitHubRepository repository in repositories)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (repository.IsArchived)
			{
				logger.WriteVerbose($"Skipping archived repo: {repository.Name}");
				continue;
			}

			if (options.OnlyRepositories.Count > 0 &&
				!options.OnlyRepositories.Contains(repository.Name, StringComparer.OrdinalIgnoreCase))
			{
				continue;
			}

			if (options.ExcludedRepositories.Contains(repository.Name, StringComparer.OrdinalIgnoreCase))
			{
				logger.WriteVerbose($"Skipping excluded repo: {repository.Name}");
				continue;
			}

			RepoFacts? repositoryFacts = await GatherRepositoryAsync(repository, options, cancellationToken).ConfigureAwait(false);
			if (repositoryFacts is not null)
			{
				facts.Add(repositoryFacts);
			}
		}

		logger.WriteInfo($"Listing {facts.Count(static f => f.IsApplication).ToString(CultureInfo.InvariantCulture)} applications and {facts.Count(static f => !f.IsApplication).ToString(CultureInfo.InvariantCulture)} libraries");

		return facts;
	}

	/// <summary>
	/// Collects facts for one repository.
	/// </summary>
	/// <param name="repository">The repository to inspect.</param>
	/// <param name="options">The gathering settings.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The facts, or <see langword="null"/> when the repository does not belong in the tables.</returns>
	private async Task<RepoFacts?> GatherRepositoryAsync(GitHubRepository repository, ProfileOptions options, CancellationToken cancellationToken)
	{
		logger.WriteInfo($"Processing {repository.Name}");

		IReadOnlyList<GitHubRelease> releases = await gitHub
			.ListReleasesAsync(options.Organization, repository.Name, cancellationToken)
			.ConfigureAwait(false);

		// A version is a prerelease when its tag carries a label, which is more reliable than the
		// prerelease flag on the release itself.
		string? stableVersion = StripTagPrefix(releases
			.FirstOrDefault(static release => !release.TagName.Contains('-', StringComparison.Ordinal))?
			.TagName);

		if (stableVersion is null)
		{
			logger.WriteVerbose($"  Skipping repo with no stable release: {repository.Name}");
			return null;
		}

		string? prereleaseVersion = StripTagPrefix(releases
			.FirstOrDefault(static release => release.TagName.Contains('-', StringComparison.Ordinal))?
			.TagName);

		NuGetPackageInfo? package = await nuGet
			.GetPackageAsync($"{options.PackagePrefix}.{repository.Name}", cancellationToken)
			.ConfigureAwait(false);

		bool isApplication = await DetectApplicationAsync(repository, options, cancellationToken).ConfigureAwait(false);

		int commitActivity = await gitHub
			.CountCommitsSinceAsync(options.Organization, repository.Name, DateTimeOffset.UtcNow.AddDays(-options.ActivityWindowDays), cancellationToken)
			.ConfigureAwait(false);

		GitHubWorkflowRun? run = await GetBuildStatusAsync(repository, options, cancellationToken).ConfigureAwait(false);

		string? readme = await gitHub
			.GetFileTextAsync(options.Organization, repository.Name, "README.md", cancellationToken)
			.ConfigureAwait(false);

		string? wingetVersion = isApplication
			? await GetWingetVersionAsync(repository.Name, options, cancellationToken).ConfigureAwait(false)
			: null;

		return new RepoFacts
		{
			Owner = options.Organization,
			Name = repository.Name,
			IsApplication = isApplication,
			NuGetStableVersion = package?.StableVersion,
			NuGetPrereleaseVersion = package?.PrereleaseVersion,
			ReleaseStableVersion = stableVersion,
			ReleasePrereleaseVersion = prereleaseVersion,
			WingetVersion = wingetVersion,
			CommitActivity = commitActivity,
			WorkflowConclusion = run?.Conclusion,
			HasWorkflowRun = run is not null,
			ReadmePasses = (readme?.Length ?? 0) >= options.MinimumReadmeLength,
		};
	}

	/// <summary>
	/// Determines whether a repository's primary deliverable is an application.
	/// </summary>
	/// <param name="repository">The repository to inspect.</param>
	/// <param name="options">The gathering settings.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns><see langword="true"/> when the primary project declares an application SDK.</returns>
	private async Task<bool> DetectApplicationAsync(GitHubRepository repository, ProfileOptions options, CancellationToken cancellationToken)
	{
		IReadOnlyList<string> paths = await gitHub
			.ListTreePathsAsync(options.Organization, repository.Name, repository.DefaultBranch, cancellationToken)
			.ConfigureAwait(false);

		List<string> projects = [.. paths.Where(static path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))];

		string? primary = ProjectClassifier.SelectPrimaryProject(repository.Name, projects);
		if (primary is null)
		{
			logger.WriteVerbose($"  No project found, treating {repository.Name} as a library");
			return false;
		}

		string? content = await gitHub
			.GetFileTextAsync(options.Organization, repository.Name, primary, cancellationToken)
			.ConfigureAwait(false);

		bool isApplication = ProjectClassifier.IsApplication(content);
		logger.WriteVerbose($"  Primary project {primary} is {(isApplication ? "an application" : "a library")}");

		return isApplication;
	}

	/// <summary>
	/// Gets the latest build workflow run for a repository's default branch.
	/// </summary>
	/// <param name="repository">The repository to inspect.</param>
	/// <param name="options">The gathering settings.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The run, or <see langword="null"/> when no build workflow has run.</returns>
	private async Task<GitHubWorkflowRun?> GetBuildStatusAsync(GitHubRepository repository, ProfileOptions options, CancellationToken cancellationToken)
	{
		GitHubWorkflowRun? run = await gitHub
			.GetLatestWorkflowRunAsync(options.Organization, repository.Name, options.BuildWorkflowFileName, repository.DefaultBranch, cancellationToken)
			.ConfigureAwait(false);

		if (run is not null || options.FallbackWorkflowFileNames.Count == 0)
		{
			return run;
		}

		// Repositories that break the naming convention still get a status, but the warning says which
		// ones need renaming so the exception can be retired.
		IReadOnlyList<string> workflows = await gitHub
			.ListActiveWorkflowFileNamesAsync(options.Organization, repository.Name, cancellationToken)
			.ConfigureAwait(false);

		foreach (string fallback in options.FallbackWorkflowFileNames)
		{
			if (!workflows.Contains(fallback, StringComparer.OrdinalIgnoreCase))
			{
				continue;
			}

			logger.WriteWarning($"  {repository.Name} builds through {fallback} rather than {options.BuildWorkflowFileName}, so it should be renamed");
			return await gitHub
				.GetLatestWorkflowRunAsync(options.Organization, repository.Name, fallback, repository.DefaultBranch, cancellationToken)
				.ConfigureAwait(false);
		}

		return null;
	}

	/// <summary>
	/// Finds the newest version of an application published to the winget community repository.
	/// </summary>
	/// <param name="repositoryName">The repository name, which doubles as the winget package name.</param>
	/// <param name="options">The gathering settings.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The newest published version, or <see langword="null"/> when the package is not in winget.</returns>
	private async Task<string?> GetWingetVersionAsync(string repositoryName, ProfileOptions options, CancellationToken cancellationToken)
	{
		string publisher = options.WingetPublisher;
		string manifestPath = $"manifests/{char.ToLowerInvariant(publisher[0]).ToString(CultureInfo.InvariantCulture)}/{publisher}/{repositoryName}";

		IReadOnlyList<string> versions = await gitHub
			.ListDirectoryNamesAsync(WingetRepositoryOwner, WingetRepositoryName, manifestPath, cancellationToken)
			.ConfigureAwait(false);

		if (versions.Count == 0)
		{
			return null;
		}

		// Compare by version precedence rather than as text, so 1.0.21 beats 1.0.9.
		return versions.Aggregate((newest, candidate) => SemanticVersion.IsGreater(candidate, newest) ? candidate : newest);
	}

	/// <summary>
	/// Removes the conventional single <c>v</c> from the front of a release tag.
	/// </summary>
	/// <param name="tagName">The tag name, or <see langword="null"/>.</param>
	/// <returns>The version without its prefix, or <see langword="null"/> when there was no tag.</returns>
	private static string? StripTagPrefix(string? tagName) =>
		tagName is not null && (tagName.StartsWith('v') || tagName.StartsWith('V')) ? tagName[1..] : tagName;
}
