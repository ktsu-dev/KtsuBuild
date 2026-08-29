// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Profile;

using System.Globalization;
using System.Text;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Renders repository facts into the organization profile README.
/// </summary>
/// <remarks>
/// Pure. Given the same template and facts it always produces the same markdown, which is what makes
/// the generator testable without network access.
/// </remarks>
public static class ProfileRenderer
{
	private const string TableHeader =
		"\n| Repo | Ships | Stable | SDK | Stars | Activity | Status |\n" +
		"|------|-------|--------|-----|-------|----------|--------|\n";

	private const string EmptyCell = "| ";

	private const string GitHubLogo = "github";

	/// <summary>
	/// Renders the full profile README.
	/// </summary>
	/// <param name="template">The template content the table is appended to.</param>
	/// <param name="repositories">The repositories to list, in the order they should appear.</param>
	/// <returns>The rendered markdown, using LF line endings.</returns>
	/// <remarks>
	/// One table rather than one per kind. The Ships column says what each repository is, so splitting
	/// the list would only make a reader guess which half to look in, and a repository that ships both
	/// a library and an application has no correct half.
	/// </remarks>
	public static string Render(string template, IEnumerable<RepoFacts> repositories)
	{
		Ensure.NotNull(template);
		Ensure.NotNull(repositories);

		List<RepoFacts> all = [.. repositories];
		StringBuilder builder = new(template);

		if (all.Count > 0)
		{
			builder.Append(TableHeader);
			foreach (RepoFacts repository in all)
			{
				builder.Append(RenderRow(repository));
			}
		}

		// The file ends with a newline of its own, after the newline that terminates the last row.
		builder.Append('\n');

		return builder.ToString();
	}

	/// <summary>
	/// Renders a single table row, terminated by a newline.
	/// </summary>
	/// <param name="repository">The repository to render.</param>
	/// <returns>The markdown table row.</returns>
	public static string RenderRow(RepoFacts repository)
	{
		Ensure.NotNull(repository);

		StringBuilder row = new();
		row.Append(CultureInfo.InvariantCulture, $"|[{repository.Name}](https://github.com/{repository.Owner}/{repository.Name})");
		row.Append(RenderShipsCell(repository));
		row.Append(RenderStableCell(repository));
		row.Append(RenderSdkCell(repository));
		row.Append(RenderStarsCell(repository));
		row.Append(RenderActivityCell(repository));
		row.Append(RenderStatusCell(repository));
		row.Append("|\n");

		return row.ToString();
	}

	/// <summary>
	/// Renders the badges for what the repository ships.
	/// </summary>
	/// <param name="repository">The repository to render.</param>
	/// <returns>The markdown cell.</returns>
	private static string RenderShipsCell(RepoFacts repository)
	{
		if (repository.Variants.Count == 0)
		{
			return EmptyCell;
		}

		StringBuilder cell = new("|");
		foreach (ShippedVariant variant in repository.Variants)
		{
			string label = ShippedVariants.ToLabel(variant);
			cell.Append(CultureInfo.InvariantCulture, $"![{label}]({BadgeBuilder.Build(string.Empty, label, ColorFor(variant))})");
		}

		return cell.ToString();
	}

	/// <summary>
	/// Gets the badge color for a variant. Packages a caller references are blue, programs a user runs
	/// are purple, so the column separates the two at a glance.
	/// </summary>
	/// <param name="variant">The variant to color.</param>
	/// <returns>The hex color.</returns>
	private static string ColorFor(ShippedVariant variant) => variant switch
	{
		ShippedVariant.Library => BadgeColors.NuGet,
		ShippedVariant.Tool => BadgeColors.Tool,
		ShippedVariant.App => BadgeColors.App,
		_ => BadgeColors.ConsoleApp,
	};

	/// <summary>
	/// Renders the stable version cell, preferring the NuGet version over the GitHub release tag
	/// because the package is what consumers install.
	/// </summary>
	/// <param name="repository">The repository to render.</param>
	/// <returns>The markdown cell.</returns>
	private static string RenderStableCell(RepoFacts repository)
	{
		if (!string.IsNullOrEmpty(repository.NuGetStableVersion))
		{
			return $"|![NuGet Version]({BadgeBuilder.Build(string.Empty, $"v{repository.NuGetStableVersion}", BadgeColors.NuGet, "nuget")})";
		}

		if (!string.IsNullOrEmpty(repository.ReleaseStableVersion))
		{
			return $"|![GitHub Version]({BadgeBuilder.Build(string.Empty, $"v{repository.ReleaseStableVersion}", BadgeColors.GitHub, GitHubLogo)})";
		}

		return EmptyCell;
	}

	/// <summary>
	/// Renders the pinned SDK version, colored by whether the repository has kept up with the newest
	/// published one.
	/// </summary>
	/// <param name="repository">The repository to render.</param>
	/// <returns>The markdown cell.</returns>
	private static string RenderSdkCell(RepoFacts repository)
	{
		if (string.IsNullOrEmpty(repository.SdkVersion))
		{
			return EmptyCell;
		}

		string color = repository.SdkIsCurrent ? BadgeColors.Success : BadgeColors.Warning;
		return $"|![SDK]({BadgeBuilder.Build(string.Empty, repository.SdkVersion, color)})";
	}

	/// <summary>
	/// Renders the stargazer count, left blank for a repository nobody has starred yet.
	/// </summary>
	/// <param name="repository">The repository to render.</param>
	/// <returns>The markdown cell.</returns>
	/// <remarks>Gold and unlogoed, so it does not read as another of the dark GitHub badges.</remarks>
	private static string RenderStarsCell(RepoFacts repository) =>
		repository.Stars > 0
			? $"|![Stars]({BadgeBuilder.Build(string.Empty, repository.Stars.ToString(CultureInfo.InvariantCulture), BadgeColors.Star)})"
			: EmptyCell;

	/// <summary>
	/// Renders the commit activity cell, left blank when there has been no activity in the window.
	/// </summary>
	/// <param name="repository">The repository to render.</param>
	/// <returns>The markdown cell.</returns>
	private static string RenderActivityCell(RepoFacts repository) =>
		repository.CommitActivity > 0
			? $"|![Activity]({BadgeBuilder.Build(string.Empty, repository.CommitActivity.ToString(CultureInfo.InvariantCulture), BadgeColors.GitHub, GitHubLogo)})"
			: EmptyCell;

	/// <summary>
	/// Renders the build status cell.
	/// </summary>
	/// <param name="repository">The repository to render.</param>
	/// <returns>The markdown cell.</returns>
	private static string RenderStatusCell(RepoFacts repository)
	{
		if (!repository.HasWorkflowRun)
		{
			return EmptyCell;
		}

		(string message, string color) = repository.WorkflowConclusion switch
		{
			"success" => ("passing", BadgeColors.Success),
			"failure" => ("failing", BadgeColors.Failure),
			"cancelled" => ("cancelled", BadgeColors.Cancelled),
			_ => ("unknown", BadgeColors.Warning),
		};

		return $"|![Status]({BadgeBuilder.Build(string.Empty, message, color, GitHubLogo)})";
	}
}
