// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Configuration;

using KtsuBuild.Abstractions;
using static Polyfill;

/// <summary>
/// Provides build configuration from options or environment.
/// </summary>
/// <param name="gitService">The Git service.</param>
/// <param name="gitHubService">The GitHub service.</param>
public class BuildConfigurationProvider(IGitService gitService, IGitHubService gitHubService) : IBuildConfigurationProvider
{
	/// <inheritdoc/>
	public async Task<BuildConfiguration> CreateAsync(BuildConfigurationOptions options, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(options);

		// Check if official repository
		bool isOfficial = await gitHubService.IsOfficialRepositoryAsync(options.WorkspacePath, options.ExpectedOwner, cancellationToken).ConfigureAwait(false);

		// Check branch and tag status
		bool isMain = options.GitRef == "refs/heads/main";
		bool isTagged = await gitService.IsCommitTaggedAsync(options.WorkspacePath, options.GitSha, cancellationToken).ConfigureAwait(false);
		bool shouldRelease = isMain && !isTagged && isOfficial;

		// Check for .csx files
		bool useDotnetScript = Directory.GetFiles(options.WorkspacePath, "*.csx", SearchOption.AllDirectories).Length > 0;

		// Setup paths
		string outputPath = Path.Combine(options.WorkspacePath, "output");
		string stagingPath = Path.Combine(options.WorkspacePath, "staging");

		return new BuildConfiguration
		{
			IsOfficial = isOfficial,
			IsMain = isMain,
			IsTagged = isTagged,
			ShouldRelease = shouldRelease,
			UseDotnetScript = useDotnetScript,
			OutputPath = outputPath,
			StagingPath = stagingPath,
			PackagePattern = Path.Combine(stagingPath, "*.nupkg"),
			SymbolsPattern = Path.Combine(stagingPath, "*.snupkg"),
			ApplicationPattern = Path.Combine(stagingPath, "*.zip"),
			BuildArgs = useDotnetScript ? "-maxCpuCount:1" : string.Empty,
			WorkspacePath = options.WorkspacePath,
			ServerUrl = options.ServerUrl,
			GitRef = options.GitRef,
			GitSha = options.GitSha,
			GitHubOwner = options.GitHubOwner,
			GitHubRepo = options.GitHubRepo,
			GithubToken = options.GithubToken,
			NuGetApiKey = options.NuGetApiKey,
			KtsuPackageKey = options.KtsuPackageKey,
			ExpectedOwner = options.ExpectedOwner,
			Version = "1.0.0-pre.0",
			ReleaseHash = options.GitSha,
			ChangelogFile = options.ChangelogFile,
			LatestChangelogFile = options.LatestChangelogFile,
			AssetPatterns = options.AssetPatterns,
			Configuration = options.Configuration,
		};
	}

	/// <inheritdoc/>
	public async Task<BuildConfiguration> CreateFromEnvironmentAsync(string workspacePath, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workspacePath);

		// Read from environment variables (GitHub Actions style)
		string serverUrl = Environment.GetEnvironmentVariable("GITHUB_SERVER_URL") ?? "https://github.com";
		string gitRef = Environment.GetEnvironmentVariable("GITHUB_REF") ?? string.Empty;
		string gitSha = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? await gitService.GetCurrentCommitHashAsync(workspacePath, cancellationToken).ConfigureAwait(false);

		string? repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
		string githubOwner = string.Empty;
		string githubRepo = string.Empty;

		if (!string.IsNullOrEmpty(repository))
		{
			string[] parts = repository.Split('/');
			if (parts.Length == 2)
			{
				githubOwner = parts[0];
				githubRepo = repository;
			}
		}
		else
		{
			// Try to detect from git remote
			string? remoteUrl = await gitService.GetRemoteUrlAsync(workspacePath, cancellationToken: cancellationToken).ConfigureAwait(false);
			if (!string.IsNullOrEmpty(remoteUrl) && remoteUrl!.Contains("github.com"))
			{
				// Parse owner/repo from URL
				int startIndex = remoteUrl!.IndexOf("github.com", StringComparison.OrdinalIgnoreCase) + 11;
				if (startIndex < remoteUrl.Length)
				{
					char separator = remoteUrl[startIndex];
					if (separator is ':' or '/')
					{
						startIndex++;
					}
					string ownerRepo = remoteUrl[startIndex..].TrimEnd('/').Replace(".git", string.Empty);
					string[] parts = ownerRepo.Split('/');
					if (parts.Length == 2)
					{
						githubOwner = parts[0];
						githubRepo = ownerRepo;
					}
				}
			}
		}

		string githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? Environment.GetEnvironmentVariable("GH_TOKEN") ?? string.Empty;
		string nugetApiKey = Environment.GetEnvironmentVariable("NUGET_API_KEY") ?? string.Empty;
		string ktsuPackageKey = Environment.GetEnvironmentVariable("KTSU_PACKAGE_KEY") ?? string.Empty;
		string expectedOwner = Environment.GetEnvironmentVariable("EXPECTED_OWNER") ?? githubOwner;

		BuildConfigurationOptions options = new()
		{
			ServerUrl = serverUrl,
			GitRef = gitRef,
			GitSha = gitSha,
			GitHubOwner = githubOwner,
			GitHubRepo = githubRepo,
			GithubToken = githubToken,
			NuGetApiKey = nugetApiKey,
			KtsuPackageKey = ktsuPackageKey,
			WorkspacePath = workspacePath,
			ExpectedOwner = expectedOwner,
			AssetPatterns =
			[
				Path.Combine(workspacePath, "staging", "*.nupkg"),
				Path.Combine(workspacePath, "staging", "*.snupkg"),
				Path.Combine(workspacePath, "staging", "*.zip"),
			],
		};

		return await CreateAsync(options, cancellationToken).ConfigureAwait(false);
	}
}
