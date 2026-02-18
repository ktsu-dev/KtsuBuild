// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Publishing;

using KtsuBuild.Abstractions;
using KtsuBuild.Configuration;
using KtsuBuild.Publishing;
using KtsuBuild.Tests.Helpers;
using KtsuBuild.Tests.Mocks;
using NSubstitute;

[TestClass]
public class ReleaseServiceTests
{
	private IDotNetService _dotNetService = null!;
	private INuGetPublisher _nuGetPublisher = null!;
	private IGitHubService _gitHubService = null!;
	private ReleaseService _service = null!;
	private string _tempDir = null!;

	[TestInitialize]
	public void Setup()
	{
		_dotNetService = Substitute.For<IDotNetService>();
		_nuGetPublisher = Substitute.For<INuGetPublisher>();
		_gitHubService = Substitute.For<IGitHubService>();
		_service = new ReleaseService(_dotNetService, _nuGetPublisher, _gitHubService, new MockBuildLogger());
		_tempDir = TestHelpers.CreateTempDir("ReleaseSvc");

		// Default setup - no project files
		_dotNetService.GetProjectFiles(Arg.Any<string>()).Returns([]);
		_dotNetService.PackAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);
		_gitHubService.CreateReleaseAsync(Arg.Any<ReleaseOptions>(), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);
	}

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_PacksNuGetPackages()
	{
		BuildConfiguration config = CreateDefaultConfig();

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _dotNetService.Received(1).PackAsync(
			_tempDir, config.StagingPath, "Release", Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_PublishesExecutableProjects_ForAllRuntimes()
	{
		string projPath = Path.Combine(_tempDir, "MyApp", "MyApp.csproj");
		Directory.CreateDirectory(Path.GetDirectoryName(projPath)!);
		await File.WriteAllTextAsync(projPath, "<Project />").ConfigureAwait(false);

		_dotNetService.GetProjectFiles(Arg.Any<string>()).Returns([projPath]);
		_dotNetService.IsExecutableProject(projPath).Returns(true);
		_dotNetService.PublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);

		BuildConfiguration config = CreateDefaultConfig();
		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		// 7 runtimes per executable project
		await _dotNetService.Received(7).PublishAsync(
			Arg.Any<string>(), projPath, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_PublishesToGitHub_WhenTokenAvailable()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.GithubToken = "token123";

		// Create a fake nupkg in staging
		Directory.CreateDirectory(config.StagingPath);
		await File.WriteAllTextAsync(Path.Combine(config.StagingPath, "test.nupkg"), "fake").ConfigureAwait(false);

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _nuGetPublisher.Received(1).PublishToGitHubAsync(
			Arg.Any<string>(), config.GitHubOwner, config.GithubToken, Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_PublishesToNuGetOrg_WhenApiKeyAvailable()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.GithubToken = "token123";
		config.NuGetApiKey = "nuget-key";

		Directory.CreateDirectory(config.StagingPath);
		await File.WriteAllTextAsync(Path.Combine(config.StagingPath, "test.nupkg"), "fake").ConfigureAwait(false);

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _nuGetPublisher.Received(1).PublishToNuGetOrgAsync(
			Arg.Any<string>(), "nuget-key", Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_PublishesToKtsuSource_WhenKeyAvailable()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.GithubToken = "token123";
		config.KtsuPackageKey = "ktsu-key";

		Directory.CreateDirectory(config.StagingPath);
		await File.WriteAllTextAsync(Path.Combine(config.StagingPath, "test.nupkg"), "fake").ConfigureAwait(false);

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _nuGetPublisher.Received(1).PublishToSourceAsync(
			Arg.Any<string>(), "https://packages.ktsu.dev/v3/index.json", "ktsu-key", Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_SkipsNuGetPublish_WhenNoPackages()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.GithubToken = "token123";

		// No staging directory = no packages
		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _nuGetPublisher.DidNotReceive().PublishToGitHubAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_SkipsNuGetPublish_WhenNoToken()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.GithubToken = string.Empty;

		Directory.CreateDirectory(config.StagingPath);
		await File.WriteAllTextAsync(Path.Combine(config.StagingPath, "test.nupkg"), "fake").ConfigureAwait(false);

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _nuGetPublisher.DidNotReceive().PublishToGitHubAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_CreatesGitHubRelease()
	{
		BuildConfiguration config = CreateDefaultConfig();

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _gitHubService.Received(1).CreateReleaseAsync(
			Arg.Is<ReleaseOptions>(o => o.Version == "1.0.0" && o.CommitHash == "abc123"),
			Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_DetectsPrerelease_FromVersionString()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.Version = "1.0.0-pre.1";

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _gitHubService.Received(1).CreateReleaseAsync(
			Arg.Is<ReleaseOptions>(o => o.IsPrerelease),
			Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_DetectsPrerelease_Alpha()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.Version = "2.0.0-alpha.1";

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _gitHubService.Received(1).CreateReleaseAsync(
			Arg.Is<ReleaseOptions>(o => o.IsPrerelease),
			Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_DetectsPrerelease_Beta()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.Version = "2.0.0-beta.3";

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _gitHubService.Received(1).CreateReleaseAsync(
			Arg.Is<ReleaseOptions>(o => o.IsPrerelease),
			Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_NotPrerelease_ForStableVersion()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.Version = "1.0.0";

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _gitHubService.Received(1).CreateReleaseAsync(
			Arg.Is<ReleaseOptions>(o => !o.IsPrerelease),
			Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_NoExecutableProjects_SkipsPublish()
	{
		_dotNetService.GetProjectFiles(Arg.Any<string>()).Returns(["lib.csproj"]);
		_dotNetService.IsExecutableProject("lib.csproj").Returns(false);

		BuildConfiguration config = CreateDefaultConfig();
		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _dotNetService.DidNotReceive().PublishAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_GeneratesHashesFile_ForZipArchives()
	{
		BuildConfiguration config = CreateDefaultConfig();
		Directory.CreateDirectory(config.StagingPath);

		// Create a fake zip file
		string zipPath = Path.Combine(config.StagingPath, "app-1.0.0-win-x64.zip");
		await File.WriteAllTextAsync(zipPath, "fake-zip-content").ConfigureAwait(false);

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		string hashesPath = Path.Combine(config.StagingPath, "hashes.txt");
		Assert.IsTrue(File.Exists(hashesPath), "hashes.txt should be created");
		string content = await File.ReadAllTextAsync(hashesPath).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("app-1.0.0-win-x64.zip="), "Should contain filename=hash entry");
	}

	private BuildConfiguration CreateDefaultConfig() => new()
	{
		Version = "1.0.0",
		ReleaseHash = "abc123",
		GithubToken = string.Empty,
		GitHubOwner = "ktsu-dev",
		StagingPath = Path.Combine(_tempDir, "staging"),
		OutputPath = Path.Combine(_tempDir, "output"),
		PackagePattern = Path.Combine(_tempDir, "staging", "*.nupkg"),
		LatestChangelogFile = "LATEST_CHANGELOG.md",
		ChangelogFile = "CHANGELOG.md",
		WorkspacePath = _tempDir,
	};
}
