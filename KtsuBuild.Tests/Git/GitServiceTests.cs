// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Git;

using KtsuBuild.Abstractions;
using KtsuBuild.Git;
using KtsuBuild.Tests.Helpers;
using KtsuBuild.Tests.Mocks;

using NSubstitute;

[TestClass]
public class GitServiceTests
{
	private IProcessRunner _processRunner = null!;
	private GitService _service = null!;

	[TestInitialize]
	public void Setup()
	{
		_processRunner = Substitute.For<IProcessRunner>();
		_service = new GitService(_processRunner, new MockBuildLogger());
	}

	// GetTagsAsync

	[TestMethod]
	public async Task GetTagsAsync_SuccessWithTags_ReturnsTagList()
	{
		_processRunner.RunAsync("git", Arg.Is<string>(a => a.StartsWith("config")), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult());
		_processRunner.RunAsync("git", "tag --list --sort=-v:refname", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("v2.0.0\nv1.1.0\nv1.0.0\n"));

		IReadOnlyList<string> tags = await _service.GetTagsAsync("/repo").ConfigureAwait(false);

		Assert.AreEqual(3, tags.Count);
		Assert.AreEqual("v2.0.0", tags[0]);
		Assert.AreEqual("v1.1.0", tags[1]);
		Assert.AreEqual("v1.0.0", tags[2]);
	}

	[TestMethod]
	public async Task GetTagsAsync_NoTags_ReturnsEmptyList()
	{
		_processRunner.RunAsync("git", Arg.Is<string>(a => a.StartsWith("config")), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult());
		_processRunner.RunAsync("git", "tag --list --sort=-v:refname", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult(""));

		IReadOnlyList<string> tags = await _service.GetTagsAsync("/repo").ConfigureAwait(false);

		Assert.AreEqual(0, tags.Count);
	}

	[TestMethod]
	public async Task GetTagsAsync_FailedProcess_ReturnsEmptyList()
	{
		_processRunner.RunAsync("git", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult());
		_processRunner.RunAsync("git", "tag --list --sort=-v:refname", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult());

		IReadOnlyList<string> tags = await _service.GetTagsAsync("/repo").ConfigureAwait(false);

		Assert.AreEqual(0, tags.Count);
	}

