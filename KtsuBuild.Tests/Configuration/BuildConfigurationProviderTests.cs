// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Configuration;

using KtsuBuild.Abstractions;
using KtsuBuild.Configuration;
using KtsuBuild.Tests.Helpers;
using NSubstitute;

[TestClass]
public class BuildConfigurationProviderTests
{
	private IGitService _gitService = null!;
	private IGitHubService _gitHubService = null!;
	private BuildConfigurationProvider _provider = null!;
	private string _tempDir = null!;

	[TestInitialize]
	public void Setup()
	{
		_gitService = Substitute.For<IGitService>();
		_gitHubService = Substitute.For<IGitHubService>();
		_provider = new BuildConfigurationProvider(_gitService, _gitHubService);
		_tempDir = TestHelpers.CreateTempDir("BuildCfg");
	}

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	// CreateAsync

	[TestMethod]
	public async Task CreateAsync_OfficialMainUntagged_ShouldReleaseTrue()
	{
		_gitHubService.IsOfficialRepositoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_gitService.IsCommitTaggedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(false);

		BuildConfigurationOptions options = CreateDefaultOptions();
		options.GitRef = "refs/heads/main";

		BuildConfiguration config = await _provider.CreateAsync(options).ConfigureAwait(false);

		Assert.IsTrue(config.ShouldRelease);
		Assert.IsTrue(config.IsMain);
		Assert.IsTrue(config.IsOfficial);
		Assert.IsFalse(config.IsTagged);
	}

	[TestMethod]
	public async Task CreateAsync_OfficialMainTagged_ShouldReleaseFalse()
	{
		_gitHubService.IsOfficialRepositoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_gitService.IsCommitTaggedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(true);

		BuildConfigurationOptions options = CreateDefaultOptions();
		options.GitRef = "refs/heads/main";

		BuildConfiguration config = await _provider.CreateAsync(options).ConfigureAwait(false);

		Assert.IsFalse(config.ShouldRelease);
		Assert.IsTrue(config.IsTagged);
	}

	[TestMethod]
	public async Task CreateAsync_ForkMainUntagged_ShouldReleaseFalse()
	{
		_gitHubService.IsOfficialRepositoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(false);
		_gitService.IsCommitTaggedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(false);

		BuildConfigurationOptions options = CreateDefaultOptions();
		options.GitRef = "refs/heads/main";

		BuildConfiguration config = await _provider.CreateAsync(options).ConfigureAwait(false);

		Assert.IsFalse(config.ShouldRelease);
		Assert.IsFalse(config.IsOfficial);
	}

	[TestMethod]
	public async Task CreateAsync_OfficialNonMain_ShouldReleaseFalse()
	{
		_gitHubService.IsOfficialRepositoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_gitService.IsCommitTaggedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(false);

		BuildConfigurationOptions options = CreateDefaultOptions();
		options.GitRef = "refs/heads/feature-branch";

		BuildConfiguration config = await _provider.CreateAsync(options).ConfigureAwait(false);

		Assert.IsFalse(config.ShouldRelease);
		Assert.IsFalse(config.IsMain);
	}

	[TestMethod]
	public async Task CreateAsync_SetsOutputAndStagingPaths()
	{
		SetupDefaultMocks();

		BuildConfigurationOptions options = CreateDefaultOptions();
		BuildConfiguration config = await _provider.CreateAsync(options).ConfigureAwait(false);

		Assert.AreEqual(Path.Combine(_tempDir, "output"), config.OutputPath);
		Assert.AreEqual(Path.Combine(_tempDir, "staging"), config.StagingPath);
	}

	[TestMethod]
	public async Task CreateAsync_WithCsxFiles_SetsBuildArgs()
	{
		SetupDefaultMocks();

		// Create a .csx file
		await File.WriteAllTextAsync(Path.Combine(_tempDir, "build.csx"), "// script").ConfigureAwait(false);

		BuildConfigurationOptions options = CreateDefaultOptions();
		BuildConfiguration config = await _provider.CreateAsync(options).ConfigureAwait(false);

		Assert.IsTrue(config.UseDotnetScript);
		Assert.AreEqual("-maxCpuCount:1", config.BuildArgs);
	}

