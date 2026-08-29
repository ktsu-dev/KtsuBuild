// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Profile;

using KtsuBuild.Profile;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class SemanticVersionTests
{
	[TestMethod]
	[DataRow("1.0.1", "1.0.0")]
	[DataRow("1.1.0", "1.0.99")]
	[DataRow("2.0.0", "1.99.99")]
	public void IsGreater_WithHigherCoreVersion_ReturnsTrue(string version, string reference) =>
		Assert.IsTrue(SemanticVersion.IsGreater(version, reference));

	[TestMethod]
	[DataRow("1.0.0", "1.0.1")]
	[DataRow("1.0.0", "1.0.0")]
	public void IsGreater_WithLowerOrEqualCoreVersion_ReturnsFalse(string version, string reference) =>
		Assert.IsFalse(SemanticVersion.IsGreater(version, reference));

	[TestMethod]
	public void IsGreater_WithStableAgainstSameCorePrerelease_ReturnsTrue() =>
		Assert.IsTrue(SemanticVersion.IsGreater("1.2.0", "1.2.0-pre.1"));

	[TestMethod]
	public void IsGreater_WithPrereleaseAgainstSameCoreStable_ReturnsFalse() =>
		Assert.IsFalse(SemanticVersion.IsGreater("1.2.0-pre.1", "1.2.0"));

	[TestMethod]
	public void IsGreater_WithPrereleaseOnHigherCore_ReturnsTrue() =>
		Assert.IsTrue(SemanticVersion.IsGreater("1.3.0-pre.1", "1.2.0"));

	[TestMethod]
	[DataRow("1.0.0-pre.2", "1.0.0-pre.1")]
	[DataRow("1.0.0-pre.10", "1.0.0-pre.9")]
	[DataRow("1.0.0-beta", "1.0.0-alpha")]
	[DataRow("1.0.0-alpha.1", "1.0.0-alpha")]
	[DataRow("1.0.0-alpha", "1.0.0-1")]
	public void IsGreater_ComparesPrereleaseIdentifiers(string version, string reference) =>
		Assert.IsTrue(SemanticVersion.IsGreater(version, reference));

	[TestMethod]
	public void IsGreater_ComparesNumericPrereleaseIdentifiersNumerically() =>
		Assert.IsFalse(SemanticVersion.IsGreater("1.0.0-pre.9", "1.0.0-pre.10"));

	[TestMethod]
	[DataRow("v1.0.1", "1.0.0")]
	[DataRow("1.0.1", "v1.0.0")]
	[DataRow("v1.0.1", "v1.0.0")]
	public void IsGreater_IgnoresLeadingVersionPrefix(string version, string reference) =>
		Assert.IsTrue(SemanticVersion.IsGreater(version, reference));

	[TestMethod]
	public void IsGreater_IgnoresBuildMetadata() =>
		Assert.IsFalse(SemanticVersion.IsGreater("1.0.0+build.5", "1.0.0+build.1"));

	[TestMethod]
	public void IsGreater_WithMissingVersion_ReturnsFalse() =>
		Assert.IsFalse(SemanticVersion.IsGreater(null, "1.0.0"));

	[TestMethod]
	public void IsGreater_WithMissingReference_ReturnsTrue() =>
		Assert.IsTrue(SemanticVersion.IsGreater("1.0.0", null));

	[TestMethod]
	public void IsGreater_WithUnparseableVersion_ReturnsFalse() =>
		Assert.IsFalse(SemanticVersion.IsGreater("not-a-version", "1.0.0"));

	[TestMethod]
	public void IsGreater_OrdersWingetPatchVersionsNumerically() =>
		// Sorted as text, 1.0.9 would beat 1.0.21. This is what the winget lookup relies on.
		Assert.IsTrue(SemanticVersion.IsGreater("1.0.21", "1.0.9"));
}
