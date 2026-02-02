// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Git;

using KtsuBuild.Abstractions;
using KtsuBuild.Git;
using KtsuBuild.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

[TestClass]
public class VersionCalculatorTests
{
	private IGitService _gitService = null!;
	private IBuildLogger _logger = null!;
	private VersionCalculator _calculator = null!;

	[TestInitialize]
	public void Setup()
	{
		_gitService = Substitute.For<IGitService>();
		_logger = new MockBuildLogger();
		_calculator = new VersionCalculator(_gitService, _logger);
	}

	private void SetupGitServiceForVersion(string? lastTag, IReadOnlyList<string>? commitMessages = null)
	{
		if (lastTag is null)
		{
			_gitService.GetTagsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
				.Returns(Task.FromResult<IReadOnlyList<string>>([]));
		}
		else
		{
			_gitService.GetTagsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
				.Returns(Task.FromResult<IReadOnlyList<string>>([lastTag]));
			_gitService.GetTagCommitHashAsync(Arg.Any<string>(), Arg.Is(lastTag), Arg.Any<CancellationToken>())
				.Returns(Task.FromResult<string?>("aaa111"));
		}

		_gitService.GetFirstCommitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult("000000"));
		_gitService.GetCommitMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(commitMessages ?? (IReadOnlyList<string>)["Some commit message"]));
		_gitService.GetDiffAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(string.Empty));
	}

	[TestMethod]
	public async Task GetVersionInfoAsync_NoTags_UsesFallbackVersion()
	{
		// Arrange
		SetupGitServiceForVersion(null, ["Initial commit [patch]"]);

		// Act
		var result = await _calculator.GetVersionInfoAsync("/repo", "abc123");

		// Assert
		Assert.IsTrue(result.UsingFallbackTag);
		Assert.AreEqual("1.0.0", result.Version);
	}

	[TestMethod]
	public async Task GetVersionInfoAsync_MajorBump()
	{
		// Arrange
		SetupGitServiceForVersion("v1.2.3", ["Breaking change [major]"]);

		// Act
		var result = await _calculator.GetVersionInfoAsync("/repo", "abc123");

		// Assert
		Assert.AreEqual("2.0.0", result.Version);
		Assert.AreEqual(2, result.Major);
		Assert.AreEqual(0, result.Minor);
		Assert.AreEqual(0, result.Patch);
		Assert.IsFalse(result.IsPrerelease);
	}

	[TestMethod]
	public async Task GetVersionInfoAsync_MinorBump()
	{
		// Arrange
		SetupGitServiceForVersion("v1.2.3", ["New feature [minor]"]);

		// Act
		var result = await _calculator.GetVersionInfoAsync("/repo", "abc123");

		// Assert
		Assert.AreEqual("1.3.0", result.Version);
		Assert.AreEqual(1, result.Major);
		Assert.AreEqual(3, result.Minor);
		Assert.AreEqual(0, result.Patch);
		Assert.IsFalse(result.IsPrerelease);
	}

	[TestMethod]
	public async Task GetVersionInfoAsync_PatchBump()
	{
		// Arrange
		SetupGitServiceForVersion("v1.2.3", ["Bug fix [patch]"]);

		// Act
		var result = await _calculator.GetVersionInfoAsync("/repo", "abc123");

		// Assert
		Assert.AreEqual("1.2.4", result.Version);
		Assert.AreEqual(1, result.Major);
		Assert.AreEqual(2, result.Minor);
		Assert.AreEqual(4, result.Patch);
		Assert.IsFalse(result.IsPrerelease);
	}

	[TestMethod]
	public async Task GetVersionInfoAsync_NewPrerelease()
	{
		// Arrange
		SetupGitServiceForVersion("v1.2.3", ["Experimental feature [pre]"]);

		// Act
		var result = await _calculator.GetVersionInfoAsync("/repo", "abc123");

		// Assert
		Assert.AreEqual("1.2.4-pre.1", result.Version);
		Assert.AreEqual(1, result.Major);
		Assert.AreEqual(2, result.Minor);
		Assert.AreEqual(4, result.Patch);
		Assert.IsTrue(result.IsPrerelease);
		Assert.AreEqual(1, result.PrereleaseNumber);
	}

	[TestMethod]
	public async Task GetVersionInfoAsync_BumpPrereleaseNumber()
	{
		// Arrange
		SetupGitServiceForVersion("v1.2.3-pre.1", ["More experimental work [pre]"]);

		// Act
		var result = await _calculator.GetVersionInfoAsync("/repo", "abc123");

		// Assert
		Assert.AreEqual("1.2.3-pre.2", result.Version);
		Assert.AreEqual(1, result.Major);
		Assert.AreEqual(2, result.Minor);
		Assert.AreEqual(3, result.Patch);
		Assert.IsTrue(result.IsPrerelease);
		Assert.AreEqual(2, result.PrereleaseNumber);
	}

	[TestMethod]
	public async Task GetVersionInfoAsync_ReleaseFromPrerelease()
	{
		// Arrange
		SetupGitServiceForVersion("v1.2.3-pre.1", ["Ready for release [patch]"]);

		// Act
		var result = await _calculator.GetVersionInfoAsync("/repo", "abc123");

		// Assert
		Assert.AreEqual("1.2.3", result.Version);
		Assert.AreEqual(1, result.Major);
		Assert.AreEqual(2, result.Minor);
		Assert.AreEqual(3, result.Patch);
		Assert.IsFalse(result.IsPrerelease);
	}

	[TestMethod]
	public async Task GetVersionInfoAsync_MajorFromPrerelease()
	{
		// Arrange
		SetupGitServiceForVersion("v1.2.3-pre.1", ["Breaking change [major]"]);

		// Act
		var result = await _calculator.GetVersionInfoAsync("/repo", "abc123");

		// Assert
		Assert.AreEqual("2.0.0", result.Version);
		Assert.AreEqual(2, result.Major);
		Assert.AreEqual(0, result.Minor);
		Assert.AreEqual(0, result.Patch);
		Assert.IsFalse(result.IsPrerelease);
	}

	[TestMethod]
	public async Task GetVersionInfoAsync_AlphaPrereleaseBump()
	{
		// Arrange
		SetupGitServiceForVersion("v1.0.0-alpha.5", ["More alpha work [pre]"]);

		// Act
		var result = await _calculator.GetVersionInfoAsync("/repo", "abc123");

		// Assert
		Assert.AreEqual("1.0.0-alpha.6", result.Version);
		Assert.AreEqual(1, result.Major);
		Assert.AreEqual(0, result.Minor);
		Assert.AreEqual(0, result.Patch);
		Assert.IsTrue(result.IsPrerelease);
		Assert.AreEqual(6, result.PrereleaseNumber);
		Assert.AreEqual("alpha", result.PrereleaseLabel);
	}

	[TestMethod]
	public async Task GetVersionInfoAsync_SkipKeepsVersion()
	{
		// Arrange
		SetupGitServiceForVersion("v1.2.3", []);

		// Act
		var result = await _calculator.GetVersionInfoAsync("/repo", "abc123");

		// Assert
		Assert.AreEqual("1.2.3", result.Version);
		Assert.AreEqual(VersionType.Skip, result.VersionIncrement);
	}

	[TestMethod]
	public async Task GetVersionInfoAsync_BetaPrereleaseBump()
	{
		// Arrange
		SetupGitServiceForVersion("v2.0.0-beta.3", ["Beta update [pre]"]);

		// Act
		var result = await _calculator.GetVersionInfoAsync("/repo", "abc123");

		// Assert
		Assert.AreEqual("2.0.0-beta.4", result.Version);
		Assert.AreEqual("beta", result.PrereleaseLabel);
	}

	[TestMethod]
	public async Task GetVersionInfoAsync_RcPrereleaseBump()
	{
		// Arrange
		SetupGitServiceForVersion("v3.1.0-rc.2", ["Release candidate fix [pre]"]);

		// Act
		var result = await _calculator.GetVersionInfoAsync("/repo", "abc123");

		// Assert
		Assert.AreEqual("3.1.0-rc.3", result.Version);
		Assert.AreEqual("rc", result.PrereleaseLabel);
	}

	[TestMethod]
	public async Task GetVersionInfoAsync_MinorFromPrerelease()
	{
		// Arrange
		SetupGitServiceForVersion("v1.2.3-pre.5", ["New feature [minor]"]);

		// Act
		var result = await _calculator.GetVersionInfoAsync("/repo", "abc123");

		// Assert
		Assert.AreEqual("1.3.0", result.Version);
		Assert.IsFalse(result.IsPrerelease);
	}

	[TestMethod]
	public async Task GetVersionInfoAsync_SetsVersionInfoProperties()
	{
		// Arrange
		SetupGitServiceForVersion("v1.2.3", ["Bug fix [patch]"]);

		// Act
		var result = await _calculator.GetVersionInfoAsync("/repo", "abc123");

		// Assert
		Assert.AreEqual("v1.2.3", result.LastTag);
		Assert.AreEqual("1.2.3", result.LastVersion);
		Assert.IsFalse(result.WasPrerelease);
		Assert.AreEqual(VersionType.Patch, result.VersionIncrement);
		Assert.IsFalse(string.IsNullOrEmpty(result.IncrementReason));
		Assert.AreEqual("abc123", result.LastCommit);
	}

	[TestMethod]
	public async Task GetVersionInfoAsync_CustomInitialVersion()
	{
		// Arrange
		SetupGitServiceForVersion(null, ["First commit [patch]"]);

		// Act
		var result = await _calculator.GetVersionInfoAsync("/repo", "abc123", initialVersion: "0.1.0");

		// Assert
		Assert.IsTrue(result.UsingFallbackTag);
		Assert.AreEqual("0.1.0", result.Version);
	}
}