	[TestMethod]
	public async Task CreateAsync_NoCsxFiles_EmptyBuildArgs()
	{
		SetupDefaultMocks();

		BuildConfigurationOptions options = CreateDefaultOptions();
		BuildConfiguration config = await _provider.CreateAsync(options).ConfigureAwait(false);

		Assert.IsFalse(config.UseDotnetScript);
		Assert.AreEqual(string.Empty, config.BuildArgs);
	}

	[TestMethod]
	public async Task CreateAsync_CopiesOptionsToConfiguration()
	{
		SetupDefaultMocks();

		BuildConfigurationOptions options = CreateDefaultOptions();
		options.ServerUrl = "https://custom.github.com";
		options.GitHubOwner = "myowner";
		options.GitHubRepo = "myowner/myrepo";
		options.GithubToken = "ghp_token123";
		options.NuGetApiKey = "nuget-key";
		options.KtsuPackageKey = "ktsu-key";

		BuildConfiguration config = await _provider.CreateAsync(options).ConfigureAwait(false);

		Assert.AreEqual("https://custom.github.com", config.ServerUrl);
		Assert.AreEqual("myowner", config.GitHubOwner);
		Assert.AreEqual("myowner/myrepo", config.GitHubRepo);
		Assert.AreEqual("ghp_token123", config.GithubToken);
		Assert.AreEqual("nuget-key", config.NuGetApiKey);
		Assert.AreEqual("ktsu-key", config.KtsuPackageKey);
	}

	[TestMethod]
	public async Task CreateAsync_SetsDefaultVersion()
	{
		SetupDefaultMocks();

		BuildConfigurationOptions options = CreateDefaultOptions();
		BuildConfiguration config = await _provider.CreateAsync(options).ConfigureAwait(false);

		Assert.AreEqual("1.0.0-pre.0", config.Version);
	}

	// CreateFromEnvironmentAsync

	[TestMethod]
	public async Task CreateFromEnvironmentAsync_WithGitHubRepository_ParsesOwnerAndRepo()
	{
		SetupDefaultMocks();
		Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", "testowner/testrepo");

		try
		{
			BuildConfiguration config = await _provider.CreateFromEnvironmentAsync(_tempDir).ConfigureAwait(false);

			Assert.AreEqual("testowner", config.GitHubOwner);
			Assert.AreEqual("testowner/testrepo", config.GitHubRepo);
		}
		finally
		{
			Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", null);
		}
	}

	[TestMethod]
	public async Task CreateFromEnvironmentAsync_WithoutGitHubRepository_DetectsFromRemote()
	{
		SetupDefaultMocks();
		_gitService.GetRemoteUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("https://github.com/remoteowner/remoterepo.git"));

		// Ensure GITHUB_REPOSITORY is not set
		Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", null);

