// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Profile;

using KtsuBuild.Abstractions;
using KtsuBuild.Profile;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TemplateLinksTests
{
	private static GitHubRepository Live(string name) => new(name, "main", false);

	private static GitHubRepository Archived(string name) => new(name, "main", true);

	[TestMethod]
	public void FindLinkedRepositories_ReadsMarkdownLinks() =>
		Assert.AreEqual(
			"Semantics,PreciseNumber",
			string.Join(",", TemplateLinks.FindLinkedRepositories(
				"- **[Semantics](https://github.com/ktsu-dev/Semantics)**: things\n" +
				"- **[PreciseNumber](https://github.com/ktsu-dev/PreciseNumber)**: numbers\n",
				"ktsu-dev")));

	[TestMethod]
	public void FindLinkedRepositories_IgnoresOtherOwners() =>
		// The template credits its author, whose profile link is not a repository in the organization.
		Assert.IsEmpty(TemplateLinks.FindLinkedRepositories(
			"maintained by [Matt Edmondson](https://github.com/matt-edmondson/Something)",
			"ktsu-dev"));

	[TestMethod]
	public void FindLinkedRepositories_IgnoresABareOrganizationLink() =>
		Assert.IsEmpty(TemplateLinks.FindLinkedRepositories(
			"the public profile page at https://github.com/ktsu-dev.",
			"ktsu-dev"));

	[TestMethod]
	public void FindLinkedRepositories_DeduplicatesRepeatedLinks() =>
		Assert.AreEqual(
			"Semantics",
			string.Join(",", TemplateLinks.FindLinkedRepositories(
				"[a](https://github.com/ktsu-dev/Semantics) and [b](https://github.com/ktsu-dev/Semantics)",
				"ktsu-dev")));

	[TestMethod]
	public void FindLinkedRepositories_ReadsAPathBeyondTheRepository() =>
		Assert.AreEqual(
			"ImGuiApp",
			string.Join(",", TemplateLinks.FindLinkedRepositories(
				"https://github.com/ktsu-dev/ImGuiApp/commits/main",
				"ktsu-dev")));

	[TestMethod]
	public void FindLinkedRepositories_MatchesTheOwnerCaseInsensitively() =>
		Assert.AreEqual(
			"Semantics",
			string.Join(",", TemplateLinks.FindLinkedRepositories(
				"https://github.com/KTSU-DEV/Semantics",
				"ktsu-dev")));

	[TestMethod]
	public void FindRetired_WithHealthyLinks_ReturnsNothing() =>
		Assert.IsEmpty(TemplateLinks.FindRetired(
			"[a](https://github.com/ktsu-dev/Semantics) [b](https://github.com/ktsu-dev/Extensions)",
			"ktsu-dev",
			[Live("Semantics"), Live("Extensions")]));

	[TestMethod]
	public void FindRetired_FlagsAnArchivedRepository() =>
		// The page promoted four archived repositories before this check existed.
		Assert.AreEqual(
			"PersistenceProvider",
			string.Join(",", TemplateLinks.FindRetired(
				"[a](https://github.com/ktsu-dev/Semantics) [b](https://github.com/ktsu-dev/PersistenceProvider)",
				"ktsu-dev",
				[Live("Semantics"), Archived("PersistenceProvider")])));

	[TestMethod]
	public void FindRetired_FlagsARepositoryTheListingDoesNotKnow() =>
		// Renamed, deleted, or made private. All of them leave a broken promise on the page.
		Assert.AreEqual(
			"Gone",
			string.Join(",", TemplateLinks.FindRetired(
				"[a](https://github.com/ktsu-dev/Gone)",
				"ktsu-dev",
				[Live("Semantics")])));

	[TestMethod]
	public void FindRetired_ReportsEveryOffender() =>
		Assert.AreEqual(
			"PersistenceProvider,SerializationProvider,FileSystemProvider,UniversalSerializer",
			string.Join(",", TemplateLinks.FindRetired(
				"[1](https://github.com/ktsu-dev/PersistenceProvider) [2](https://github.com/ktsu-dev/SerializationProvider) " +
				"[3](https://github.com/ktsu-dev/FileSystemProvider) [4](https://github.com/ktsu-dev/UniversalSerializer)",
				"ktsu-dev",
				[
					Archived("PersistenceProvider"),
					Archived("SerializationProvider"),
					Archived("FileSystemProvider"),
					Archived("UniversalSerializer"),
				])));

	[TestMethod]
	public void FindRetired_DoesNotFlagALiveRepositoryLeftOutOfTheTable() =>
		// Sdk is excluded from the generated table but is live and rightly linked in the prose.
		Assert.IsEmpty(TemplateLinks.FindRetired(
			"[Sdk](https://github.com/ktsu-dev/Sdk)",
			"ktsu-dev",
			[Live("Sdk")]));

	[TestMethod]
	public void FindRetired_WithNoLinks_ReturnsNothing() =>
		Assert.IsEmpty(TemplateLinks.FindRetired("no links here", "ktsu-dev", [Live("Semantics")]));
}
