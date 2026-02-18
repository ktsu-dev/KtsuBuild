// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Winget;

using KtsuBuild.Abstractions;
using KtsuBuild.Tests.Helpers;
using KtsuBuild.Tests.Mocks;
using KtsuBuild.Winget;
using NSubstitute;

[TestClass]
public class WingetServiceTests
{
	private IProcessRunner _processRunner = null!;
	private WingetService _service = null!;
	private string _tempDir = null!;

	[TestInitialize]
	public void Setup()
	{
		_processRunner = Substitute.For<IProcessRunner>();
		_service = new WingetService(_processRunner, new MockBuildLogger());
		_tempDir = TestHelpers.CreateTempDir("WingetSvc");
	}

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	// GenerateManifestsAsync - Library detection

	[TestMethod]
	public async Task GenerateManifestsAsync_LibraryProject_ReturnsSuccessWithLibraryFlag()
	{
		// Create a library project whose name matches the root directory (required by IsLibraryOnlyProject)
		string rootDir = Path.Combine(_tempDir, "MyLib");
		Directory.CreateDirectory(rootDir);
		string projDir = Path.Combine(rootDir, "MyLib");
		Directory.CreateDirectory(projDir);
		await File.WriteAllTextAsync(Path.Combine(projDir, "MyLib.csproj"),
			"<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>").ConfigureAwait(false);

		WingetOptions options = CreateDefaultOptions();
		options.RootDirectory = rootDir;

		WingetManifestResult result = await _service.GenerateManifestsAsync(options).ConfigureAwait(false);

		Assert.IsTrue(result.Success);
		Assert.IsTrue(result.IsLibraryOnly);
	}

	[TestMethod]
	public async Task GenerateManifestsAsync_NoGitHubRepo_ReturnsError()
	{
		// Create an exe project so it doesn't get flagged as library-only
		string projDir = Path.Combine(_tempDir, "MyApp");
		Directory.CreateDirectory(projDir);
		await File.WriteAllTextAsync(Path.Combine(projDir, "MyApp.csproj"),
			"<Project><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>").ConfigureAwait(false);

		// No GitHubRepo set and git remote returns failure
		_processRunner.RunAsync("git", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult());

		WingetOptions options = CreateDefaultOptions();
		options.GitHubRepo = null;

		WingetManifestResult result = await _service.GenerateManifestsAsync(options).ConfigureAwait(false);

		Assert.IsFalse(result.Success);
		Assert.IsNotNull(result.Error);
	}

	[TestMethod]
	public async Task GenerateManifestsAsync_WithLocalHashes_GeneratesManifests()
	{
		// Create an exe project
		string projDir = Path.Combine(_tempDir, "MyApp");
		Directory.CreateDirectory(projDir);
		await File.WriteAllTextAsync(Path.Combine(projDir, "MyApp.csproj"),
			"<Project><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>").ConfigureAwait(false);

		// Create staging with hashes
		string stagingDir = Path.Combine(_tempDir, "staging");
		Directory.CreateDirectory(stagingDir);
		await File.WriteAllTextAsync(Path.Combine(stagingDir, "hashes.txt"),
			"MyApp-1.0.0-win-x64.zip=ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890\n").ConfigureAwait(false);

		string outputDir = Path.Combine(_tempDir, "winget");
		WingetOptions options = new()
		{
			Version = "1.0.0",
			GitHubRepo = "testowner/MyApp",
			RootDirectory = _tempDir,
			OutputDirectory = outputDir,
			StagingDirectory = stagingDir,
		};

		WingetManifestResult result = await _service.GenerateManifestsAsync(options).ConfigureAwait(false);

		Assert.IsTrue(result.Success);
		Assert.IsFalse(result.IsLibraryOnly);
		Assert.IsTrue(result.ManifestFiles.Count > 0, "Should generate manifest files");
	}

