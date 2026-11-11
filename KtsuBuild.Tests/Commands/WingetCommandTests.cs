// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Commands;

using System.CommandLine;
using KtsuBuild.Abstractions;
using KtsuBuild.Tests.Helpers;
using KtsuBuild.Tests.Mocks;
using KtsuBuild.Tool;
using KtsuBuild.Tool.Commands;
using KtsuBuild.Winget;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

/// <summary>
/// Tests how <c>winget generate</c> reports each kind of result and which exit code it chooses, and
/// drives the <c>winget upload</c> handler against a workspace on disk and a substituted process
/// runner, so its exit code and the directory <c>gh</c> runs in are asserted end to end.
/// </summary>
[TestClass]
public class WingetCommandTests
{
	private IProcessRunner _processRunner = null!;
	private RecordingBuildLogger _logger = null!;
	private string _workspace = null!;

	[TestInitialize]
	public void Setup()
	{
		_workspace = TestHelpers.CreateTempDir("WingetCommand");
		_logger = new RecordingBuildLogger();
		_processRunner = Substitute.For<IProcessRunner>();
	}

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_workspace))
		{
			Directory.Delete(_workspace, recursive: true);
		}
	}

	// A missing release made gh exit 1, but upload only warned, so the step printed
	// "Manifests uploaded!" and exited 0 (#203).
	[TestMethod]
	public async Task Upload_GhFails_ExitsOneWithoutReportingSuccess()
	{
		await WriteManifestAsync().ConfigureAwait(false);
		GhExitsWith(1);

		int exitCode = await RunUploadAsync().ConfigureAwait(false);

		Assert.AreEqual(1, exitCode);
		Assert.IsFalse(_logger.Successes.Contains("Manifests uploaded!"), string.Join(Environment.NewLine, _logger.Successes));
		Assert.IsTrue(_logger.Errors.Any(e => e.StartsWith("Failed to upload manifests:", StringComparison.Ordinal)), string.Join(Environment.NewLine, _logger.Errors));
	}

	[TestMethod]
	public async Task Upload_NoManifests_ExitsOneWithoutRunningGh()
	{
		int exitCode = await RunUploadAsync().ConfigureAwait(false);

		Assert.AreEqual(1, exitCode);
		Assert.IsFalse(_logger.Successes.Contains("Manifests uploaded!"), string.Join(Environment.NewLine, _logger.Successes));
		await _processRunner.DidNotReceive().RunWithCallbackAsync("gh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task Upload_Succeeds_RunsGhInTheWorkspace()
	{
		await WriteManifestAsync().ConfigureAwait(false);
		GhExitsWith(0);

		int exitCode = await RunUploadAsync().ConfigureAwait(false);

		Assert.AreEqual(0, exitCode);
		Assert.IsTrue(_logger.Successes.Contains("Manifests uploaded!"), string.Join(Environment.NewLine, _logger.Successes));
		await _processRunner.Received(1).RunWithCallbackAsync(
			"gh",
			ArgMatch.NotNull<string>(a => a.StartsWith("release upload v1.2.3 ", StringComparison.Ordinal)),
			_workspace,
			Arg.Any<Action<string>?>(),
			Arg.Any<Action<string>?>(),
			Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	private async Task WriteManifestAsync()
	{
		string manifestDir = Path.Combine(_workspace, "winget");
		Directory.CreateDirectory(manifestDir);
		await File.WriteAllTextAsync(Path.Combine(manifestDir, "ktsu.Test.yaml"), "content").ConfigureAwait(false);
	}

	private void GhExitsWith(int exitCode) =>
		_processRunner
			.RunWithCallbackAsync("gh", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(exitCode);

	private async Task<int> RunUploadAsync()
	{
#pragma warning disable IDE0028 // RootCommand implements IEnumerable, so the analyzer offers a collection initializer
		RootCommand root = new();
#pragma warning restore IDE0028
		Program.AddWingetCommand(root, _processRunner, _logger);
		return await root.Parse(["winget", "upload", "--workspace", _workspace, "--version", "1.2.3"])
			.InvokeAsync(configuration: null, cancellationToken: CancellationToken.None)
			.ConfigureAwait(false);
	}

	[TestMethod]
	public void ReportGenerateResult_ManifestsWritten_ReportsThePackageId()
	{
		RecordingBuildLogger logger = new();
		WingetManifestResult result = new() { Success = true, PackageId = "ktsu.MyApp", ManifestFiles = ["a.yaml"] };

		Assert.AreEqual(0, WingetCommand.ReportGenerateResult(result, logger));
		Assert.Contains("Generated manifests for ktsu.MyApp", logger.Successes);
	}

	[TestMethod]
	public void ReportGenerateResult_SuccessWithNoManifests_WarnsInsteadOfReportingAnEmptyId()
	{
		// What HandleNoHashes returns when nothing executable was found to hash (#300)
		RecordingBuildLogger logger = new();
		WingetManifestResult result = new() { Success = true, IsLibraryOnly = false };

		Assert.AreEqual(0, WingetCommand.ReportGenerateResult(result, logger));
		Assert.IsEmpty(logger.Successes);
		Assert.Contains("No manifests generated", logger.Warnings);
	}

	[TestMethod]
	public void ReportGenerateResult_LibraryOnly_ReportsTheSkip()
	{
		RecordingBuildLogger logger = new();
		WingetManifestResult result = new() { Success = true, IsLibraryOnly = true };

		Assert.AreEqual(0, WingetCommand.ReportGenerateResult(result, logger));
		Assert.Contains("Library-only project - no manifests generated", logger.Infos);
		Assert.IsEmpty(logger.Successes);
	}

	[TestMethod]
	public void ReportGenerateResult_Failure_ExitsOne()
	{
		RecordingBuildLogger logger = new();
		WingetManifestResult result = new() { Success = false, Error = "no repo" };

		Assert.AreEqual(1, WingetCommand.ReportGenerateResult(result, logger));
		Assert.Contains("Failed to generate manifests: no repo", logger.Errors);
	}
}