		try
		{
			BuildConfiguration config = await _provider.CreateFromEnvironmentAsync(_tempDir).ConfigureAwait(false);

			Assert.AreEqual("remoteowner", config.GitHubOwner);
			Assert.AreEqual("remoteowner/remoterepo", config.GitHubRepo);
		}
		finally
		{
			Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", null);
		}
	}

	[TestMethod]
	public async Task CreateFromEnvironmentAsync_SshRemote_ParsesCorrectly()
	{
		SetupDefaultMocks();
		_gitService.GetRemoteUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("git@github.com:sshowner/sshrepo.git"));
		Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", null);

		try
		{
			BuildConfiguration config = await _provider.CreateFromEnvironmentAsync(_tempDir).ConfigureAwait(false);

			Assert.AreEqual("sshowner", config.GitHubOwner);
			Assert.AreEqual("sshowner/sshrepo", config.GitHubRepo);
		}
		finally
		{
			Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", null);
		}
	}

	[TestMethod]
	public async Task CreateFromEnvironmentAsync_ReadsGitHubTokenFromGH_TOKEN()
	{
		SetupDefaultMocks();
		Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
		Environment.SetEnvironmentVariable("GH_TOKEN", "gh-token-value");

		try
		{
			BuildConfiguration config = await _provider.CreateFromEnvironmentAsync(_tempDir).ConfigureAwait(false);

			Assert.AreEqual("gh-token-value", config.GithubToken);
		}
		finally
		{
			Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
			Environment.SetEnvironmentVariable("GH_TOKEN", null);
		}
	}

	[TestMethod]
	public async Task CreateFromEnvironmentAsync_ReadsGitHubTokenFromGITHUB_TOKEN()
	{
		SetupDefaultMocks();
		Environment.SetEnvironmentVariable("GITHUB_TOKEN", "github-token-value");
		Environment.SetEnvironmentVariable("GH_TOKEN", null);

		try
		{
			BuildConfiguration config = await _provider.CreateFromEnvironmentAsync(_tempDir).ConfigureAwait(false);

			Assert.AreEqual("github-token-value", config.GithubToken);
		}
		finally
		{
			Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
			Environment.SetEnvironmentVariable("GH_TOKEN", null);
		}
	}

	[TestMethod]
	public async Task CreateFromEnvironmentAsync_ReadsNuGetApiKey()
	{
		SetupDefaultMocks();
		Environment.SetEnvironmentVariable("NUGET_API_KEY", "nuget-key-123");

		try
		{
			BuildConfiguration config = await _provider.CreateFromEnvironmentAsync(_tempDir).ConfigureAwait(false);

			Assert.AreEqual("nuget-key-123", config.NuGetApiKey);
		}
		finally
		{
			Environment.SetEnvironmentVariable("NUGET_API_KEY", null);
		}
	}

	[TestMethod]
	public async Task CreateFromEnvironmentAsync_ReadsKtsuPackageKey()
	{
		SetupDefaultMocks();
		Environment.SetEnvironmentVariable("KTSU_PACKAGE_KEY", "ktsu-key-456");

		try
		{
			BuildConfiguration config = await _provider.CreateFromEnvironmentAsync(_tempDir).ConfigureAwait(false);

			Assert.AreEqual("ktsu-key-456", config.KtsuPackageKey);
		}
		finally
		{
			Environment.SetEnvironmentVariable("KTSU_PACKAGE_KEY", null);
		}
	}

	[TestMethod]
	public async Task CreateFromEnvironmentAsync_SetsAssetPatterns()
	{
		SetupDefaultMocks();

		BuildConfiguration config = await _provider.CreateFromEnvironmentAsync(_tempDir).ConfigureAwait(false);

		Assert.IsTrue(config.AssetPatterns.Count >= 3, "Should have at least 3 asset patterns");
		Assert.IsTrue(config.AssetPatterns.Any(p => p.Contains("*.nupkg")), "Should include nupkg pattern");
		Assert.IsTrue(config.AssetPatterns.Any(p => p.Contains("*.snupkg")), "Should include snupkg pattern");
		Assert.IsTrue(config.AssetPatterns.Any(p => p.Contains("*.zip")), "Should include zip pattern");
	}

	[TestMethod]
	public async Task CreateFromEnvironmentAsync_FallsBackToGitSha_WhenNoEnvVar()
	{
		SetupDefaultMocks();
		_gitService.GetCurrentCommitHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns("fallbackhash123");
		Environment.SetEnvironmentVariable("GITHUB_SHA", null);

		try
		{
			BuildConfiguration config = await _provider.CreateFromEnvironmentAsync(_tempDir).ConfigureAwait(false);

			Assert.AreEqual("fallbackhash123", config.GitSha);
		}
		finally
		{
			Environment.SetEnvironmentVariable("GITHUB_SHA", null);
		}
	}

	private void SetupDefaultMocks()
	{
		_gitHubService.IsOfficialRepositoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(false);
		_gitService.IsCommitTaggedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(false);
		_gitService.GetCurrentCommitHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns("abc123");
		_gitService.GetRemoteUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>(null));
	}

	private BuildConfigurationOptions CreateDefaultOptions() => new()
	{
		WorkspacePath = _tempDir,
		GitRef = "refs/heads/main",
		GitSha = "abc123",
		GitHubOwner = "ktsu-dev",
		GitHubRepo = "ktsu-dev/KtsuBuild",
		GithubToken = string.Empty,
		ExpectedOwner = "ktsu-dev",
	};
}
