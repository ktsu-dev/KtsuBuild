// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Profile;

using KtsuBuild.Profile;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ProfileRendererTests
{
	private static RepoFacts Library(string name) => new()
	{
		Owner = "ktsu-dev",
		Name = name,
		ReleaseStableVersion = "1.0.0",
		CommitActivity = 5,
		HasWorkflowRun = true,
		WorkflowConclusion = "success",
		ReadmePasses = true,
	};

	[TestMethod]
	public void RenderRow_ForApplication_MatchesPublishedRow() =>
		// Pinned against the BlastMerge row in the live profile README.
		Assert.AreEqual(
			"|[BlastMerge](https://github.com/ktsu-dev/BlastMerge)" +
			"|![GitHub Version](https://img.shields.io/badge/-v1.1.4-181717?logo=github&logoColor=white)" +
			"|![winget](https://img.shields.io/badge/-v1.0.21-0078D4?logo=windows&logoColor=white)" +
			"|![Activity](https://img.shields.io/badge/-37-181717?logo=github&logoColor=white)" +
			"|![Status](https://img.shields.io/badge/-failing-d73a4a?logo=github&logoColor=white)" +
			"|![README](https://img.shields.io/badge/-passing-2ea44f?logo=mdbook&logoColor=white)|\n",
			ProfileRenderer.RenderRow(new RepoFacts
			{
				Owner = "ktsu-dev",
				Name = "BlastMerge",
				IsApplication = true,
				ReleaseStableVersion = "1.1.4",
				WingetVersion = "1.0.21",
				CommitActivity = 37,
				HasWorkflowRun = true,
				WorkflowConclusion = "failure",
				ReadmePasses = true,
			}));

	[TestMethod]
	public void RenderRow_ForLibrary_MatchesPublishedRow() =>
		// Pinned against the AppDataStorage row in the live profile README.
		Assert.AreEqual(
			"|[AppDataStorage](https://github.com/ktsu-dev/AppDataStorage)" +
			"|![GitHub Version](https://img.shields.io/badge/-v1.16.46-181717?logo=github&logoColor=white)" +
			"| " +
			"|![Activity](https://img.shields.io/badge/-94-181717?logo=github&logoColor=white)" +
			"|![Status](https://img.shields.io/badge/-passing-2ea44f?logo=github&logoColor=white)" +
			"|![README](https://img.shields.io/badge/-passing-2ea44f?logo=mdbook&logoColor=white)|\n",
			ProfileRenderer.RenderRow(new RepoFacts
			{
				Owner = "ktsu-dev",
				Name = "AppDataStorage",
				ReleaseStableVersion = "1.16.46",
				CommitActivity = 94,
				HasWorkflowRun = true,
				WorkflowConclusion = "success",
				ReadmePasses = true,
			}));

	[TestMethod]
	public void RenderRow_PrefersNuGetVersionOverReleaseTag()
	{
		string row = ProfileRenderer.RenderRow(Library("Extensions") with { NuGetStableVersion = "1.6.8" });

		StringAssert.Contains(row, "![NuGet Version](https://img.shields.io/badge/-v1.6.8-004880?logo=nuget&logoColor=white)");
		Assert.IsFalse(row.Contains("GitHub Version", StringComparison.Ordinal));
	}

	[TestMethod]
	public void RenderRow_WithNewerPrerelease_ShowsIt() =>
		StringAssert.Contains(
			ProfileRenderer.RenderRow(Library("Widget") with
			{
				NuGetStableVersion = "1.0.0",
				NuGetPrereleaseVersion = "1.1.0-pre.1",
			}),
			"![NuGet Prerelease](https://img.shields.io/badge/-v1.1.0--pre.1-004880?logo=nuget&logoColor=white)");

	[TestMethod]
	public void RenderRow_WithSupersededPrerelease_LeavesTheCellBlank()
	{
		string row = ProfileRenderer.RenderRow(Library("Widget") with
		{
			NuGetStableVersion = "1.1.0",
			NuGetPrereleaseVersion = "1.1.0-pre.1",
		});

		Assert.IsFalse(row.Contains("Prerelease", StringComparison.Ordinal));
	}

	[TestMethod]
	public void RenderRow_WithNoCommitActivity_LeavesTheCellBlank()
	{
		string row = ProfileRenderer.RenderRow(Library("Widget") with { CommitActivity = 0 });

		Assert.IsFalse(row.Contains("Activity", StringComparison.Ordinal));
	}

	[TestMethod]
	public void RenderRow_WithNoWorkflowRun_LeavesTheCellBlank()
	{
		string row = ProfileRenderer.RenderRow(Library("Widget") with { HasWorkflowRun = false });

		Assert.IsFalse(row.Contains("Status", StringComparison.Ordinal));
	}

	[TestMethod]
	[DataRow("cancelled", "cancelled", "6e7681")]
	[DataRow(null, "unknown", "dbab09")]
	[DataRow("timed_out", "unknown", "dbab09")]
	public void RenderRow_MapsWorkflowConclusionToBadge(string? conclusion, string message, string color) =>
		StringAssert.Contains(
			ProfileRenderer.RenderRow(Library("Widget") with { WorkflowConclusion = conclusion }),
			$"![Status](https://img.shields.io/badge/-{message}-{color}?logo=github&logoColor=white)");

	[TestMethod]
	public void RenderRow_WithShortReadme_ShowsFailing() =>
		StringAssert.Contains(
			ProfileRenderer.RenderRow(Library("Widget") with { ReadmePasses = false }),
			"![README](https://img.shields.io/badge/-failing-d73a4a?logo=mdbook&logoColor=white)");

	[TestMethod]
	public void Render_SplitsApplicationsAndLibrariesIntoSeparateTables()
	{
		string rendered = ProfileRenderer.Render("## Project Status\n", [
			Library("Alpha"),
			Library("Bravo") with { IsApplication = true },
			Library("Charlie"),
		]);

		StringAssert.Contains(rendered, "\n### Applications\n\n| Repo | Stable | winget | Activity | Status | README |\n|------|--------|--------|----------|--------|--------|\n|[Bravo]");
		StringAssert.Contains(rendered, "\n### Libraries\n\n| Repo | Stable | Prerelease | Activity | Status | README |\n|------|--------|------------|----------|--------|--------|\n|[Alpha]");
		Assert.IsTrue(
			rendered.IndexOf("### Applications", StringComparison.Ordinal) < rendered.IndexOf("### Libraries", StringComparison.Ordinal),
			"Applications should be listed before libraries");
	}

	[TestMethod]
	public void Render_PreservesInputOrderWithinEachTable()
	{
		string rendered = ProfileRenderer.Render(string.Empty, [Library("Charlie"), Library("Alpha"), Library("Bravo")]);

		Assert.IsTrue(
			rendered.IndexOf("[Charlie]", StringComparison.Ordinal) < rendered.IndexOf("[Alpha]", StringComparison.Ordinal),
			"Rows should keep the order they were gathered in, not be re-sorted");
	}

	[TestMethod]
	public void Render_WithNoApplications_OmitsTheApplicationsTable()
	{
		string rendered = ProfileRenderer.Render(string.Empty, [Library("Alpha")]);

		Assert.IsFalse(rendered.Contains("### Applications", StringComparison.Ordinal));
		StringAssert.Contains(rendered, "### Libraries");
	}

	[TestMethod]
	public void Render_WithNoRepositories_ReturnsTemplatePlusTrailingNewline() =>
		Assert.AreEqual("## Project Status\n\n", ProfileRenderer.Render("## Project Status\n", []));

	[TestMethod]
	public void Render_EndsWithABlankLineAfterTheLastRow()
	{
		string rendered = ProfileRenderer.Render(string.Empty, [Library("Alpha")]);

		Assert.IsTrue(rendered.EndsWith(")|\n\n", StringComparison.Ordinal), "The file should end with a newline of its own after the last row");
	}
}
