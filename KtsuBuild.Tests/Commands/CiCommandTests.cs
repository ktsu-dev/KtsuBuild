// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Commands;

using KtsuBuild.Abstractions;
using KtsuBuild.Tests.Helpers;
using KtsuBuild.Tests.Mocks;
using KtsuBuild.Tool.Commands;
using KtsuBuild.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

/// <summary>
/// Drives the <c>ci</c> command's handler against a workspace on disk and a substituted process
/// runner, so the wiring from its options into the pipeline stages is asserted rather than only the
/// stages themselves.
/// </summary>
/// <remarks>
/// The pipeline reads its inputs from the process environment, which is per-process rather than
/// per-test, so these run one at a time and put every variable they touch back afterwards.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class CiCommandTests
{
	private static readonly string[] EnvironmentVariables =
	[
		"GITHUB_SERVER_URL",
		"GITHUB_REF",
		"GITHUB_SHA",
		"GITHUB_REPOSITORY",
		"GITHUB_TOKEN",
		"GH_TOKEN",
		"NUGET_API_KEY",
		"KTSU_PACKAGE_KEY",
		"EXPECTED_OWNER",
		"GITHUB_OUTPUT",
	];

	private readonly Dictionary<string, string?> _savedEnvironment = [];

	private IProcessRunner _processRunner = null!;
	private RecordingBuildLogger _logger = null!;
	private string _workspace = null!;

	/// <summary>
	/// Gets or sets the context MSTest injects, whose cancellation token the handler runs under.
	/// </summary>
	public TestContext TestContext { get; set; } = null!;

	[TestInitialize]
	public void Setup()
	{
		foreach (string name in EnvironmentVariables)
		{
			_savedEnvironment[name] = Environment.GetEnvironmentVariable(name);
			Environment.SetEnvironmentVariable(name, null);
		}

		Environment.SetEnvironmentVariable("GITHUB_REF", "refs/heads/main");
		Environment.SetEnvironmentVariable("GITHUB_SHA", "4444444444444444444444444444444444444444");
		Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", "ktsu-dev/TestRepo");

		_workspace = TestHelpers.CreateTempDir("CiCommand");
		_logger = new RecordingBuildLogger();
		_processRunner = Substitute.For<IProcessRunner>();
		_processRunner
			.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(call => Respond(call.ArgAt<string>(0), call.ArgAt<string>(1)));
		_processRunner
			.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult());
		_processRunner
			.RunWithCallbackAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);
	}

	[TestCleanup]
	public void Cleanup()
	{
		foreach (KeyValuePair<string, string?> saved in _savedEnvironment)
		{
			Environment.SetEnvironmentVariable(saved.Key, saved.Value);
		}

		if (Directory.Exists(_workspace))
		{
			Directory.Delete(_workspace, recursive: true);
		}
	}

	// ci takes the version it writes, packs and tags from the metadata stage, so a --version-bump
	// that reached only the version gate published the detected version: major on top of v3.10.0
	// and a fix wrote 3.10.1.
	[TestMethod]
	public async Task VersionBumpReachesTheVersionTheMetadataStageWrites()
	{
		Func<CiCommand.CiOptions, CancellationToken, Task<int>> handler = CiCommand.CreateHandler(_processRunner, _logger);

		await handler(new CiCommand.CiOptions(
			Workspace: _workspace,
			Configuration: "Release",
			Verbose: false,
			DryRun: false,
			VersionBump: "major",
			NoTest: true,
			NoRelease: true), TestContext.CancellationToken).ConfigureAwait(false);

		string versionFile = Path.Combine(_workspace, "VERSION.md");
		Assert.IsTrue(File.Exists(versionFile), string.Join(Environment.NewLine, _logger.Errors));
		Assert.AreEqual("4.0.0", (await File.ReadAllTextAsync(versionFile, TestContext.CancellationToken).ConfigureAwait(false)).Trim());
	}

	/// <summary>
	/// Answers the git commands version calculation runs: one tag, <c>v3.10.0</c>, with a single fix
	/// since it. Everything else succeeds with no output.
	/// </summary>
	private static ProcessResult Respond(string fileName, string arguments)
	{
		if (fileName == "git")
		{
			if (arguments.EndsWith("tag --list --sort=-v:refname", StringComparison.Ordinal))
			{
				return TestHelpers.SuccessResult("v3.10.0");
			}

			if (arguments == "rev-list HEAD")
			{
				return TestHelpers.SuccessResult("1111111111111111111111111111111111111111\n2222222222222222222222222222222222222222");
			}

			if (arguments.StartsWith("rev-list -n 1 ", StringComparison.Ordinal))
			{
				return TestHelpers.SuccessResult("3333333333333333333333333333333333333333");
			}

			if (arguments.StartsWith("log --format=format:%s", StringComparison.Ordinal))
			{
				return TestHelpers.SuccessResult("fix: a change worth shipping");
			}
		}

		return TestHelpers.SuccessResult();
	}
}