	[TestMethod]
	public async Task GenerateManifestsAsync_DetectsGitHubRepoFromRemote_Https()
	{
		// Create an exe project
		string projDir = Path.Combine(_tempDir, "MyApp");
		Directory.CreateDirectory(projDir);
		await File.WriteAllTextAsync(Path.Combine(projDir, "MyApp.csproj"),
			"<Project><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>").ConfigureAwait(false);

		_processRunner.RunAsync("git", "remote get-url origin", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("https://github.com/testowner/testrepo.git"));

		string stagingDir = Path.Combine(_tempDir, "staging");
		Directory.CreateDirectory(stagingDir);
		await File.WriteAllTextAsync(Path.Combine(stagingDir, "hashes.txt"),
			"testrepo-1.0.0-win-x64.zip=ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890\n").ConfigureAwait(false);

		WingetOptions options = new()
		{
			Version = "1.0.0",
			GitHubRepo = null,
			RootDirectory = _tempDir,
			OutputDirectory = Path.Combine(_tempDir, "winget"),
			StagingDirectory = stagingDir,
		};

		WingetManifestResult result = await _service.GenerateManifestsAsync(options).ConfigureAwait(false);

		Assert.IsTrue(result.Success);
		Assert.AreEqual("testowner.testrepo", result.PackageId);
	}

	[TestMethod]
	public async Task GenerateManifestsAsync_DetectsGitHubRepoFromRemote_Ssh()
	{
		string projDir = Path.Combine(_tempDir, "MyApp");
		Directory.CreateDirectory(projDir);
		await File.WriteAllTextAsync(Path.Combine(projDir, "MyApp.csproj"),
			"<Project><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>").ConfigureAwait(false);

		_processRunner.RunAsync("git", "remote get-url origin", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("git@github.com:sshowner/sshrepo.git"));

		string stagingDir = Path.Combine(_tempDir, "staging");
		Directory.CreateDirectory(stagingDir);
		await File.WriteAllTextAsync(Path.Combine(stagingDir, "hashes.txt"),
			"sshrepo-1.0.0-win-x64.zip=ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890\n").ConfigureAwait(false);

		WingetOptions options = new()
		{
			Version = "1.0.0",
			GitHubRepo = null,
			RootDirectory = _tempDir,
			OutputDirectory = Path.Combine(_tempDir, "winget"),
			StagingDirectory = stagingDir,
		};

		WingetManifestResult result = await _service.GenerateManifestsAsync(options).ConfigureAwait(false);

		Assert.IsTrue(result.Success);
		Assert.AreEqual("sshowner.sshrepo", result.PackageId);
	}

	// UploadManifestsAsync

	[TestMethod]
	public async Task UploadManifestsAsync_NoManifestFiles_ReturnsEarly()
	{
		string manifestDir = Path.Combine(_tempDir, "manifests");
		Directory.CreateDirectory(manifestDir);

		await _service.UploadManifestsAsync("1.0.0", manifestDir).ConfigureAwait(false);

		await _processRunner.DidNotReceive().RunWithCallbackAsync("gh",
			Arg.Any<string>(),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task UploadManifestsAsync_WithManifestFiles_UploadsViaGh()
	{
		string manifestDir = Path.Combine(_tempDir, "manifests");
		Directory.CreateDirectory(manifestDir);
		await File.WriteAllTextAsync(Path.Combine(manifestDir, "test.yaml"), "manifest content").ConfigureAwait(false);

		_processRunner.RunWithCallbackAsync("gh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _service.UploadManifestsAsync("1.0.0", manifestDir).ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("gh",
			Arg.Is<string>(a => a.Contains("release upload v1.0.0")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task UploadManifestsAsync_UploadFailure_DoesNotThrow()
	{
		string manifestDir = Path.Combine(_tempDir, "manifests");
		Directory.CreateDirectory(manifestDir);
		await File.WriteAllTextAsync(Path.Combine(manifestDir, "test.yaml"), "manifest content").ConfigureAwait(false);

		_processRunner.RunWithCallbackAsync("gh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(1);

		// Should not throw
		await _service.UploadManifestsAsync("1.0.0", manifestDir).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task UploadManifestsAsync_UploadSuccess_Completes()
	{
		string manifestDir = Path.Combine(_tempDir, "manifests");
		Directory.CreateDirectory(manifestDir);
		await File.WriteAllTextAsync(Path.Combine(manifestDir, "pkg.yaml"), "content").ConfigureAwait(false);
		await File.WriteAllTextAsync(Path.Combine(manifestDir, "pkg.installer.yaml"), "content").ConfigureAwait(false);

		_processRunner.RunWithCallbackAsync("gh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _service.UploadManifestsAsync("2.0.0", manifestDir).ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("gh",
			Arg.Is<string>(a => a.Contains("release upload v2.0.0")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	private WingetOptions CreateDefaultOptions() => new()
	{
		Version = "1.0.0",
		RootDirectory = _tempDir,
		OutputDirectory = Path.Combine(_tempDir, "winget"),
	};
}
