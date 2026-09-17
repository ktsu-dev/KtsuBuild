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
/// Drives the <c>profile readme</c> handler directly, over a substituted <c>gh</c> process runner.
/// </summary>
/// <remarks>
/// The handler's own logic is the mapping from a failure to an exit code and a message. Three
/// exception kinds are reported three different ways, and the difference is all a caller reading CI
/// output has to work with, so each is asserted here. Every case below fails before the generator
/// reaches the NuGet catalogue, so no test makes a network request.
/// </remarks>
[TestClass]
public class ProfileCommandTests
{
	private IProcessRunner _processRunner = null!;
	private RecordingBuildLogger _logger = null!;
	private string _tempDir = null!;
	private string _response = "[]";

	[TestInitialize]
	public void Setup()
	{
		_logger = new RecordingBuildLogger();
		_tempDir = TestHelpers.CreateTempDir("ProfileCommand");
		_response = "[]";
		_processRunner = Substitute.For<IProcessRunner>();

		_processRunner.RunAsync("gh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(_ => Task.FromResult(new ProcessResult
			{
				ExitCode = 0,
				StandardOutput = _response,
				StandardError = string.Empty,
			}));
	}

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	private ProfileCommand.ProfileOptionsInput Input(string templatePath, bool verbose = false) => new(
		Organization: "ktsu-dev",
		TemplatePath: templatePath,
		OutputPath: Path.Combine(_tempDir, "README.md"),
		PackagePrefix: "ktsu",
		SdkPackage: "ktsu.Sdk",
		Exclude: [],
		Only: [],
		FallbackWorkflows: [],
		Verbose: verbose);

	private string WriteTemplate(string content)
	{
		string path = Path.Combine(_tempDir, "README.template");
		File.WriteAllText(path, content);
		return path;
	}

	private Task<int> Run(ProfileCommand.ProfileOptionsInput input, CancellationToken cancellationToken = default) =>
		ProfileCommand.CreateReadmeHandler(_processRunner, _logger)(input, cancellationToken);

	[TestMethod]
	public async Task CreateReadmeHandler_MissingTemplate_ReportsWhichFileWasMissing()
	{
		string missing = Path.Combine(_tempDir, "absent.template");

		Assert.AreEqual(1, await Run(Input(missing)).ConfigureAwait(false));

		// The FileNotFoundException branch reports the exception's own message, which names the path.
		// A generic "failed to generate" here would leave the reader to guess which file.
		Assert.IsTrue(RecordingBuildLogger.Any(_logger.Errors, missing));
		Assert.IsFalse(RecordingBuildLogger.Any(_logger.Errors, "Failed to generate profile README"));
	}

	[TestMethod]
	public async Task CreateReadmeHandler_Cancelled_ReportsCancellationAsAWarning()
	{
		string template = WriteTemplate("# ktsu.dev\n");
		using CancellationTokenSource cancelled = new();
		await cancelled.CancelAsync().ConfigureAwait(false);

		Assert.AreEqual(1, await Run(Input(template), cancelled.Token).ConfigureAwait(false));

		// A cancelled run is not a defect in the profile, so it is a warning and not an error.
		Assert.Contains("Cancelled", _logger.Warnings);
		Assert.IsEmpty(_logger.Errors);
	}

	[TestMethod]
	public async Task CreateReadmeHandler_TemplateLinkingARetiredRepository_PrefixesTheError()
	{
		_response = """[{"name":"Common","default_branch":"main","archived":true,"stargazers_count":0}]""";
		string template = WriteTemplate("See [Common](https://github.com/ktsu-dev/Common).\n");

		Assert.AreEqual(1, await Run(Input(template)).ConfigureAwait(false));

		// Anything that is neither a missing template nor a cancellation is reported with a prefix
		// naming what was being attempted, since the exception alone need not say.
		Assert.IsTrue(RecordingBuildLogger.Any(_logger.Errors, "Failed to generate profile README: "));
		Assert.IsTrue(RecordingBuildLogger.Any(_logger.Errors, "Common"));
	}

	[TestMethod]
	public async Task CreateReadmeHandler_AnyRun_HeadsTheOutputWithTheOrganization()
	{
		await Run(Input(Path.Combine(_tempDir, "absent.template"))).ConfigureAwait(false);

		Assert.Contains("Generating profile README for ktsu-dev", _logger.StepHeaders);
	}

	[TestMethod]
	public async Task CreateReadmeHandler_Verbose_TurnsOnVerboseLogging()
	{
		await Run(Input(Path.Combine(_tempDir, "absent.template"), verbose: true)).ConfigureAwait(false);

		Assert.IsTrue(_logger.VerboseEnabled);
	}
}
