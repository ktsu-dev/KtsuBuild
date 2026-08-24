// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Pipeline;

using KtsuBuild.Abstractions;
using KtsuBuild.Git;
using KtsuBuild.Pipeline;
using KtsuBuild.Tests.Helpers;
using KtsuBuild.Tests.Mocks;
using KtsuBuild.Utilities;
using NSubstitute;

// The pipeline reads its inputs from the process environment, which is per-process rather than
// per-test, so these run one at a time and put every variable they touch back afterwards.
[TestClass]
[DoNotParallelize]
public class PipelineServiceTests
{
	/// <summary>
	/// The version <c>BuildConfigurationProvider</c> seeds a fresh configuration with.
	/// </summary>
	private const string PlaceholderVersion = "1.0.0-pre.0";

	private const string HeadCommit = "1111111111111111111111111111111111111111";
	private const string FirstCommit = "2222222222222222222222222222222222222222";
	private const string TagCommit = "3333333333333333333333333333333333333333";

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
	private MockBuildLogger _logger = null!;
	private PipelineService _pipeline = null!;
	private string _tempDir = null!;

	// The two git answers each test varies: the tags that exist, and the commits since the newest
	// of them. Everything else the pipeline asks git is fixed.
	private string _tagList = "v3.10.0";
	private string _commitMessages = "fix: a change worth shipping";

	[TestInitialize]
	public void Setup()
	{
		foreach (string name in EnvironmentVariables)
		{
			_savedEnvironment[name] = Environment.GetEnvironmentVariable(name);
			Environment.SetEnvironmentVariable(name, null);
		}

		Environment.SetEnvironmentVariable("GITHUB_REF", "refs/heads/main");
		Environment.SetEnvironmentVariable("GITHUB_SHA", HeadCommit);
		Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", "ktsu-dev/TestRepo");

		_tempDir = TestHelpers.CreateTempDir("Pipeline");
		_logger = new MockBuildLogger();
		_processRunner = Substitute.For<IProcessRunner>();
		_processRunner
			.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(call => Respond(call.ArgAt<string>(0), call.ArgAt<string>(1)));

		_pipeline = new PipelineService(_processRunner, _logger);
	}

	[TestCleanup]
	public void Cleanup()
	{
		foreach (KeyValuePair<string, string?> saved in _savedEnvironment)
		{
			Environment.SetEnvironmentVariable(saved.Key, saved.Value);
		}

		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	// The defect this refactor exists to remove. BuildConfigurationProvider seeds Version with
	// "1.0.0-pre.0", and a caller that publishes without asking what the version should be ships a
	// real release under a version nobody chose. Resolving is now a stage of its own, and this is
	// what it has to produce.
	[TestMethod]
	public async Task ResolveVersionProducesTheResolvedVersionRatherThanThePlaceholder()
	{
		PipelineContext context = await _pipeline.PrepareAsync(_tempDir, "Release", CancellationToken.None).ConfigureAwait(false);
		await _pipeline.ResolveVersionAsync(context, "auto", CancellationToken.None).ConfigureAwait(false);

		Assert.IsNotNull(context.VersionInfo);
		Assert.AreEqual("3.10.1", context.VersionInfo.Version);
		Assert.AreNotEqual(PlaceholderVersion, context.VersionInfo.Version);
	}

	// The two stages are separate because ci resolves against the metadata commit, which does not
	// exist when a run is prepared. That makes the split easy to get wrong, so pin it: preparation
	// leaves the version unresolved, and leaves it visibly unresolved rather than defaulted.
	[TestMethod]
	public async Task PrepareDoesNoVersionWork()
	{
		PipelineContext context = await _pipeline.PrepareAsync(_tempDir, "Release", CancellationToken.None).ConfigureAwait(false);

		Assert.IsNull(context.VersionInfo);
		Assert.AreEqual(PlaceholderVersion, context.Configuration.Version);
		Assert.IsFalse(context.ReleaseSuppressedByVersionGate);
	}

	// Resolving reports what the version should be. It does not write it onto the configuration,
	// because in ci the configuration's version comes from the metadata result instead.
	[TestMethod]
	public async Task ResolveVersionLeavesTheConfigurationVersionAndHashAlone()
	{
		PipelineContext context = await _pipeline.PrepareAsync(_tempDir, "Release", CancellationToken.None).ConfigureAwait(false);
		string releaseHashBefore = context.Configuration.ReleaseHash;

		await _pipeline.ResolveVersionAsync(context, "auto", CancellationToken.None).ConfigureAwait(false);

		Assert.AreEqual(PlaceholderVersion, context.Configuration.Version);
		Assert.AreEqual(releaseHashBefore, context.Configuration.ReleaseHash);
	}

	[TestMethod]
	public async Task PrepareCarriesTheConfigurationNameOntoTheBuildConfiguration()
	{
		PipelineContext context = await _pipeline.PrepareAsync(_tempDir, "Debug", CancellationToken.None).ConfigureAwait(false);

		Assert.AreEqual("Debug", context.Configuration.Configuration);
	}

	[TestMethod]
	public async Task ResolveVersionSuppressesTheReleaseWhenEveryCommitCarriesSkipCi()
	{
		_commitMessages = "[bot][skip ci] Update Metadata\nchore: tidy [skip ci]";

		PipelineContext context = await ResolveAsync("auto").ConfigureAwait(false);

		Assert.IsNotNull(context.VersionInfo);
		Assert.AreEqual(VersionType.Skip, context.VersionInfo.VersionIncrement);
		Assert.IsTrue(context.ReleaseSuppressedByVersionGate);
	}

	[TestMethod]
	public async Task ResolveVersionSuppressesTheReleaseWhenTheRangeHoldsNoCommits()
	{
		_commitMessages = string.Empty;

		PipelineContext context = await ResolveAsync("auto").ConfigureAwait(false);

		Assert.IsNotNull(context.VersionInfo);
		Assert.AreEqual(VersionType.Skip, context.VersionInfo.VersionIncrement);
		Assert.IsTrue(context.ReleaseSuppressedByVersionGate);
	}

	[TestMethod]
	[DataRow("major", VersionType.Major, "4.0.0")]
	[DataRow("minor", VersionType.Minor, "3.11.0")]
	[DataRow("patch", VersionType.Patch, "3.10.1")]
	public async Task ResolveVersionForwardsTheForcedVersionBumpToTheCalculator(string versionBump, VersionType expectedIncrement, string expectedVersion)
	{
		PipelineContext context = await ResolveAsync(versionBump).ConfigureAwait(false);

		Assert.IsNotNull(context.VersionInfo);
		Assert.AreEqual(expectedIncrement, context.VersionInfo.VersionIncrement);
		Assert.AreEqual(expectedVersion, context.VersionInfo.Version);
		Assert.IsFalse(context.ReleaseSuppressedByVersionGate);
	}

	[TestMethod]
	public async Task ResolveVersionLeavesTheReleaseUnsuppressedForAnOrdinaryIncrement()
	{
		PipelineContext context = await ResolveAsync("auto").ConfigureAwait(false);

		Assert.IsNotNull(context.VersionInfo);
		Assert.AreEqual(VersionType.Patch, context.VersionInfo.VersionIncrement);
		Assert.IsFalse(context.ReleaseSuppressedByVersionGate);
	}

	[TestMethod]
	public async Task ResolveVersionStartsFromTheInitialVersionWhenNoTagsExist()
	{
		_tagList = string.Empty;

		PipelineContext context = await ResolveAsync("auto").ConfigureAwait(false);

		Assert.IsNotNull(context.VersionInfo);
		Assert.IsTrue(context.VersionInfo.UsingFallbackTag);
		Assert.AreNotEqual(PlaceholderVersion, context.VersionInfo.Version);
	}

	[TestMethod]
	public async Task ValidateIosReportsSuccessWhenTheWorkspaceHasNoIosHead()
	{
		bool result = await _pipeline.ValidateIosAsync(_tempDir, "Release", CancellationToken.None).ConfigureAwait(false);

		Assert.IsTrue(result);
	}

	// RestoreAndBuildAsync derives the build argument from the same .csx scan that decides
	// whether to install dotnet-script, rather than reading it off a configuration. This pins
	// both directions of that rule so ci and build, which now both call this method with no
	// build arguments parameter of their own, keep getting the argument dotnet-script needs.
	[TestMethod]
	public async Task RestoreAndBuildIncludesTheSingleCoreArgumentWhenTheWorkspaceHasCsxFiles()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);
		await File.WriteAllTextAsync(Path.Combine(_tempDir, "tool.csx"), "// script\n").ConfigureAwait(false);

