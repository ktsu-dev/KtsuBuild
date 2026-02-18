// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Publishing;

using KtsuBuild.Abstractions;
using KtsuBuild.Publishing;
using KtsuBuild.Tests.Mocks;
using NSubstitute;

[TestClass]
public class NuGetPublisherTests
{
	private IProcessRunner _processRunner = null!;
	private NuGetPublisher _publisher = null!;

	[TestInitialize]
	public void Setup()
	{
		_processRunner = Substitute.For<IProcessRunner>();
		_publisher = new NuGetPublisher(_processRunner, new MockBuildLogger());
	}

	// PublishToGitHubAsync

	[TestMethod]
	public async Task PublishToGitHubAsync_Success_Completes()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _publisher.PublishToGitHubAsync("*.nupkg", "testowner", "testtoken").ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("nuget push")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task PublishToGitHubAsync_BuildsCorrectSourceUrl()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _publisher.PublishToGitHubAsync("*.nupkg", "myowner", "token").ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("https://nuget.pkg.github.com/myowner/index.json")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task PublishToGitHubAsync_Failure_ThrowsInvalidOperationException()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(1);

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _publisher.PublishToGitHubAsync("*.nupkg", "owner", "token")).ConfigureAwait(false);
	}

	// PublishToNuGetOrgAsync

	[TestMethod]
	public async Task PublishToNuGetOrgAsync_Success_Completes()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _publisher.PublishToNuGetOrgAsync("*.nupkg", "apikey").ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("https://api.nuget.org/v3/index.json")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task PublishToNuGetOrgAsync_Failure_ThrowsInvalidOperationException()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(1);

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _publisher.PublishToNuGetOrgAsync("*.nupkg", "apikey")).ConfigureAwait(false);
	}

	// PublishToSourceAsync

	[TestMethod]
	public async Task PublishToSourceAsync_Success_Completes()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _publisher.PublishToSourceAsync("*.nupkg", "https://custom.feed/v3/index.json", "apikey").ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("https://custom.feed/v3/index.json")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task PublishToSourceAsync_Failure_ThrowsInvalidOperationException()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(1);

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _publisher.PublishToSourceAsync("*.nupkg", "https://custom.feed/v3/index.json", "apikey")).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task PublishAsync_IncludesSkipDuplicateFlag()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _publisher.PublishToNuGetOrgAsync("*.nupkg", "apikey").ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("--skip-duplicate")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task PublishAsync_PassesApiKeyCorrectly()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _publisher.PublishToNuGetOrgAsync("*.nupkg", "my-secret-key").ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("my-secret-key")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}
}
