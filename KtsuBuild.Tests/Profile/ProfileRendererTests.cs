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
		Variants = [ShippedVariant.Library],
		ReleaseStableVersion = "1.0.0",
		CommitActivity = 5,
		HasWorkflowRun = true,
		WorkflowConclusion = "success",
		ReadmePasses = true,
	};

	[TestMethod]
	public void RenderRow_RendersEveryColumnInOrder() =>
		Assert.AreEqual(
			"|[BlastMerge](https://github.com/ktsu-dev/BlastMerge)" +
			"|![lib](https://img.shields.io/badge/-lib-004880)![cli](https://img.shields.io/badge/-cli-3B3B3B)" +
			"|![NuGet Version](https://img.shields.io/badge/-v1.1.4-004880?logo=nuget&logoColor=white)" +
			"| " +
			"|![winget](https://img.shields.io/badge/-v1.0.21-0078D4?logo=windows&logoColor=white)" +
			"|![SDK](https://img.shields.io/badge/-2.25.0-dbab09)" +
			"|![Activity](https://img.shields.io/badge/-37-181717?logo=github&logoColor=white)" +
			"|![Status](https://img.shields.io/badge/-failing-d73a4a?logo=github&logoColor=white)" +
			"|![README](https://img.shields.io/badge/-passing-2ea44f?logo=mdbook&logoColor=white)|\n",
			ProfileRenderer.RenderRow(new RepoFacts
			{
				Owner = "ktsu-dev",
				Name = "BlastMerge",
				Variants = [ShippedVariant.Library, ShippedVariant.ConsoleApp],
				NuGetStableVersion = "1.1.4",
				WingetVersion = "1.0.21",
				SdkVersion = "2.25.0",
				SdkIsCurrent = false,
				CommitActivity = 37,
				HasWorkflowRun = true,
				WorkflowConclusion = "failure",
				ReadmePasses = true,
			}));

	[TestMethod]
	public void RenderRow_WithCurrentSdk_UsesTheSuccessColor() =>
		Assert.Contains(
			"![SDK](https://img.shields.io/badge/-2.28.0-2ea44f)",
			ProfileRenderer.RenderRow(Library("Widget") with { SdkVersion = "2.28.0", SdkIsCurrent = true }));

	[TestMethod]
	public void RenderRow_WithOutdatedSdk_UsesTheWarningColor() =>
		Assert.Contains(
			"![SDK](https://img.shields.io/badge/-2.8.0-dbab09)",
			ProfileRenderer.RenderRow(Library("Widget") with { SdkVersion = "2.8.0", SdkIsCurrent = false }));

	[TestMethod]
	public void RenderRow_WithNoSdkPin_LeavesTheCellBlank() =>
		Assert.IsFalse(ProfileRenderer.RenderRow(Library("Widget")).Contains("SDK", StringComparison.Ordinal));

	[TestMethod]
	public void RenderRow_RendersOneBadgePerVariant()
	{
		string row = ProfileRenderer.RenderRow(Library("KtsuBuild") with
		{
			Variants = [ShippedVariant.Library, ShippedVariant.Tool],
		});

		Assert.Contains("![lib](https://img.shields.io/badge/-lib-004880)", row);
		Assert.Contains("![tool](https://img.shields.io/badge/-tool-512BD4)", row);
	}

	[TestMethod]
	public void RenderRow_WithNoVariants_LeavesTheShipsCellBlank()
	{
		string row = ProfileRenderer.RenderRow(Library("Widget") with { Variants = [] });

		Assert.IsFalse(row.Contains("badge/-lib", StringComparison.Ordinal));
	}

	[TestMethod]
	public void RenderRow_PrefersNuGetVersionOverReleaseTag()
	{
		string row = ProfileRenderer.RenderRow(Library("Extensions") with { NuGetStableVersion = "1.6.8" });

		Assert.Contains("![NuGet Version](https://img.shields.io/badge/-v1.6.8-004880?logo=nuget&logoColor=white)", row);
		Assert.IsFalse(row.Contains("GitHub Version", StringComparison.Ordinal));
	}

	[TestMethod]
	public void RenderRow_WithNewerPrerelease_ShowsIt() =>
		Assert.Contains(
			"![NuGet Prerelease](https://img.shields.io/badge/-v1.1.0--pre.1-004880?logo=nuget&logoColor=white)",
			ProfileRenderer.RenderRow(Library("Widget") with
			{
				NuGetStableVersion = "1.0.0",
				NuGetPrereleaseVersion = "1.1.0-pre.1",
			}));

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
	public void RenderRow_WithNoCommitActivity_LeavesTheCellBlank() =>
		Assert.IsFalse(ProfileRenderer.RenderRow(Library("Widget") with { CommitActivity = 0 })
			.Contains("Activity", StringComparison.Ordinal));

	[TestMethod]
	public void RenderRow_WithNoWorkflowRun_LeavesTheCellBlank() =>
		Assert.IsFalse(ProfileRenderer.RenderRow(Library("Widget") with { HasWorkflowRun = false })
			.Contains("Status", StringComparison.Ordinal));

	[TestMethod]
	[DataRow("cancelled", "cancelled", "6e7681")]
	[DataRow(null, "unknown", "dbab09")]
	[DataRow("timed_out", "unknown", "dbab09")]
	public void RenderRow_MapsWorkflowConclusionToBadge(string? conclusion, string message, string color) =>
		Assert.Contains($"![Status](https://img.shields.io/badge/-{message}-{color}?logo=github&logoColor=white)", ProfileRenderer.RenderRow(Library("Widget") with { WorkflowConclusion = conclusion }));

	[TestMethod]
	public void RenderRow_WithShortReadme_ShowsFailing() =>
		Assert.Contains("![README](https://img.shields.io/badge/-failing-d73a4a?logo=mdbook&logoColor=white)", ProfileRenderer.RenderRow(Library("Widget") with { ReadmePasses = false }));

	[TestMethod]
	public void RenderRow_HasOneCellPerHeaderColumn()
	{
		string row = ProfileRenderer.RenderRow(Library("Widget"));

		Assert.AreEqual(9, row.TrimEnd('\n').Split('|')[1..^1].Length);
	}

	[TestMethod]
	public void Render_ListsEveryRepositoryInOneTable()
	{
		string rendered = ProfileRenderer.Render("## Project Status\n", [
			Library("Alpha"),
			Library("Bravo") with { Variants = [ShippedVariant.App] },
			Library("Charlie"),
		]);

		Assert.Contains("\n| Repo | Ships | Stable | Prerelease | winget | SDK | Activity | Status | README |\n|------|-------|--------|------------|--------|-----|----------|--------|--------|\n|[Alpha]", rendered);
		Assert.HasCount(2, rendered.Split("| Repo |"), "One header occurrence splits the text into two parts, so there is exactly one table");
	}

	[TestMethod]
	public void Render_PreservesInputOrder()
	{
		string rendered = ProfileRenderer.Render(string.Empty, [Library("Charlie"), Library("Alpha"), Library("Bravo")]);

		Assert.IsLessThan(
			rendered.IndexOf("[Alpha]", StringComparison.Ordinal),
			rendered.IndexOf("[Charlie]", StringComparison.Ordinal),
			"Rows should keep the order they were gathered in, not be re-sorted");
	}

	[TestMethod]
	public void Render_DoesNotSeparateApplicationsFromLibraries()
	{
		// The Ships column says what each repository is, so an application between two libraries stays
		// where the alphabet puts it.
		string rendered = ProfileRenderer.Render(string.Empty, [
			Library("Alpha"),
			Library("Bravo") with { Variants = [ShippedVariant.App] },
			Library("Charlie"),
		]);

		Assert.IsLessThan(
			rendered.IndexOf("[Bravo]", StringComparison.Ordinal),
			rendered.IndexOf("[Alpha]", StringComparison.Ordinal));
		Assert.IsLessThan(
			rendered.IndexOf("[Charlie]", StringComparison.Ordinal),
			rendered.IndexOf("[Bravo]", StringComparison.Ordinal));
	}

	[TestMethod]
	public void Render_WithNoRepositories_ReturnsTemplatePlusTrailingNewline() =>
		Assert.AreEqual("## Project Status\n\n", ProfileRenderer.Render("## Project Status\n", []));

	[TestMethod]
	public void Render_EndsWithABlankLineAfterTheLastRow() =>
		Assert.IsTrue(
			ProfileRenderer.Render(string.Empty, [Library("Alpha")]).EndsWith(")|\n\n", StringComparison.Ordinal),
			"The file should end with a newline of its own after the last row");
}