	[TestMethod]
	public async Task GetTagsAsync_ConfiguresVersionSortSuffixes()
	{
		_processRunner.RunAsync("git", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult());

		await _service.GetTagsAsync("/repo").ConfigureAwait(false);

		// Verify 4 config commands were called for versionsort suffixes
		await _processRunner.Received(1).RunAsync("git", Arg.Is<string>(a => a.Contains("-alpha")), Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _processRunner.Received(1).RunAsync("git", Arg.Is<string>(a => a.Contains("-beta")), Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _processRunner.Received(1).RunAsync("git", Arg.Is<string>(a => a.Contains("-rc")), Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _processRunner.Received(1).RunAsync("git", Arg.Is<string>(a => a.Contains("-pre")), Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	// GetCurrentCommitHashAsync

	[TestMethod]
	public async Task GetCurrentCommitHashAsync_Success_ReturnsTrimmedHash()
	{
		_processRunner.RunAsync("git", "rev-parse HEAD", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("  abc123def456  \n"));

		string hash = await _service.GetCurrentCommitHashAsync("/repo").ConfigureAwait(false);

		Assert.AreEqual("abc123def456", hash);
	}

	// GetTagCommitHashAsync

	[TestMethod]
	public async Task GetTagCommitHashAsync_Success_ReturnsTrimmedHash()
	{
		_processRunner.RunAsync("git", "rev-list -n 1 v1.0.0", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("  abc123  \n"));

		string? hash = await _service.GetTagCommitHashAsync("/repo", "v1.0.0").ConfigureAwait(false);

		Assert.AreEqual("abc123", hash);
	}

	[TestMethod]
	public async Task GetTagCommitHashAsync_Failure_ReturnsNull()
	{
		_processRunner.RunAsync("git", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult());

		string? hash = await _service.GetTagCommitHashAsync("/repo", "v1.0.0").ConfigureAwait(false);

		Assert.IsNull(hash);
	}

	// GetRemoteUrlAsync

	[TestMethod]
	public async Task GetRemoteUrlAsync_Success_ReturnsTrimmedUrl()
	{
		_processRunner.RunAsync("git", "remote get-url origin", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("  https://github.com/owner/repo.git  \n"));

		string? url = await _service.GetRemoteUrlAsync("/repo").ConfigureAwait(false);

		Assert.AreEqual("https://github.com/owner/repo.git", url);
	}

	[TestMethod]
	public async Task GetRemoteUrlAsync_Failure_ReturnsNull()
	{
		_processRunner.RunAsync("git", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult());

		string? url = await _service.GetRemoteUrlAsync("/repo").ConfigureAwait(false);

		Assert.IsNull(url);
	}

	// GetCommitMessagesAsync

	[TestMethod]
	public async Task GetCommitMessagesAsync_WithMessages_ReturnsParsedList()
	{
		_processRunner.RunAsync("git", Arg.Is<string>(a => a.StartsWith("log")), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("Fix bug\nAdd feature\nUpdate docs\n"));

		IReadOnlyList<string> messages = await _service.GetCommitMessagesAsync("/repo", "abc..def").ConfigureAwait(false);

		Assert.AreEqual(3, messages.Count);
		Assert.AreEqual("Fix bug", messages[0]);
		Assert.AreEqual("Add feature", messages[1]);
		Assert.AreEqual("Update docs", messages[2]);
	}

	[TestMethod]
	public async Task GetCommitMessagesAsync_EmptyOutput_ReturnsEmptyList()
	{
		_processRunner.RunAsync("git", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult(""));

		IReadOnlyList<string> messages = await _service.GetCommitMessagesAsync("/repo", "abc..def").ConfigureAwait(false);

		Assert.AreEqual(0, messages.Count);
	}

	[TestMethod]
	public async Task GetCommitMessagesAsync_Failure_ReturnsEmptyList()
	{
		_processRunner.RunAsync("git", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult());

		IReadOnlyList<string> messages = await _service.GetCommitMessagesAsync("/repo", "abc..def").ConfigureAwait(false);

		Assert.AreEqual(0, messages.Count);
	}

	// GetCommitsAsync

	[TestMethod]
	public async Task GetCommitsAsync_WithCommits_ReturnsParsedCommitInfoList()
	{
		_processRunner.RunAsync("git", Arg.Is<string>(a => a.StartsWith("log")), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("abc123|Fix bug|Alice\ndef456|Add feature|Bob\n"));

		IReadOnlyList<CommitInfo> commits = await _service.GetCommitsAsync("/repo", "abc..def").ConfigureAwait(false);

		Assert.AreEqual(2, commits.Count);
		Assert.AreEqual("abc123", commits[0].Hash);
		Assert.AreEqual("Fix bug", commits[0].Subject);
		Assert.AreEqual("Alice", commits[0].Author);
		Assert.AreEqual("def456", commits[1].Hash);
		Assert.AreEqual("Add feature", commits[1].Subject);
		Assert.AreEqual("Bob", commits[1].Author);
	}

	[TestMethod]
	public async Task GetCommitsAsync_MalformedLine_SkipsIncompleteEntries()
	{
		_processRunner.RunAsync("git", Arg.Is<string>(a => a.StartsWith("log")), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("abc123|Fix bug|Alice\nbadline\ndef456|Add feature|Bob\n"));

		IReadOnlyList<CommitInfo> commits = await _service.GetCommitsAsync("/repo", "abc..def").ConfigureAwait(false);

		Assert.AreEqual(2, commits.Count);
		Assert.AreEqual("abc123", commits[0].Hash);
		Assert.AreEqual("def456", commits[1].Hash);
	}

	[TestMethod]
	public async Task GetCommitsAsync_EmptyOutput_ReturnsEmptyList()
	{
		_processRunner.RunAsync("git", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult(""));

		IReadOnlyList<CommitInfo> commits = await _service.GetCommitsAsync("/repo", "abc..def").ConfigureAwait(false);

		Assert.AreEqual(0, commits.Count);
	}

	// GetDiffAsync

	[TestMethod]
	public async Task GetDiffAsync_WithPathSpec_IncludesPathInArgs()
	{
		_processRunner.RunAsync("git", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("diff content"));

		await _service.GetDiffAsync("/repo", "abc..def", "*.cs").ConfigureAwait(false);

		await _processRunner.Received(1).RunAsync("git",
			Arg.Is<string>(a => a.Contains("-- \"*.cs\"")),
			Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task GetDiffAsync_WithoutPathSpec_OmitsPathFromArgs()
	{
		_processRunner.RunAsync("git", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("diff content"));

		await _service.GetDiffAsync("/repo", "abc..def").ConfigureAwait(false);

		await _processRunner.Received(1).RunAsync("git",
			Arg.Is<string>(a => !a.Contains("--")),
			Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task GetDiffAsync_ReturnsStandardOutput()
	{
		_processRunner.RunAsync("git", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("+ added line\n- removed line"));

		string diff = await _service.GetDiffAsync("/repo", "abc..def").ConfigureAwait(false);

		Assert.AreEqual("+ added line\n- removed line", diff);
	}

	// IsCommitTaggedAsync

	[TestMethod]
	public async Task IsCommitTaggedAsync_CommitInTags_ReturnsTrue()
	{
		_processRunner.RunAsync("git", "show-ref --tags -d", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("abc123 refs/tags/v1.0.0\ndef456 refs/tags/v2.0.0\n"));

		bool result = await _service.IsCommitTaggedAsync("/repo", "abc123").ConfigureAwait(false);

		Assert.IsTrue(result);
	}

	[TestMethod]
	public async Task IsCommitTaggedAsync_CommitNotInTags_ReturnsFalse()
	{
		_processRunner.RunAsync("git", "show-ref --tags -d", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("abc123 refs/tags/v1.0.0\n"));

		bool result = await _service.IsCommitTaggedAsync("/repo", "999999").ConfigureAwait(false);

		Assert.IsFalse(result);
	}

	// GetFirstCommitAsync

	[TestMethod]
	public async Task GetFirstCommitAsync_WithCommits_ReturnsLastEntry()
	{
		_processRunner.RunAsync("git", "rev-list HEAD", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("abc123\ndef456\nghi789\n"));

		string firstCommit = await _service.GetFirstCommitAsync("/repo").ConfigureAwait(false);

		Assert.AreEqual("ghi789", firstCommit);
	}

	[TestMethod]
	public async Task GetFirstCommitAsync_NoCommits_ReturnsEmpty()
	{
		_processRunner.RunAsync("git", "rev-list HEAD", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult(""));

		string firstCommit = await _service.GetFirstCommitAsync("/repo").ConfigureAwait(false);

		Assert.AreEqual(string.Empty, firstCommit);
	}

	// CreateAndPushTagAsync

	[TestMethod]
	public async Task CreateAndPushTagAsync_Success_CreatesAndPushesTag()
	{
		_processRunner.RunAsync("git", Arg.Is<string>(a => a.StartsWith("tag")), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult());
		_processRunner.RunAsync("git", Arg.Is<string>(a => a.StartsWith("push")), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult());

		await _service.CreateAndPushTagAsync("/repo", "v1.0.0", "abc123", "Release v1.0.0").ConfigureAwait(false);

		await _processRunner.Received(1).RunAsync("git",
			Arg.Is<string>(a => a.Contains("tag") && a.Contains("v1.0.0") && a.Contains("abc123")),
			Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _processRunner.Received(1).RunAsync("git",
			Arg.Is<string>(a => a.Contains("push origin")),
			Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task CreateAndPushTagAsync_CreateFails_ThrowsInvalidOperationException()
	{
		_processRunner.RunAsync("git", Arg.Is<string>(a => a.StartsWith("tag")), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult("tag error"));

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.CreateAndPushTagAsync("/repo", "v1.0.0", "abc123", "Release")).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task CreateAndPushTagAsync_PushFails_ThrowsInvalidOperationException()
	{
		_processRunner.RunAsync("git", Arg.Is<string>(a => a.StartsWith("tag")), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult());
		_processRunner.RunAsync("git", Arg.Is<string>(a => a.StartsWith("push")), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult("push error"));

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.CreateAndPushTagAsync("/repo", "v1.0.0", "abc123", "Release")).ConfigureAwait(false);
	}

	// StageFilesAsync

	[TestMethod]
	public async Task StageFilesAsync_Success_StagesFiles()
	{
		_processRunner.RunAsync("git", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult());

		await _service.StageFilesAsync("/repo", ["file1.cs", "file2.cs"]).ConfigureAwait(false);

		await _processRunner.Received(1).RunAsync("git",
			Arg.Is<string>(a => a.Contains("add") && a.Contains("file1.cs") && a.Contains("file2.cs")),
			Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task StageFilesAsync_Failure_ThrowsInvalidOperationException()
	{
		_processRunner.RunAsync("git", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult("stage error"));

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.StageFilesAsync("/repo", ["file.cs"])).ConfigureAwait(false);
	}

	// CommitAsync

	[TestMethod]
	public async Task CommitAsync_Success_ReturnsNewCommitHash()
	{
		_processRunner.RunAsync("git", Arg.Is<string>(a => a.StartsWith("commit")), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult());
		_processRunner.RunAsync("git", "rev-parse HEAD", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("newcommithash"));

		string hash = await _service.CommitAsync("/repo", "test message").ConfigureAwait(false);

		Assert.AreEqual("newcommithash", hash);
	}

	[TestMethod]
	public async Task CommitAsync_Failure_ThrowsInvalidOperationException()
	{
		_processRunner.RunAsync("git", Arg.Is<string>(a => a.StartsWith("commit")), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult("commit error"));

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.CommitAsync("/repo", "test message")).ConfigureAwait(false);
	}

	// PushAsync

	[TestMethod]
	public async Task PushAsync_Success_Completes()
	{
		_processRunner.RunAsync("git", "push", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult());

		await _service.PushAsync("/repo").ConfigureAwait(false);

		await _processRunner.Received(1).RunAsync("git", "push", Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task PushAsync_Failure_ThrowsInvalidOperationException()
	{
		_processRunner.RunAsync("git", "push", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult("push error"));

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.PushAsync("/repo")).ConfigureAwait(false);
	}

	// HasUncommittedChangesAsync

	[TestMethod]
	public async Task HasUncommittedChangesAsync_WithChanges_ReturnsTrue()
	{
		_processRunner.RunAsync("git", "status --porcelain", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult(" M file.cs\n"));

		bool result = await _service.HasUncommittedChangesAsync("/repo").ConfigureAwait(false);

		Assert.IsTrue(result);
	}

	[TestMethod]
	public async Task HasUncommittedChangesAsync_Clean_ReturnsFalse()
	{
		_processRunner.RunAsync("git", "status --porcelain", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult(""));

		bool result = await _service.HasUncommittedChangesAsync("/repo").ConfigureAwait(false);

		Assert.IsFalse(result);
	}

	// SetIdentityAsync

	[TestMethod]
	public async Task SetIdentityAsync_Success_SetsNameAndEmail()
	{
		_processRunner.RunAsync("git", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult());

		await _service.SetIdentityAsync("/repo", "Test User", "test@example.com").ConfigureAwait(false);

		await _processRunner.Received(1).RunAsync("git",
			Arg.Is<string>(a => a.Contains("user.name")),
			Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _processRunner.Received(1).RunAsync("git",
			Arg.Is<string>(a => a.Contains("user.email")),
			Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task SetIdentityAsync_NameFails_ThrowsInvalidOperationException()
	{
		_processRunner.RunAsync("git", Arg.Is<string>(a => a.Contains("user.name")), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult("name error"));

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.SetIdentityAsync("/repo", "Test", "test@example.com")).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task SetIdentityAsync_EmailFails_ThrowsInvalidOperationException()
	{
		_processRunner.RunAsync("git", Arg.Is<string>(a => a.Contains("user.name")), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult());
		_processRunner.RunAsync("git", Arg.Is<string>(a => a.Contains("user.email")), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult("email error"));

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.SetIdentityAsync("/repo", "Test", "test@example.com")).ConfigureAwait(false);
	}
}