		await _pipeline.RestoreAndBuildAsync(_tempDir, "Release", CancellationToken.None).ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			ArgMatch.NotNull<string>(a => a.Contains("build ", StringComparison.Ordinal) && a.EndsWith("-maxCpuCount:1", StringComparison.Ordinal)),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task RestoreAndBuildOmitsTheSingleCoreArgumentWhenTheWorkspaceHasNoCsxFiles()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _pipeline.RestoreAndBuildAsync(_tempDir, "Release", CancellationToken.None).ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			ArgMatch.NotNull<string>(a => a.Contains("build ", StringComparison.Ordinal) && !a.Contains("-maxCpuCount:1", StringComparison.Ordinal)),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	/// <summary>
	/// Runs the two stages a caller needs before it knows what version it would publish.
	/// </summary>
	private async Task<PipelineContext> ResolveAsync(string versionBump)
	{
		PipelineContext context = await _pipeline.PrepareAsync(_tempDir, "Release", CancellationToken.None).ConfigureAwait(false);
		await _pipeline.ResolveVersionAsync(context, versionBump, CancellationToken.None).ConfigureAwait(false);
		return context;
	}

	/// <summary>
	/// Answers the git and gh commands the pipeline runs, so the stages run end to end without a
	/// repository on disk.
	/// </summary>
	private ProcessResult Respond(string fileName, string arguments)
	{
		if (fileName == "gh")
		{
			return arguments.StartsWith("repo view", StringComparison.Ordinal)
				? TestHelpers.SuccessResult("{\"owner\":{\"login\":\"ktsu-dev\"},\"nameWithOwner\":\"ktsu-dev/TestRepo\",\"isFork\":false}")
				: TestHelpers.SuccessResult();
		}

		if (fileName == "git")
		{
			if (arguments == "tag --list --sort=-v:refname")
			{
				return TestHelpers.SuccessResult(_tagList);
			}

			if (arguments == "rev-list HEAD")
			{
				// GetFirstCommitAsync takes the last line, so the oldest commit goes last.
				return TestHelpers.SuccessResult($"{HeadCommit}\n{FirstCommit}");
			}

			if (arguments.StartsWith("rev-list -n 1 ", StringComparison.Ordinal))
			{
				return TestHelpers.SuccessResult(TagCommit);
			}

			if (arguments.StartsWith("log --format=format:%s", StringComparison.Ordinal))
			{
				return TestHelpers.SuccessResult(_commitMessages);
			}
		}

		return TestHelpers.SuccessResult();
	}
}
