// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Profile;

using KtsuBuild.Abstractions;
using KtsuBuild.Profile;
using KtsuBuild.Tests.Mocks;
using KtsuBuild.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

[TestClass]
public class GitHubApiClientTests
{
	private IProcessRunner _processRunner = null!;
	private GitHubApiClient _client = null!;
	private readonly List<string> _requestedArguments = [];
	private string _response = "[]";

	[TestInitialize]
	public void Setup()
	{
		_requestedArguments.Clear();
		_response = "[]";
		_processRunner = Substitute.For<IProcessRunner>();

		// Registered once. Registering the same call spec twice would run both Arg.Do callbacks per
		// invocation and double-count the requests.
		_processRunner
			.RunAsync("gh", Arg.Do<string>(_requestedArguments.Add), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(_ => Task.FromResult(Success(_response)));

		_client = new GitHubApiClient(_processRunner, new MockBuildLogger());
	}

	private static ProcessResult Success(string output) => new()
	{
		ExitCode = 0,
		StandardOutput = output,
		StandardError = string.Empty,
	};

	private void RespondWith(string output) => _response = output;

	[TestMethod]
	public async Task CountCommitsSinceAsync_SendsTheTimestampAsUtc()
	{
		// The timestamp is labelled Z, so it must be a UTC instant. Sending a local time under a Z
		// label shifts the activity window by the machine's offset and miscounts commits.
		DateTimeOffset since = new(2026, 7, 30, 6, 28, 34, TimeSpan.FromHours(10));

		await _client.CountCommitsSinceAsync("ktsu-dev", "Extensions", since).ConfigureAwait(false);

		StringAssert.Contains(_requestedArguments[0], "since=2026-07-29T20:28:34Z");
	}

	[TestMethod]
	public async Task CountCommitsSinceAsync_CountsTheReturnedCommits()
	{
		RespondWith("[{},{},{}]");

		Assert.AreEqual(3, await _client.CountCommitsSinceAsync("ktsu-dev", "Extensions", DateTimeOffset.UtcNow).ConfigureAwait(false));
	}

	[TestMethod]
	public async Task CountCommitsSinceAsync_WithFailedCall_ReturnsZero()
	{
		_processRunner
			.RunAsync("gh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(new ProcessResult { ExitCode = 1, StandardOutput = string.Empty, StandardError = "not found" }));

		Assert.AreEqual(0, await _client.CountCommitsSinceAsync("ktsu-dev", "Missing", DateTimeOffset.UtcNow).ConfigureAwait(false));
	}

	[TestMethod]
	public async Task GetLatestWorkflowRunAsync_FiltersByBranch()
	{
		// Without a branch the API returns the newest run on any branch, so a failing feature branch
		// would misreport the repository's status.
		RespondWith("""{"workflow_runs":[{"status":"completed","conclusion":"success"}]}""");

		GitHubWorkflowRun? run = await _client.GetLatestWorkflowRunAsync("ktsu-dev", "TUI", "dotnet.yml", "main").ConfigureAwait(false);

		StringAssert.Contains(_requestedArguments[0], "branch=main");
		StringAssert.Contains(_requestedArguments[0], "per_page=1");
		Assert.AreEqual("success", run?.Conclusion);
	}

	[TestMethod]
	public async Task GetLatestWorkflowRunAsync_WithNoRuns_ReturnsNull()
	{
		RespondWith("""{"workflow_runs":[]}""");

		Assert.IsNull(await _client.GetLatestWorkflowRunAsync("ktsu-dev", "TUI", "dotnet.yml", "main").ConfigureAwait(false));
	}

	[TestMethod]
	public async Task GetLatestWorkflowRunAsync_WithInProgressRun_ReportsNoConclusion()
	{
		RespondWith("""{"workflow_runs":[{"status":"in_progress","conclusion":null}]}""");

		GitHubWorkflowRun? run = await _client.GetLatestWorkflowRunAsync("ktsu-dev", "TUI", "dotnet.yml", "main").ConfigureAwait(false);

		Assert.IsNotNull(run);
		Assert.IsNull(run.Conclusion);
	}

	[TestMethod]
	public async Task ListTreePathsAsync_ReturnsBlobPathsOnly()
	{
		RespondWith("""
			{"tree":[
				{"path":"src","type":"tree"},
				{"path":"src/Widget.csproj","type":"blob"},
				{"path":"README.md","type":"blob"}
			]}
			""");

		IReadOnlyList<string> paths = await _client.ListTreePathsAsync("ktsu-dev", "Widget", "main").ConfigureAwait(false);

		Assert.AreEqual("src/Widget.csproj,README.md", string.Join(",", paths));
	}

	[TestMethod]
	public async Task ListTreePathsAsync_RequestsTheTreeRecursively()
	{
		// One recursive request replaces a walk that cost a call per directory.
		await _client.ListTreePathsAsync("ktsu-dev", "Widget", "main").ConfigureAwait(false);

		StringAssert.Contains(_requestedArguments[0], "git/trees/main?recursive=1");
	}

	[TestMethod]
	public async Task GetFileTextAsync_DecodesBase64Content()
	{
		string encoded = Convert.ToBase64String("<Project />"u8.ToArray());
		RespondWith($$"""{"content":"{{encoded}}"}""");

		Assert.AreEqual("<Project />", await _client.GetFileTextAsync("ktsu-dev", "Widget", "Widget.csproj").ConfigureAwait(false));
	}

	[TestMethod]
	public async Task GetFileTextAsync_WithMissingFile_ReturnsNull()
	{
		RespondWith("""{"message":"Not Found"}""");

		Assert.IsNull(await _client.GetFileTextAsync("ktsu-dev", "Widget", "Nope.csproj").ConfigureAwait(false));
	}

	[TestMethod]
	public async Task ListOrganizationRepositoriesAsync_ParsesArchivedAndDefaultBranch()
	{
		RespondWith("""
			[
				{"name":"Alpha","default_branch":"main","archived":false},
				{"name":"Bravo","default_branch":"trunk","archived":true}
			]
			""");

		IReadOnlyList<GitHubRepository> repositories = await _client.ListOrganizationRepositoriesAsync("ktsu-dev").ConfigureAwait(false);

		Assert.AreEqual(2, repositories.Count);
		Assert.AreEqual("main", repositories[0].DefaultBranch);
		Assert.IsFalse(repositories[0].IsArchived);
		Assert.AreEqual("trunk", repositories[1].DefaultBranch);
		Assert.IsTrue(repositories[1].IsArchived);
	}

	[TestMethod]
	public async Task ListOrganizationRepositoriesAsync_StopsAfterAShortPage()
	{
		RespondWith("""[{"name":"Alpha","default_branch":"main","archived":false}]""");

		await _client.ListOrganizationRepositoriesAsync("ktsu-dev").ConfigureAwait(false);

		Assert.AreEqual(1, _requestedArguments.Count, "A page shorter than the page size is the last page");
	}

	[TestMethod]
	public async Task ListDirectoryNamesAsync_ReturnsDirectoriesOnly()
	{
		RespondWith("""
			[
				{"name":"1.0.19","type":"dir"},
				{"name":"1.0.21","type":"dir"},
				{"name":"README.md","type":"file"}
			]
			""");

		IReadOnlyList<string> names = await _client
			.ListDirectoryNamesAsync("microsoft", "winget-pkgs", "manifests/k/ktsu/BlastMerge")
			.ConfigureAwait(false);

		Assert.AreEqual("1.0.19,1.0.21", string.Join(",", names));
	}

	[TestMethod]
	public async Task ListActiveWorkflowFileNamesAsync_SkipsDisabledWorkflowsAndStripsPaths()
	{
		RespondWith("""
			{"workflows":[
				{"state":"active","path":".github/workflows/dotnet.yml"},
				{"state":"disabled_manually","path":".github/workflows/old.yml"}
			]}
			""");

		IReadOnlyList<string> names = await _client.ListActiveWorkflowFileNamesAsync("ktsu-dev", "Widget").ConfigureAwait(false);

		Assert.AreEqual("dotnet.yml", string.Join(",", names));
	}

	[TestMethod]
	public async Task GetJson_WithUnparseableResponse_DegradesToEmpty()
	{
		RespondWith("not json at all");

		Assert.AreEqual(0, (await _client.ListOrganizationRepositoriesAsync("ktsu-dev").ConfigureAwait(false)).Count);
	}
}
