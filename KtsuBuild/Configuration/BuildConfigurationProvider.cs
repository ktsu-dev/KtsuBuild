// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Configuration;

using System.Diagnostics.CodeAnalysis;
using KtsuBuild.Abstractions;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Provides build configuration from options or environment.
/// </summary>
/// <param name="gitService">The Git service.</param>
/// <param name="gitHubService">The GitHub service.</param>
public class BuildConfigurationProvider(IGitService gitService, IGitHubService gitHubService) : IBuildConfigurationProvider
{
	/// <summary>
	/// Name of the directory that packaged artifacts are staged into.
	/// </summary>
	private const string StagingDirectoryName = "staging";

	/// <summary>
	/// GitHub host used when <c>GITHUB_SERVER_URL</c> is not set.
	/// </summary>
	[SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "This is the documented fallback for GITHUB_SERVER_URL, not a fixed endpoint; callers override it through the environment.")]
	private const string DefaultServerUrl = "https://github.com";

	/// <summary>
	/// The GitHub host name looked for when parsing a Git remote URL.
	/// </summary>
	private const string GitHubHost = "github.com";

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
		string stagingPath = Path.Combine(options.WorkspacePath, StagingDirectoryName);

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
		string serverUrl = Environment.GetEnvironmentVariable("GITHUB_SERVER_URL") ?? DefaultServerUrl;
		string gitRef = Environment.GetEnvironmentVariable("GITHUB_REF") ?? string.Empty;
		string gitSha = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? await gitService.GetCurrentCommitHashAsync(workspacePath, cancellationToken).ConfigureAwait(false);

		(string githubOwner, string githubRepo) = await ResolveRepositoryAsync(workspacePath, cancellationToken).ConfigureAwait(false);

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
				Path.Combine(workspacePath, StagingDirectoryName, "*.nupkg"),
				Path.Combine(workspacePath, StagingDirectoryName, "*.snupkg"),
				Path.Combine(workspacePath, StagingDirectoryName, "*.zip"),
			],
		};

		BuildConfiguration configuration = await CreateAsync(options, cancellationToken).ConfigureAwait(false);

		ApplyIosEnvironment(configuration);

		return configuration;
	}

	/// <summary>
	/// Resolves the GitHub owner and <c>owner/repo</c> pair from the environment, falling back
	/// to the configured Git remote.
	/// </summary>
	/// <param name="workspacePath">The repository directory.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The owner and <c>owner/repo</c>, both empty when neither source identifies them.</returns>
	private async Task<(string Owner, string Repo)> ResolveRepositoryAsync(string workspacePath, CancellationToken cancellationToken)
	{
		string? repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
		if (!string.IsNullOrEmpty(repository))
		{
			string[] parts = repository.Split('/');
			return parts.Length == 2 ? (parts[0], repository) : (string.Empty, string.Empty);
		}

		// Try to detect from git remote
		string? remoteUrl = await gitService.GetRemoteUrlAsync(workspacePath, cancellationToken: cancellationToken).ConfigureAwait(false);
		return ParseRepositoryFromRemoteUrl(remoteUrl);
	}

	/// <summary>
	/// Parses the GitHub owner and <c>owner/repo</c> pair out of a Git remote URL.
	/// </summary>
	/// <param name="remoteUrl">The remote URL, in either HTTPS or SSH form.</param>
	/// <returns>The owner and <c>owner/repo</c>, both empty when the URL is not a GitHub remote.</returns>
	private static (string Owner, string Repo) ParseRepositoryFromRemoteUrl(string? remoteUrl)
	{
		if (string.IsNullOrEmpty(remoteUrl) || !remoteUrl.Contains(GitHubHost))
		{
			return (string.Empty, string.Empty);
		}

		// Parse owner/repo from URL
		int startIndex = remoteUrl.IndexOf(GitHubHost, StringComparison.OrdinalIgnoreCase) + 11;
		if (startIndex >= remoteUrl.Length)
		{
			return (string.Empty, string.Empty);
		}

		char separator = remoteUrl[startIndex];
		if (separator is ':' or '/')
		{
			startIndex++;
		}

		string ownerRepo = remoteUrl[startIndex..].TrimEnd('/').Replace(".git", string.Empty);
		string[] parts = ownerRepo.Split('/');
		return parts.Length == 2 ? (parts[0], ownerRepo) : (string.Empty, string.Empty);
	}

	// Reads the iOS signing, toolchain, and App Store Connect inputs from the environment
	// onto the configuration. Extracted to keep CreateFromEnvironmentAsync's complexity in
	// check. These carry secrets and are read here only; nothing logs them.
	// IosSigningAvailable is the single boolean gate that may surface in output.
	private static void ApplyIosEnvironment(BuildConfiguration configuration)
	{
		configuration.IosSigningAvailable = string.Equals(Environment.GetEnvironmentVariable("IOS_SIGNING_AVAILABLE"), "true", StringComparison.OrdinalIgnoreCase);
		configuration.IosCodesignKey = Environment.GetEnvironmentVariable("IOS_CODESIGN_KEY") ?? string.Empty;
		configuration.IosProvisionName = Environment.GetEnvironmentVariable("IOS_PROVISION_NAME") ?? string.Empty;
		configuration.IosCertP12Base64 = Environment.GetEnvironmentVariable("IOS_CERT_P12_BASE64") ?? string.Empty;
		configuration.IosCertP12Password = Environment.GetEnvironmentVariable("IOS_CERT_P12_PASSWORD") ?? string.Empty;
		configuration.IosKeychainPassword = Environment.GetEnvironmentVariable("IOS_KEYCHAIN_PASSWORD") ?? string.Empty;
		configuration.IosProvisioningProfileBase64 = Environment.GetEnvironmentVariable("IOS_PROVISIONING_PROFILE_BASE64") ?? string.Empty;
		configuration.XcodeVersion = Environment.GetEnvironmentVariable("IOS_XCODE_VERSION") ?? string.Empty;
		configuration.IosWorkloadVersion = Environment.GetEnvironmentVariable("IOS_WORKLOAD_VERSION") ?? string.Empty;

		// App Store Connect API inputs for the TestFlight upload. The key is a secret and is
		// never logged; the key/issuer identifiers are not surfaced either.
		configuration.AppStoreConnectKeyBase64 = Environment.GetEnvironmentVariable("APP_STORE_CONNECT_KEY_BASE64") ?? string.Empty;
		configuration.AppStoreConnectKeyId = Environment.GetEnvironmentVariable("APP_STORE_CONNECT_KEY_ID") ?? string.Empty;
		configuration.AppStoreConnectIssuerId = Environment.GetEnvironmentVariable("APP_STORE_CONNECT_ISSUER_ID") ?? string.Empty;
	}
}
