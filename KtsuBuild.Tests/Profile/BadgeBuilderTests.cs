// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Profile;

using KtsuBuild.Profile;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class BadgeBuilderTests
{
	[TestMethod]
	public void Build_WithEmptyLabelAndLogo_MatchesPublishedBadgeUrl() =>
		// Pinned against a URL taken from the live profile README.
		Assert.AreEqual(
			"https://img.shields.io/badge/-v1.1.4-181717?logo=github&logoColor=white",
			BadgeBuilder.Build(string.Empty, "v1.1.4", BadgeColors.GitHub, "github"));

	[TestMethod]
	public void Build_WithWingetColors_MatchesPublishedBadgeUrl() =>
		Assert.AreEqual(
			"https://img.shields.io/badge/-v1.0.21-0078D4?logo=windows&logoColor=white",
			BadgeBuilder.Build(string.Empty, "v1.0.21", BadgeColors.Winget, "windows"));

	[TestMethod]
	public void Build_WithStatusMessage_MatchesPublishedBadgeUrl() =>
		Assert.AreEqual(
			"https://img.shields.io/badge/-passing-2ea44f?logo=mdbook&logoColor=white",
			BadgeBuilder.Build(string.Empty, "passing", BadgeColors.Success, "mdbook"));

	[TestMethod]
	public void Build_WithoutLogo_OmitsQueryString() =>
		Assert.AreEqual(
			"https://img.shields.io/badge/-42-181717",
			BadgeBuilder.Build(string.Empty, "42", BadgeColors.GitHub));

	[TestMethod]
	public void Build_WithHyphenInMessage_EscapesItByDoubling() =>
		// shields.io reads a single hyphen as a segment delimiter, so a prerelease label would split
		// the badge into the wrong parts.
		Assert.AreEqual(
			"https://img.shields.io/badge/-v1.2.0--pre.1-004880?logo=nuget&logoColor=white",
			BadgeBuilder.Build(string.Empty, "v1.2.0-pre.1", BadgeColors.NuGet, "nuget"));

	[TestMethod]
	public void Build_WithLabel_PlacesItBeforeTheMessage() =>
		Assert.AreEqual(
			"https://img.shields.io/badge/Build-passing-2ea44f",
			BadgeBuilder.Build("Build", "passing", BadgeColors.Success));

	[TestMethod]
	public void Build_WithSpaceInLabel_EncodesIt() =>
		Assert.AreEqual(
			"https://img.shields.io/badge/Code+Coverage-90%25-2ea44f",
			BadgeBuilder.Build("Code Coverage", "90%", BadgeColors.Success));
}
