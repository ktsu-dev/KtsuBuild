// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Configuration;

using KtsuBuild.Configuration;

[TestClass]
public class CiReleaseDecisionTests
{
	[TestMethod]
	[DataRow(false, false, false, false, DisplayName = "not a release build")]
	[DataRow(false, false, true, false, DisplayName = "not a release build, flag set")]
	[DataRow(false, true, false, false, DisplayName = "not a release build, version skipped")]
	[DataRow(false, true, true, false, DisplayName = "not a release build, version skipped, flag set")]
	[DataRow(true, false, false, true, DisplayName = "release build, nothing suppressing it")]
	[DataRow(true, false, true, false, DisplayName = "release build suppressed by the flag")]
	[DataRow(true, true, false, false, DisplayName = "release build suppressed by the version gate")]
	[DataRow(true, true, true, false, DisplayName = "release build suppressed by both")]
	public void ShouldExecuteReleaseCoversEveryCombination(bool shouldRelease, bool releaseSkipped, bool suppressedByFlag, bool expected)
	{
		bool actual = CiReleaseDecision.ShouldExecuteRelease(shouldRelease, releaseSkipped, suppressedByFlag);

		Assert.AreEqual(expected, actual);
	}

	[TestMethod]
	[DataRow(false, false, "false", DisplayName = "not a release build")]
	[DataRow(false, true, "false", DisplayName = "not a release build, version skipped")]
	[DataRow(true, false, "true", DisplayName = "release build, version moved")]
	[DataRow(true, true, "false", DisplayName = "release build, version skipped")]
	public void ShouldReleaseOutputReportsWhetherAReleaseIsWarranted(bool shouldRelease, bool releaseSkipped, string expected)
	{
		string actual = CiReleaseDecision.ShouldReleaseOutput(shouldRelease, releaseSkipped);

		Assert.AreEqual(expected, actual);
	}

	// The whole point of the split. A run that suppresses its own release must still tell later
	// jobs that a release is warranted, because a later job is the one that performs it. If this
	// ever becomes false, the winget and security jobs stop running and report nothing.
	[TestMethod]
	public void SuppressingTheReleaseDoesNotChangeWhatLaterJobsAreTold()
	{
		Assert.IsFalse(CiReleaseDecision.ShouldExecuteRelease(shouldRelease: true, releaseSkipped: false, suppressedByFlag: true));
		Assert.AreEqual("true", CiReleaseDecision.ShouldReleaseOutput(shouldRelease: true, releaseSkipped: false));
	}
}
