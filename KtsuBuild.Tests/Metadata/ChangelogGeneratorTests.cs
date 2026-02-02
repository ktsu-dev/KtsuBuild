// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Metadata;

using KtsuBuild.Abstractions;
using KtsuBuild.Git;
using KtsuBuild.Metadata;
using KtsuBuild.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

[TestClass]
public class ChangelogGeneratorTests
{
	private IGitService _gitService = null!;
	private IBuildLogger _logger = null!;
	private ChangelogGenerator _generator = null!;
	private string _tempDir = null!;

	[TestInitialize]
	public void Setup()
	{
		_gitService = Substitute.For<IGitService>();
		_logger = new MockBuildLogger();
		_generator = new ChangelogGenerator(_gitService, _logger);
		_tempDir = Path.Combine(Path.GetTempPath(), $"ChangelogTest_{Guid.NewGuid():N}");
		Directory.CreateDirectory(_tempDir);
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
	public async Task GenerateAsync_WithCommits_CreatesChangelogFiles()
	{
		// Arrange
		_gitService.GetTagsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(["v1.0.0"]));
		_gitService.GetTagCommitHashAsync(Arg.Any<string>(), "v1.0.0", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("abc111"));
		_gitService.GetCommitsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<CommitInfo>>([
				new CommitInfo { Hash = "def222", Subject = "Add new feature", Author = "testuser" },
				new CommitInfo { Hash = "ghi333", Subject = "Fix bug", Author = "testuser" },
			]));

		// Act
		await _generator.GenerateAsync(
			version: "1.1.0",
			commitHash: "abc123",
			workingDirectory: "/repo",
			outputPath: _tempDir,
			lineEnding: "\n");

		// Assert
		string changelogPath = Path.Combine(_tempDir, "CHANGELOG.md");
		string latestPath = Path.Combine(_tempDir, "LATEST_CHANGELOG.md");

		Assert.IsTrue(File.Exists(changelogPath), "CHANGELOG.md should be created");
		Assert.IsTrue(File.Exists(latestPath), "LATEST_CHANGELOG.md should be created");

		string changelogContent = await File.ReadAllTextAsync(changelogPath);
		Assert.IsTrue(changelogContent.Contains("v1.1.0"), "Should contain new version");
		Assert.IsTrue(changelogContent.Contains("Add new feature"), "Should contain commit message");
	}

	[TestMethod]
	public async Task GenerateAsync_EmptyHistory_CreatesMinimalChangelog()
	{
		// Arrange
		_gitService.GetTagsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>([]));
		_gitService.GetCommitsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<CommitInfo>>([]));

		// Act
		await _generator.GenerateAsync(
			version: "1.0.0",
			commitHash: "abc123",
			workingDirectory: "/repo",
			outputPath: _tempDir,
			lineEnding: "\n");

		// Assert
		string changelogPath = Path.Combine(_tempDir, "CHANGELOG.md");
		Assert.IsTrue(File.Exists(changelogPath));

		string content = await File.ReadAllTextAsync(changelogPath);
		Assert.IsTrue(content.Contains("v1.0.0"));
	}

	[TestMethod]
	public async Task GenerateAsync_FiltersBotCommits()
	{
		// Arrange
		_gitService.GetTagsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(["v1.0.0"]));
		_gitService.GetTagCommitHashAsync(Arg.Any<string>(), "v1.0.0", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("abc111"));
		_gitService.GetCommitsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<CommitInfo>>([
				new CommitInfo { Hash = "aaa", Subject = "Real commit", Author = "developer" },
				new CommitInfo { Hash = "bbb", Subject = "Update by [bot]", Author = "github-bot" },
				new CommitInfo { Hash = "ccc", Subject = "Merge pull request #123", Author = "developer" },
			]));

		// Act
		await _generator.GenerateAsync(
			version: "1.1.0",
			commitHash: "abc123",
			workingDirectory: "/repo",
			outputPath: _tempDir,
			lineEnding: "\n");

		// Assert
		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "CHANGELOG.md"));
		Assert.IsTrue(content.Contains("Real commit"), "Should include real commit");
		Assert.IsFalse(content.Contains("[bot]"), "Should filter bot commit");
		Assert.IsFalse(content.Contains("Merge pull request"), "Should filter PR merge");
	}

	[TestMethod]
	public async Task GenerateAsync_FormatsEntriesCorrectly()
	{
		// Arrange
		_gitService.GetTagsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(["v1.0.0"]));
		_gitService.GetTagCommitHashAsync(Arg.Any<string>(), "v1.0.0", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("abc111"));
		_gitService.GetCommitsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<CommitInfo>>([
				new CommitInfo { Hash = "def222", Subject = "Add awesome feature", Author = "developer" },
			]));

		// Act
		await _generator.GenerateAsync(
			version: "1.1.0",
			commitHash: "abc123",
			workingDirectory: "/repo",
			outputPath: _tempDir,
			lineEnding: "\n");

		// Assert
		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "CHANGELOG.md"));
		Assert.IsTrue(content.Contains("- Add awesome feature"), "Should format as bullet point");
		Assert.IsTrue(content.Contains("[@developer]"), "Should include author link");
	}

	[TestMethod]
	public async Task GenerateAsync_DetectsVersionType()
	{
		// Arrange
		_gitService.GetTagsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(["v1.0.0"]));
		_gitService.GetTagCommitHashAsync(Arg.Any<string>(), "v1.0.0", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("abc111"));
		_gitService.GetCommitsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<CommitInfo>>([
				new CommitInfo { Hash = "def222", Subject = "Add feature", Author = "developer" },
			]));

		// Act
		await _generator.GenerateAsync(
			version: "1.1.0",
			commitHash: "abc123",
			workingDirectory: "/repo",
			outputPath: _tempDir,
			lineEnding: "\n");

		// Assert
		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "CHANGELOG.md"));
		Assert.IsTrue(content.Contains("(minor)"), "Should detect minor version bump");
	}

	[TestMethod]
	public async Task GenerateAsync_UsesCustomLatestChangelogFileName()
	{
		// Arrange
		_gitService.GetTagsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>([]));
		_gitService.GetCommitsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<CommitInfo>>([]));

		// Act
		await _generator.GenerateAsync(
			version: "1.0.0",
			commitHash: "abc123",
			workingDirectory: "/repo",
			outputPath: _tempDir,
			lineEnding: "\n",
			latestChangelogFileName: "RELEASE_NOTES.md");

		// Assert
		string customPath = Path.Combine(_tempDir, "RELEASE_NOTES.md");
		Assert.IsTrue(File.Exists(customPath), "Custom latest changelog file should be created");
	}

	[TestMethod]
	public async Task GenerateAsync_FiltersSkipCiCommits()
	{
		// Arrange
		_gitService.GetTagsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(["v1.0.0"]));
		_gitService.GetTagCommitHashAsync(Arg.Any<string>(), "v1.0.0", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("abc111"));
		_gitService.GetCommitsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<CommitInfo>>([
				new CommitInfo { Hash = "aaa", Subject = "Real fix", Author = "developer" },
				new CommitInfo { Hash = "bbb", Subject = "Update docs [skip ci]", Author = "developer" },
			]));

		// Act
		await _generator.GenerateAsync(
			version: "1.1.0",
			commitHash: "abc123",
			workingDirectory: "/repo",
			outputPath: _tempDir,
			lineEnding: "\n");

		// Assert
		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "CHANGELOG.md"));
		Assert.IsTrue(content.Contains("Real fix"), "Should include normal commit");
		Assert.IsFalse(content.Contains("[skip ci]"), "Should filter skip ci commit");
	}
}
