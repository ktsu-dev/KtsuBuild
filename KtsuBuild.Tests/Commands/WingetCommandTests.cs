// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Commands;

using KtsuBuild.Tests.Mocks;
using KtsuBuild.Tool.Commands;
using KtsuBuild.Winget;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests how <c>winget generate</c> reports each kind of result and which exit code it chooses.
/// </summary>
[TestClass]
public class WingetCommandTests
{
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
