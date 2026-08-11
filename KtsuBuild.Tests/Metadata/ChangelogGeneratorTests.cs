// Copyright (c) 2023-2026 ktsu-dev contributors

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
			lineEnding: "\n").ConfigureAwait(false);

		// Assert
		string changelogPath = Path.Combine(_tempDir, "CHANGELOG.md");
		string latestPath = Path.Combine(_tempDir, "LATEST_CHANGELOG.md");

		Assert.IsTrue(File.Exists(changelogPath), "CHANGELOG.md should be created");
		Assert.IsTrue(File.Exists(latestPath), "LATEST_CHANGELOG.md should be created");

		string changelogContent = await File.ReadAllTextAsync(changelogPath).ConfigureAwait(false);
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
			lineEnding: "\n").ConfigureAwait(false);

		// Assert
		string changelogPath = Path.Combine(_tempDir, "CHANGELOG.md");
		Assert.IsTrue(File.Exists(changelogPath));

		string content = await File.ReadAllTextAsync(changelogPath).ConfigureAwait(false);
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
			lineEnding: "\n").ConfigureAwait(false);

		// Assert
		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "CHANGELOG.md")).ConfigureAwait(false);
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
			lineEnding: "\n").ConfigureAwait(false);

		// Assert
		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "CHANGELOG.md")).ConfigureAwait(false);
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
			lineEnding: "\n").ConfigureAwait(false);

		// Assert
		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "CHANGELOG.md")).ConfigureAwait(false);
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
			latestChangelogFileName: "RELEASE_NOTES.md").ConfigureAwait(false);

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
			lineEnding: "\n").ConfigureAwait(false);

		// Assert
		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "CHANGELOG.md")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("Real fix"), "Should include normal commit");
		Assert.IsFalse(content.Contains("[skip ci]"), "Should filter skip ci commit");
	}

	[TestMethod]
	public async Task GenerateAsync_WhenEverySkipCiCommitIsFiltered_ReportsNoChanges()
	{
		// Regression test. The existing filter tests all leave a real commit behind, so the
		// filtered list is never empty and this path never ran. When the only commits in range are
		// [skip ci] ones, the count used to be taken before that exclusion, which emitted a
		// "Changes since" header above an empty list.
		_gitService.GetTagsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(["v1.0.0"]));
		_gitService.GetTagCommitHashAsync(Arg.Any<string>(), "v1.0.0", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("abc111"));
		_gitService.GetCommitsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<CommitInfo>>([
				new CommitInfo { Hash = "aaa", Subject = "Tidy up [skip ci]", Author = "developer" },
			]));

		await _generator.GenerateAsync(
			version: "1.1.0",
			commitHash: "abc123",
			workingDirectory: "/repo",
			outputPath: _tempDir,
			lineEnding: "\n").ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "CHANGELOG.md")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("No significant changes detected"), "Should report that nothing happened.");
		Assert.IsFalse(content.Contains("Changes since"), "Should not write a header with no entries under it.");
	}

	[TestMethod]
	public async Task GenerateAsync_WhenOnlyTheBotsOwnMetadataCommitIsInRange_ReportsNoChanges()
	{
		// The production case. After a release the only commit since the tag is the pipeline's own
		// metadata commit, which is both a bot commit and a [skip ci] commit. Level 1 filtering
		// drops it, which empties the list, so the progressive relaxation puts it straight back.
		_gitService.GetTagsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(["v1.0.0"]));
		_gitService.GetTagCommitHashAsync(Arg.Any<string>(), "v1.0.0", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("abc111"));
		_gitService.GetCommitsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<CommitInfo>>([
				new CommitInfo { Hash = "aaa", Subject = "[bot][skip ci] Update Metadata", Author = "github-actions[bot]" },
			]));

		await _generator.GenerateAsync(
			version: "1.1.0",
			commitHash: "abc123",
			workingDirectory: "/repo",
			outputPath: _tempDir,
			lineEnding: "\n").ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "CHANGELOG.md")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("No significant changes detected"), "Should report that nothing happened.");
		Assert.IsFalse(content.Contains("Changes since"), "Should not write a header with no entries under it.");
		Assert.IsFalse(content.Contains("Update Metadata"), "Should not list the pipeline's own commit.");
	}

	[TestMethod]
	public async Task GenerateAsync_RepeatedRunsOverTheSameHistoryAreStable()
	{
		// The churn this fixes was a file that changed every run, each change triggering another
		// metadata commit. Generating twice over identical history must produce identical output.
		_gitService.GetTagsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(["v1.0.0"]));
		_gitService.GetTagCommitHashAsync(Arg.Any<string>(), "v1.0.0", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("abc111"));
		_gitService.GetCommitsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<CommitInfo>>([
				new CommitInfo { Hash = "aaa", Subject = "[bot][skip ci] Update Metadata", Author = "github-actions[bot]" },
			]));

		await _generator.GenerateAsync("1.1.0", "abc123", "/repo", _tempDir, lineEnding: "\n").ConfigureAwait(false);
		string first = await File.ReadAllTextAsync(Path.Combine(_tempDir, "CHANGELOG.md")).ConfigureAwait(false);

		await _generator.GenerateAsync("1.1.0", "abc123", "/repo", _tempDir, lineEnding: "\n").ConfigureAwait(false);
		string second = await File.ReadAllTextAsync(Path.Combine(_tempDir, "CHANGELOG.md")).ConfigureAwait(false);

		Assert.AreEqual(first, second, "Regenerating over unchanged history should not change the file.");
	}

	[TestMethod]
	public async Task GenerateAsync_MergeOnlyHistoryStillFallsBackToShowingCommits()
	{
		// Guards the progressive relaxation itself. Merge commits are excluded at levels 1 and 2,
		// so only the unfiltered level 3 fallback can surface them, and removing the unreachable
		// level 4 must not have disturbed that.
		_gitService.GetTagsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(["v1.0.0"]));
		_gitService.GetTagCommitHashAsync(Arg.Any<string>(), "v1.0.0", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("abc111"));
		_gitService.GetCommitsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<CommitInfo>>([
				new CommitInfo { Hash = "aaa", Subject = "Merge pull request #7 from feature", Author = "developer" },
			]));

		await _generator.GenerateAsync(
			version: "1.1.0",
			commitHash: "abc123",
			workingDirectory: "/repo",
			outputPath: _tempDir,
			lineEnding: "\n").ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "CHANGELOG.md")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("Merge pull request #7"), "Level 3 should still surface a merge only history.");
	}
}
