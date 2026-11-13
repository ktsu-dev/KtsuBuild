// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Publishing;

using KtsuBuild.Abstractions;
using KtsuBuild.Configuration;
using KtsuBuild.DotNet;
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

	public TestContext TestContext { get; set; } = null!;

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
	[DoNotParallelize]
	public async Task ExecuteReleaseAsync_ResolvesChangelogsAgainstTheWorkspace_NotTheCurrentDirectory()
	{
		// Run from a directory that has its own LATEST_CHANGELOG.md, as running from the KtsuBuild
		// checkout does: neither that file nor the bare name may reach the pack or the release.
		string elsewhere = TestHelpers.CreateTempDir("ReleaseSvcCwd");
		await File.WriteAllTextAsync(Path.Combine(elsewhere, "LATEST_CHANGELOG.md"), "someone else's notes", TestContext.CancellationToken).ConfigureAwait(false);
		string original = Directory.GetCurrentDirectory();
		ReleaseOptions? captured = null;
		_gitHubService.CreateReleaseAsync(Arg.Do<ReleaseOptions>(o => captured = o), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);

		try
		{
			Directory.SetCurrentDirectory(elsewhere);
			await _service.ExecuteReleaseAsync(CreateDefaultConfig(), _tempDir, "Release", TestContext.CancellationToken).ConfigureAwait(false);
		}
		finally
		{
			Directory.SetCurrentDirectory(original);
			Directory.Delete(elsewhere, recursive: true);
		}

		await _dotNetService.Received(1).PackAsync(
			_tempDir, Arg.Any<string>(), "Release", Path.Combine(_tempDir, "LATEST_CHANGELOG.md"), Arg.Any<CancellationToken>()).ConfigureAwait(false);
		Assert.IsNotNull(captured);
		Assert.AreEqual(Path.Combine(_tempDir, "LATEST_CHANGELOG.md"), captured.LatestChangelogFile);
		Assert.AreEqual(Path.Combine(_tempDir, "CHANGELOG.md"), captured.ChangelogFile);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_KeepsRootedChangelogPathsAsGiven()
	{
		string rooted = Path.Combine(_tempDir, "notes", "NOTES.md");
		BuildConfiguration config = CreateDefaultConfig();
		config.LatestChangelogFile = rooted;

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release", TestContext.CancellationToken).ConfigureAwait(false);

		await _dotNetService.Received(1).PackAsync(
			_tempDir, Arg.Any<string>(), "Release", rooted, Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_PublishesExecutableProjects_ForAllRuntimes()
	{
		string projPath = Path.Combine(_tempDir, "MyApp", "MyApp.csproj");
		Directory.CreateDirectory(Path.GetDirectoryName(projPath)!);
		await File.WriteAllTextAsync(projPath, "<Project />").ConfigureAwait(false);

		_dotNetService.GetProjectFiles(Arg.Any<string>()).Returns([projPath]);
		_dotNetService.IsExecutableProject(projPath).Returns(true);
		_dotNetService.PublishAsync(Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);

		BuildConfiguration config = CreateDefaultConfig();
		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		// 7 runtimes per executable project
		await _dotNetService.Received(7).PublishAsync(
			ArgMatch.NotNull<PublishOptions>(o => o.ProjectPath == projPath), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_PublishesToGitHub_WhenTokenAvailable()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.GithubToken = "token123";

		PackWrites("test.nupkg");

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _nuGetPublisher.Received(1).PublishToGitHubAsync(
			Arg.Any<string>(), config.GitHubOwner, config.GithubToken, Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	// A pack that fails has to stop the release before anything public happens: a package already
	// pushed or a tag and release already created for the version cannot be taken back, and a
	// release missing one of its packages is never retried.
	[TestMethod]
	public async Task ExecuteReleaseAsync_FailedPack_PublishesNothingAndCreatesNoRelease()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.GithubToken = "token123";
		config.NuGetApiKey = "nuget-key";
		config.KtsuPackageKey = "ktsu-key";
		Directory.CreateDirectory(config.StagingPath);
		await File.WriteAllTextAsync(Path.Combine(config.StagingPath, "Foo.1.3.0.nupkg"), "fake").ConfigureAwait(false);
		string project = Path.Combine(_tempDir, "App", "App.csproj");
		_dotNetService.GetProjectFiles(Arg.Any<string>()).Returns([project]);
		_dotNetService.IsExecutableProject(project).Returns(true);
		_dotNetService.PackAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromException(new InvalidOperationException("Pack failed for 1 project(s): Foo.Extensions (exit code 1)")));

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.ExecuteReleaseAsync(config, _tempDir, "Release")).ConfigureAwait(false);

		await _dotNetService.DidNotReceive().PublishAsync(Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _nuGetPublisher.DidNotReceive().PublishToGitHubAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _nuGetPublisher.DidNotReceive().PublishToNuGetOrgAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _nuGetPublisher.DidNotReceive().PublishToSourceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _gitHubService.DidNotReceive().CreateReleaseAsync(Arg.Any<ReleaseOptions>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _gitHubService.DidNotReceive().UploadReleaseAssetsAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_PublishesToNuGetOrg_WhenApiKeyAvailable()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.GithubToken = "token123";
		config.NuGetApiKey = "nuget-key";

		PackWrites("test.nupkg");

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

		PackWrites("test.nupkg");

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

		PackWrites("test.nupkg");

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _nuGetPublisher.DidNotReceive().PublishToGitHubAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_PublishesToNuGetOrg_WhenNoGitHubToken()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.GithubToken = string.Empty;
		config.NuGetApiKey = "k";

		PackWrites("test.nupkg");

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release", TestContext.CancellationToken).ConfigureAwait(false);

		await _nuGetPublisher.Received(1).PublishToNuGetOrgAsync(
			Arg.Any<string>(), "k", Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _nuGetPublisher.DidNotReceive().PublishToGitHubAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_PublishesToKtsuFeed_WhenNoGitHubToken()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.GithubToken = string.Empty;
		config.KtsuPackageKey = "ktsu-key";

		PackWrites("test.nupkg");

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release", TestContext.CancellationToken).ConfigureAwait(false);

		await _nuGetPublisher.Received(1).PublishToSourceAsync(
			Arg.Any<string>(), "https://packages.ktsu.dev/v3/index.json", "ktsu-key", Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _nuGetPublisher.DidNotReceive().PublishToGitHubAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_WarnsForEachFeedSkippedForAMissingCredential()
	{
		RecordingBuildLogger logger = new();
		ReleaseService service = new(_dotNetService, _nuGetPublisher, _gitHubService, logger);
		BuildConfiguration config = CreateDefaultConfig();
		config.GithubToken = string.Empty;
		config.NuGetApiKey = "k";
		config.KtsuPackageKey = string.Empty;

		PackWrites("test.nupkg");

		await service.ExecuteReleaseAsync(config, _tempDir, "Release", TestContext.CancellationToken).ConfigureAwait(false);

		Assert.IsTrue(RecordingBuildLogger.Any(logger.Warnings, "GitHub Packages"));
		Assert.IsTrue(RecordingBuildLogger.Any(logger.Warnings, "packages.ktsu.dev"));
		Assert.IsFalse(RecordingBuildLogger.Any(logger.Warnings, "nuget.org"));
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_CreatesGitHubRelease()
	{
		BuildConfiguration config = CreateDefaultConfig();

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _gitHubService.Received(1).CreateReleaseAsync(
			ArgMatch.NotNull<ReleaseOptions>(o => o.Version == "1.0.0" && o.CommitHash == "abc123"),
			Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_DetectsPrerelease_FromVersionString()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.Version = "1.0.0-pre.1";

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _gitHubService.Received(1).CreateReleaseAsync(
			ArgMatch.NotNull<ReleaseOptions>(o => o.IsPrerelease),
			Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_DetectsPrerelease_Alpha()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.Version = "2.0.0-alpha.1";

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _gitHubService.Received(1).CreateReleaseAsync(
			ArgMatch.NotNull<ReleaseOptions>(o => o.IsPrerelease),
			Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_DetectsPrerelease_Beta()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.Version = "2.0.0-beta.3";

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _gitHubService.Received(1).CreateReleaseAsync(
			ArgMatch.NotNull<ReleaseOptions>(o => o.IsPrerelease),
			Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_DetectsPrerelease_ReleaseCandidate()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.Version = "2.0.0-rc.1";

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _gitHubService.Received(1).CreateReleaseAsync(
			ArgMatch.NotNull<ReleaseOptions>(o => o.IsPrerelease),
			Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	[DataRow("1.2.4-dev.1")]
	[DataRow("1.2.4-preview.3")]
	[DataRow("1.2.4-rc1")]
	public async Task ExecuteReleaseAsync_DetectsPrerelease_AnyLabel(string version)
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.Version = version;

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _gitHubService.Received(1).CreateReleaseAsync(
			ArgMatch.NotNull<ReleaseOptions>(o => o.IsPrerelease),
			Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_NotPrerelease_ForStableVersion()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.Version = "1.0.0";

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _gitHubService.Received(1).CreateReleaseAsync(
			ArgMatch.NotNull<ReleaseOptions>(o => !o.IsPrerelease),
			Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	// A demo, sample, example, benchmark or test declares ktsu.Sdk.App exactly as a shipped
	// application does, so IsExecutableProject alone let seven RID zips of a non-deliverable onto the
	// repository's public release. ImGuiApp's examples/ImGuiAppDemo is the live case.

	[TestMethod]
	[DataRow("examples")]
	[DataRow("samples")]
	[DataRow("benchmarks")]
	[DataRow("demos")]
	[DataRow("tests")]
	public async Task ExecuteReleaseAsync_UnderASupportingDirectory_SkipsPublish(string directory)
	{
		string projPath = await WriteProjectAsync(Path.Combine(directory, "Showcase"), "Showcase.csproj").ConfigureAwait(false);
		StubExecutableProject(projPath);

		BuildConfiguration config = CreateDefaultConfig();
		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _dotNetService.DidNotReceive().PublishAsync(
			Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	[DataRow("Widget.Demo.csproj")]
	[DataRow("Widget.Sample.csproj")]
	[DataRow("Widget.Examples.csproj")]
	[DataRow("Widget.Benchmarks.csproj")]
	[DataRow("Widget.Tests.csproj")]
	public async Task ExecuteReleaseAsync_WithASupportingProjectName_SkipsPublish(string fileName)
	{
		string projPath = await WriteProjectAsync("Widget", fileName).ConfigureAwait(false);
		StubExecutableProject(projPath);

		BuildConfiguration config = CreateDefaultConfig();
		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _dotNetService.DidNotReceive().PublishAsync(
			Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_WithATestProjectTheNamingConventionMisses_SkipsPublish()
	{
		// ImGuiApp's five *.UITests projects are why the project itself is asked and not only its
		// path: a test project is free to be named anything.
		string projPath = await WriteProjectAsync("Widget.Verification", "Widget.Verification.csproj").ConfigureAwait(false);
		StubExecutableProject(projPath);
		_dotNetService.IsTestProject(projPath).Returns(true);

		BuildConfiguration config = CreateDefaultConfig();
		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _dotNetService.DidNotReceive().PublishAsync(
			Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_WhenTheWorkspaceSitsUnderASupportingDirectory_StillPublishes()
	{
		// Project discovery hands back absolute paths, so the exclusion is applied to the path
		// relative to the workspace. Judging the absolute path would let the directories above the
		// checkout decide, and a clone at ~/examples/MyApp would release nothing at all.
		string workspace = Path.Combine(_tempDir, "examples", "MyApp");
		string projPath = Path.Combine(workspace, "src", "MyApp.csproj");
		Directory.CreateDirectory(Path.GetDirectoryName(projPath)!);
		await File.WriteAllTextAsync(projPath, "<Project />").ConfigureAwait(false);
		StubExecutableProject(projPath);

		BuildConfiguration config = CreateDefaultConfig();
		await _service.ExecuteReleaseAsync(config, workspace, "Release").ConfigureAwait(false);

		await _dotNetService.Received(7).PublishAsync(
			ArgMatch.NotNull<PublishOptions>(o => o.ProjectPath == projPath), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_NoExecutableProjects_SkipsPublish()
	{
		_dotNetService.GetProjectFiles(Arg.Any<string>()).Returns(["lib.csproj"]);
		_dotNetService.IsExecutableProject("lib.csproj").Returns(false);

		BuildConfiguration config = CreateDefaultConfig();
		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		await _dotNetService.DidNotReceive().PublishAsync(
			Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_GeneratesHashesFile_ForZipArchives()
	{
		string projPath = await WriteProjectAsync("app", "app.csproj").ConfigureAwait(false);
		StubExecutableProject(projPath);
		PublishWrites("app.exe");
		BuildConfiguration config = CreateDefaultConfig();

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release").ConfigureAwait(false);

		string hashesPath = Path.Combine(config.StagingPath, "hashes.txt");
		Assert.IsTrue(File.Exists(hashesPath), "hashes.txt should be created");
		string content = await File.ReadAllTextAsync(hashesPath).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("app-1.0.0-win-x64.zip="), "Should contain filename=hash entry");
	}

	// staging/ and output/ are fixed paths under the workspace. A second release in a reused checkout
	// (a local run, or a self-hosted runner) found the previous version's files still there, and
	// everything after pack globs staging/: the old packages were pushed again, the old zips were
	// hashed into hashes.txt for winget, and both were attached to the new GitHub release.

	[TestMethod]
	public async Task ExecuteReleaseAsync_InAReusedWorkspace_PublishesHashesAndUploadsOnlyTheCurrentVersion()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.Version = "1.2.4";
		config.GithubToken = "token123";
		config.NuGetApiKey = "nuget-key";
		Directory.CreateDirectory(config.StagingPath);
		await File.WriteAllTextAsync(Path.Combine(config.StagingPath, "Foo.1.2.3.nupkg"), "old", TestContext.CancellationToken).ConfigureAwait(false);
		await File.WriteAllTextAsync(Path.Combine(config.StagingPath, "Foo-1.2.3-win-x64.zip"), "old", TestContext.CancellationToken).ConfigureAwait(false);

		string projPath = await WriteProjectAsync("Foo", "Foo.csproj").ConfigureAwait(false);
		StubExecutableProject(projPath);
		PublishWrites("Foo.exe");
		PackWrites("Foo.1.2.4.nupkg");

		string[]? stagedWhenPushed = null;
		_nuGetPublisher.PublishToNuGetOrgAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(_ =>
			{
				stagedWhenPushed = StagedFileNames(config);
				return Task.CompletedTask;
			});
		string[]? stagedWhenReleased = null;
		_gitHubService.CreateReleaseAsync(Arg.Any<ReleaseOptions>(), Arg.Any<CancellationToken>())
			.Returns(_ =>
			{
				stagedWhenReleased = StagedFileNames(config);
				return Task.CompletedTask;
			});

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release", TestContext.CancellationToken).ConfigureAwait(false);

		Assert.IsNotNull(stagedWhenPushed);
		Assert.IsNotNull(stagedWhenReleased);
		CollectionAssert.DoesNotContain(stagedWhenPushed, "Foo.1.2.3.nupkg");
		CollectionAssert.Contains(stagedWhenPushed, "Foo.1.2.4.nupkg");
		CollectionAssert.DoesNotContain(stagedWhenReleased, "Foo.1.2.3.nupkg");
		CollectionAssert.DoesNotContain(stagedWhenReleased, "Foo-1.2.3-win-x64.zip");
		CollectionAssert.Contains(stagedWhenReleased, "Foo-1.2.4-win-x64.zip");

		string hashes = await File.ReadAllTextAsync(Path.Combine(config.StagingPath, "hashes.txt"), TestContext.CancellationToken).ConfigureAwait(false);
		Assert.DoesNotContain("1.2.3", hashes);
		Assert.Contains("Foo-1.2.4-win-x64.zip=", hashes);
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_InAReusedWorkspace_ZipsOnlyWhatThisPublishWrote()
	{
		BuildConfiguration config = CreateDefaultConfig();
		string staleDir = Path.Combine(config.OutputPath, "Foo-win-x64");
		Directory.CreateDirectory(staleDir);
		await File.WriteAllTextAsync(Path.Combine(staleDir, "Removed.dll"), "left by an earlier publish", TestContext.CancellationToken).ConfigureAwait(false);

		string projPath = await WriteProjectAsync("Foo", "Foo.csproj").ConfigureAwait(false);
		StubExecutableProject(projPath);
		PublishWrites("Foo.exe");

		await _service.ExecuteReleaseAsync(config, _tempDir, "Release", TestContext.CancellationToken).ConfigureAwait(false);

		System.IO.Compression.ZipArchive zip = await System.IO.Compression.ZipFile.OpenReadAsync(Path.Combine(config.StagingPath, "Foo-1.0.0-win-x64.zip"), TestContext.CancellationToken).ConfigureAwait(false);
		string[] entries;
		await using (zip.ConfigureAwait(false))
		{
			entries = [.. zip.Entries.Select(e => e.FullName)];
		}

		CollectionAssert.Contains(entries, "Foo.exe");
		CollectionAssert.DoesNotContain(entries, "Removed.dll");
	}

	[TestMethod]
	public async Task ExecuteReleaseAsync_WithTheWorkspaceAsStagingPath_RefusesAndDeletesNothing()
	{
		BuildConfiguration config = CreateDefaultConfig();
		config.StagingPath = _tempDir;
		string keep = Path.Combine(_tempDir, "Keep.cs");
		await File.WriteAllTextAsync(keep, "// source", TestContext.CancellationToken).ConfigureAwait(false);

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.ExecuteReleaseAsync(config, _tempDir, "Release", TestContext.CancellationToken)).ConfigureAwait(false);

		Assert.IsTrue(File.Exists(keep));
		await _dotNetService.DidNotReceive().PackAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	/// <summary>
	/// Makes the stubbed pack write the named packages into the staging directory it is handed, as
	/// <c>dotnet pack</c> does.
	/// </summary>
	private void PackWrites(params string[] fileNames) =>
		_dotNetService.PackAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(async call =>
			{
				string staging = call.ArgAt<string>(1);
				Directory.CreateDirectory(staging);
				foreach (string fileName in fileNames)
				{
					await File.WriteAllTextAsync(Path.Combine(staging, fileName), "package").ConfigureAwait(false);
				}
			});

	/// <summary>
	/// Makes the stubbed publish write the named files into the output directory it is handed, as
	/// <c>dotnet publish</c> does.
	/// </summary>
	private void PublishWrites(params string[] fileNames) =>
		_dotNetService.PublishAsync(Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>())
			.Returns(async call =>
			{
				PublishOptions options = call.ArgAt<PublishOptions>(0);
				Directory.CreateDirectory(options.OutputPath);
				foreach (string fileName in fileNames)
				{
					await File.WriteAllTextAsync(Path.Combine(options.OutputPath, fileName), "binary").ConfigureAwait(false);
				}
			});

	private static string[] StagedFileNames(BuildConfiguration config) =>
		[.. Directory.GetFiles(config.StagingPath).Select(Path.GetFileName).OfType<string>()];

	/// <summary>
	/// Writes an empty project file at a path relative to the workspace and returns its full path.
	/// </summary>
	private async Task<string> WriteProjectAsync(string relativeDirectory, string fileName)
	{
		string projPath = Path.Combine(_tempDir, relativeDirectory, fileName);
		Directory.CreateDirectory(Path.GetDirectoryName(projPath)!);
		await File.WriteAllTextAsync(projPath, "<Project />", TestContext.CancellationToken).ConfigureAwait(false);
		return projPath;
	}

	/// <summary>
	/// Presents a single project to the release as an executable the publish step would pick up.
	/// </summary>
	private void StubExecutableProject(string projPath)
	{
		_dotNetService.GetProjectFiles(Arg.Any<string>()).Returns([projPath]);
		_dotNetService.IsExecutableProject(projPath).Returns(true);
		_dotNetService.PublishAsync(Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);
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
