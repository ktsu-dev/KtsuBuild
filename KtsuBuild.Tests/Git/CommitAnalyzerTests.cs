// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Git;

using KtsuBuild.Abstractions;
using KtsuBuild.Git;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

[TestClass]
public class CommitAnalyzerTests
{
	private IGitService _gitService = null!;
	private CommitAnalyzer _analyzer = null!;

	[TestInitialize]
	public void Setup()
	{
		_gitService = Substitute.For<IGitService>();
		_analyzer = new CommitAnalyzer(_gitService);
	}

	[TestMethod]
	public async Task AnalyzeAsync_NoCommitsInRange_ReturnsSkip()
	{
		// Arrange
		_gitService.GetCommitMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>([]));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Skip, type);
		Assert.IsTrue(reason.Contains("No commits found"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_AllCommitsHaveSkipCi_ReturnsSkip()
	{
		// Arrange
		List<string> messages =
		[
			"Fix typo [skip ci]",
			"Update docs [ci skip]",
		];
		_gitService.GetCommitMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(messages));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Skip, type);
		Assert.IsTrue(reason.Contains("[skip ci]"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_CommitWithMajorTag_ReturnsMajor()
	{
		// Arrange
		List<string> messages =
		[
			"Breaking change [major]",
			"Fix bug",
		];
		_gitService.GetCommitMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(messages));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Major, type);
		Assert.IsTrue(reason.Contains("[major]"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_CommitWithMinorTag_ReturnsMinor()
	{
		// Arrange
		List<string> messages =
		[
			"Add new feature [minor]",
			"Fix bug",
		];
		_gitService.GetCommitMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(messages));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Minor, type);
		Assert.IsTrue(reason.Contains("[minor]"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_CommitWithPatchTag_ReturnsPatch()
	{
		// Arrange
		List<string> messages =
		[
			"Fix critical bug [patch]",
		];
		_gitService.GetCommitMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(messages));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Patch, type);
		Assert.IsTrue(reason.Contains("[patch]"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_CommitWithPreTag_ReturnsPrerelease()
	{
		// Arrange
		List<string> messages =
		[
			"Experimental feature [pre]",
		];
		_gitService.GetCommitMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(messages));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Prerelease, type);
		Assert.IsTrue(reason.Contains("[pre]"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_MajorTakesPrecedenceOverMinor()
	{
		// Arrange
		List<string> messages =
		[
			"New feature [minor]",
			"Breaking change [major]",
		];
		_gitService.GetCommitMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(messages));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Major, type);
		Assert.IsTrue(reason.Contains("[major]"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_BotCommitsAreFiltered_ReturnsPrerelease()
	{
		// Arrange
		List<string> messages =
		[
			"Update by [bot]",
			"github automated change",
			"ProjectDirector update",
		];
		_gitService.GetCommitMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(messages));
		_gitService.GetDiffAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(string.Empty));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Prerelease, type);
		Assert.IsTrue(reason.Contains("No significant changes"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_PrMergeCommitsFiltered_ReturnsPrerelease()
	{
		// Arrange
		List<string> messages =
		[
			"Merge pull request #123 from feature",
			"Merge branch 'main' into release",
		];
		_gitService.GetCommitMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(messages));
		_gitService.GetDiffAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(string.Empty));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Prerelease, type);
	}

	[TestMethod]
	public async Task AnalyzeAsync_DiffAddsPublicClass_ReturnsMinor()
	{
		// Arrange
		List<string> messages =
		[
			"Add new service class",
		];
		string diff = @"
+public class NewService
+{
+    public void DoWork() { }
+}
";
		_gitService.GetCommitMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(messages));
		_gitService.GetDiffAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(diff));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Minor, type);
		Assert.IsTrue(reason.Contains("API changes"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_DiffModifiesInternalCodeOnly_ReturnsPatch()
	{
		// Arrange
		List<string> messages =
		[
			"Optimize internal algorithm",
		];
		string diff = @"
-internal int Calculate() => x + y;
+internal int Calculate() => x * 2 + y;
";
		_gitService.GetCommitMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(messages));
		_gitService.GetDiffAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(diff));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Patch, type);
		Assert.IsTrue(reason.Contains("patch"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_MinorTakesPrecedenceOverPatch()
	{
		// Arrange
		List<string> messages =
		[
			"Fix bug [patch]",
			"Add feature [minor]",
		];
		_gitService.GetCommitMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(messages));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Minor, type);
	}

	[TestMethod]
	public async Task AnalyzeAsync_PatchTakesPrecedenceOverPre()
	{
		// Arrange
		List<string> messages =
		[
			"Experimental [pre]",
			"Important fix [patch]",
		];
		_gitService.GetCommitMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(messages));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Patch, type);
	}

	[TestMethod]
	public async Task AnalyzeAsync_CaseInsensitiveTags()
	{
		// Arrange
		List<string> messages =
		[
			"Breaking change [MAJOR]",
		];
		_gitService.GetCommitMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(messages));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Major, type);
	}
}
