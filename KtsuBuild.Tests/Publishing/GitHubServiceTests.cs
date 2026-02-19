// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Publishing;

using KtsuBuild.Abstractions;
using KtsuBuild.Publishing;
using KtsuBuild.Tests.Helpers;
using KtsuBuild.Tests.Mocks;

using NSubstitute;

[TestClass]
public class GitHubServiceTests
{
	private IProcessRunner _processRunner = null!;
	private IGitService _gitService = null!;
	private GitHubService _service = null!;
	private string _tempDir = null!;

	[TestInitialize]
	public void Setup()
	{
		_processRunner = Substitute.For<IProcessRunner>();
		_gitService = Substitute.For<IGitService>();
		_service = new GitHubService(_processRunner, _gitService, new MockBuildLogger());
		_tempDir = TestHelpers.CreateTempDir("GitHubSvc");

		// Default setup for git identity and tag operations
		_gitService.SetIdentityAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);
		_gitService.CreateAndPushTagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
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

	// CreateReleaseAsync

	[TestMethod]
	public async Task CreateReleaseAsync_Success_CreatesTagAndRelease()
	{
		_processRunner.RunWithCallbackAsync("gh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		ReleaseOptions options = CreateReleaseOptions();
		await _service.CreateReleaseAsync(options).ConfigureAwait(false);

		await _gitService.Received(1).CreateAndPushTagAsync(
			Arg.Any<string>(), "v1.0.0", "abc123", Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task CreateReleaseAsync_WithGenerateNotes_IncludesGenerateNotesFlag()
	{
		_processRunner.RunWithCallbackAsync("gh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		ReleaseOptions options = CreateReleaseOptions();
		options.GenerateNotes = true;
		await _service.CreateReleaseAsync(options).ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("gh",
			Arg.Is<string>(a => a.Contains("--generate-notes")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task CreateReleaseAsync_WithLatestChangelogFile_UsesLatestChangelog()
	{
		_processRunner.RunWithCallbackAsync("gh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		string changelogFile = Path.Combine(_tempDir, "LATEST_CHANGELOG.md");
		await File.WriteAllTextAsync(changelogFile, "## Changes\n- Fix bug").ConfigureAwait(false);

		ReleaseOptions options = CreateReleaseOptions();
		options.LatestChangelogFile = changelogFile;
		await _service.CreateReleaseAsync(options).ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("gh",
			Arg.Is<string>(a => a.Contains("--notes-file")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task CreateReleaseAsync_WithPrerelease_IncludesPrereleaseFlag()
	{
		_processRunner.RunWithCallbackAsync("gh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		ReleaseOptions options = CreateReleaseOptions();
		options.IsPrerelease = true;
		await _service.CreateReleaseAsync(options).ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("gh",
			Arg.Is<string>(a => a.Contains("--prerelease")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task CreateReleaseAsync_WithAssetFiles_IncludesAssetPaths()
	{
		_processRunner.RunWithCallbackAsync("gh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		string assetFile = Path.Combine(_tempDir, "mypackage.nupkg");
		await File.WriteAllTextAsync(assetFile, "fake-package").ConfigureAwait(false);

		ReleaseOptions options = CreateReleaseOptions();
		options.AssetPaths = [assetFile];
		await _service.CreateReleaseAsync(options).ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("gh",
			Arg.Is<string>(a => a.Contains("mypackage.nupkg")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task CreateReleaseAsync_Failure_ThrowsInvalidOperationException()
	{
		_processRunner.RunWithCallbackAsync("gh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(1);

		ReleaseOptions options = CreateReleaseOptions();
		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.CreateReleaseAsync(options)).ConfigureAwait(false);
	}

	// UploadReleaseAssetsAsync

	[TestMethod]
	public async Task UploadReleaseAssetsAsync_NoAssets_ReturnsEarly()
	{
		await _service.UploadReleaseAssetsAsync("1.0.0", []).ConfigureAwait(false);

		await _processRunner.DidNotReceive().RunWithCallbackAsync("gh",
			Arg.Any<string>(),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task UploadReleaseAssetsAsync_WithAssets_UploadsEach()
	{
		_processRunner.RunWithCallbackAsync("gh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		string asset1 = Path.Combine(_tempDir, "file1.nupkg");
		string asset2 = Path.Combine(_tempDir, "file2.nupkg");
		await File.WriteAllTextAsync(asset1, "pkg1").ConfigureAwait(false);
		await File.WriteAllTextAsync(asset2, "pkg2").ConfigureAwait(false);

		await _service.UploadReleaseAssetsAsync("1.0.0", [asset1, asset2]).ConfigureAwait(false);

		await _processRunner.Received(2).RunWithCallbackAsync("gh",
			Arg.Is<string>(a => a.Contains("release upload v1.0.0")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task UploadReleaseAssetsAsync_MissingAssetFile_SkipsIt()
	{
		await _service.UploadReleaseAssetsAsync("1.0.0", ["/nonexistent/file.nupkg"]).ConfigureAwait(false);

		await _processRunner.DidNotReceive().RunWithCallbackAsync("gh",
			Arg.Any<string>(),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task UploadReleaseAssetsAsync_UploadFailure_ContinuesWithNext()
	{
		_processRunner.RunWithCallbackAsync("gh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(1);

		string asset = Path.Combine(_tempDir, "file.nupkg");
		await File.WriteAllTextAsync(asset, "pkg").ConfigureAwait(false);

		// Should not throw even on upload failure
		await _service.UploadReleaseAssetsAsync("1.0.0", [asset]).ConfigureAwait(false);
	}

	// GetRepositoryInfoAsync

	[TestMethod]
	public async Task GetRepositoryInfoAsync_ValidJson_ReturnsRepositoryInfo()
	{
		string json = """{"owner":{"login":"ktsu-dev"},"nameWithOwner":"ktsu-dev/KtsuBuild","isFork":false}""";
		_processRunner.RunAsync("gh", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult(json));

		RepositoryInfo? info = await _service.GetRepositoryInfoAsync("/repo").ConfigureAwait(false);

		Assert.IsNotNull(info);
		Assert.AreEqual("ktsu-dev", info.Owner);
		Assert.AreEqual("KtsuBuild", info.Name);
		Assert.IsFalse(info.IsFork);
	}

	[TestMethod]
	public async Task GetRepositoryInfoAsync_FailedProcess_ReturnsNull()
	{
		_processRunner.RunAsync("gh", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult());

		RepositoryInfo? info = await _service.GetRepositoryInfoAsync("/repo").ConfigureAwait(false);

		Assert.IsNull(info);
	}

	[TestMethod]
	public async Task GetRepositoryInfoAsync_InvalidJson_ReturnsNull()
	{
		_processRunner.RunAsync("gh", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("not-json"));

		RepositoryInfo? info = await _service.GetRepositoryInfoAsync("/repo").ConfigureAwait(false);

		Assert.IsNull(info);
	}

	[TestMethod]
	public async Task GetRepositoryInfoAsync_EmptyOutput_ReturnsNull()
	{
		_processRunner.RunAsync("gh", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult(""));

		RepositoryInfo? info = await _service.GetRepositoryInfoAsync("/repo").ConfigureAwait(false);

		Assert.IsNull(info);
	}

	[TestMethod]
	public async Task GetRepositoryInfoAsync_ParsesNameFromNameWithOwner()
	{
		string json = """{"owner":{"login":"myorg"},"nameWithOwner":"myorg/my-repo","isFork":false}""";
		_processRunner.RunAsync("gh", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult(json));

		RepositoryInfo? info = await _service.GetRepositoryInfoAsync("/repo").ConfigureAwait(false);

		Assert.IsNotNull(info);
		Assert.AreEqual("my-repo", info.Name);
	}

	// IsOfficialRepositoryAsync

	[TestMethod]
	public async Task IsOfficialRepositoryAsync_OfficialRepo_ReturnsTrue()
	{
		string json = """{"owner":{"login":"ktsu-dev"},"nameWithOwner":"ktsu-dev/KtsuBuild","isFork":false}""";
		_processRunner.RunAsync("gh", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult(json));

		bool result = await _service.IsOfficialRepositoryAsync("/repo", "ktsu-dev").ConfigureAwait(false);

		Assert.IsTrue(result);
	}

	[TestMethod]
	public async Task IsOfficialRepositoryAsync_Fork_ReturnsFalse()
	{
		string json = """{"owner":{"login":"ktsu-dev"},"nameWithOwner":"ktsu-dev/KtsuBuild","isFork":true}""";
		_processRunner.RunAsync("gh", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult(json));

		bool result = await _service.IsOfficialRepositoryAsync("/repo", "ktsu-dev").ConfigureAwait(false);

		Assert.IsFalse(result);
	}

	[TestMethod]
	public async Task IsOfficialRepositoryAsync_WrongOwner_ReturnsFalse()
	{
		string json = """{"owner":{"login":"some-user"},"nameWithOwner":"some-user/KtsuBuild","isFork":false}""";
		_processRunner.RunAsync("gh", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult(json));

		bool result = await _service.IsOfficialRepositoryAsync("/repo", "ktsu-dev").ConfigureAwait(false);

		Assert.IsFalse(result);
	}

	[TestMethod]
	public async Task IsOfficialRepositoryAsync_NullRepoInfo_ReturnsFalse()
	{
		_processRunner.RunAsync("gh", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult());

		bool result = await _service.IsOfficialRepositoryAsync("/repo", "ktsu-dev").ConfigureAwait(false);

		Assert.IsFalse(result);
	}

	[TestMethod]
	public async Task IsOfficialRepositoryAsync_CaseInsensitiveOwnerComparison()
	{
		string json = """{"owner":{"login":"Ktsu-Dev"},"nameWithOwner":"Ktsu-Dev/KtsuBuild","isFork":false}""";
		_processRunner.RunAsync("gh", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult(json));

		bool result = await _service.IsOfficialRepositoryAsync("/repo", "ktsu-dev").ConfigureAwait(false);

		Assert.IsTrue(result);
	}

	// SetRepositoryTopicsAsync

	[TestMethod]
	public async Task SetRepositoryTopicsAsync_Success_CallsGhApi()
	{
		string json = """{"owner":{"login":"ktsu-dev"},"nameWithOwner":"ktsu-dev/KtsuBuild","isFork":false}""";
		_processRunner.RunAsync("gh", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult(json));
		_processRunner.RunWithCallbackAsync("gh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _service.SetRepositoryTopicsAsync("/repo", ["dotnet", "csharp"]).ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("gh",
			Arg.Is<string>(a => a.Contains("repos/ktsu-dev/KtsuBuild/topics") && a.Contains("names[]=dotnet") && a.Contains("names[]=csharp")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task SetRepositoryTopicsAsync_EmptyTopics_SkipsApiCall()
	{
		await _service.SetRepositoryTopicsAsync("/repo", []).ConfigureAwait(false);

		await _processRunner.DidNotReceive().RunWithCallbackAsync("gh",
			Arg.Any<string>(),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task SetRepositoryTopicsAsync_FailedRepoInfo_LogsWarning()
	{
		_processRunner.RunAsync("gh", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult());

		// Should not throw
		await _service.SetRepositoryTopicsAsync("/repo", ["dotnet"]).ConfigureAwait(false);

		await _processRunner.DidNotReceive().RunWithCallbackAsync("gh",
			Arg.Any<string>(),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task SetRepositoryTopicsAsync_ApiFailure_DoesNotThrow()
	{
		string json = """{"owner":{"login":"ktsu-dev"},"nameWithOwner":"ktsu-dev/KtsuBuild","isFork":false}""";
		_processRunner.RunAsync("gh", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult(json));
		_processRunner.RunWithCallbackAsync("gh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(1);

		// Should not throw even on API failure
		await _service.SetRepositoryTopicsAsync("/repo", ["dotnet"]).ConfigureAwait(false);
	}

	private ReleaseOptions CreateReleaseOptions() => new()
	{
		Version = "1.0.0",
		CommitHash = "abc123",
		GithubToken = "test-token",
		WorkingDirectory = _tempDir,
		GenerateNotes = false,
	};
}
