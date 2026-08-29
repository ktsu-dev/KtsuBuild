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
	private const string ApplicationsHeader =
		"\n### Applications\n\n| Repo | Stable | winget | Activity | Status | README |\n|------|--------|--------|----------|--------|--------|\n";

	private const string LibrariesHeader =
		"\n### Libraries\n\n| Repo | Stable | Prerelease | Activity | Status | README |\n|------|--------|------------|----------|--------|--------|\n";

	private const string EmptyCell = "| ";

	/// <summary>
	/// Renders the full profile README.
	/// </summary>
	/// <param name="template">The template content the tables are appended to.</param>
	/// <param name="repositories">The repositories to list, in the order they should appear. Applications
	/// and libraries are split into separate tables while each keeps its relative order.</param>
	/// <returns>The rendered markdown, using LF line endings.</returns>
	public static string Render(string template, IEnumerable<RepoFacts> repositories)
	{
		Ensure.NotNull(template);
		Ensure.NotNull(repositories);

		List<RepoFacts> all = [.. repositories];
		List<RepoFacts> applications = [.. all.Where(static r => r.IsApplication)];
		List<RepoFacts> libraries = [.. all.Where(static r => !r.IsApplication)];

		StringBuilder builder = new(template);

		if (applications.Count > 0)
		{
			builder.Append(ApplicationsHeader);
			foreach (RepoFacts repository in applications)
			{
				builder.Append(RenderRow(repository));
			}
		}

		if (libraries.Count > 0)
		{
			builder.Append(LibrariesHeader);
			foreach (RepoFacts repository in libraries)
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
		row.Append(RenderStableCell(repository));
		row.Append(repository.IsApplication ? RenderWingetCell(repository) : RenderPrereleaseCell(repository));
		row.Append(RenderActivityCell(repository));
		row.Append(RenderStatusCell(repository));
		row.Append(RenderReadmeCell(repository));
		row.Append("|\n");

		return row.ToString();
	}

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
			return $"|![GitHub Version]({BadgeBuilder.Build(string.Empty, $"v{repository.ReleaseStableVersion}", BadgeColors.GitHub, "github")})";
		}

		return EmptyCell;
	}

	/// <summary>
	/// Renders the prerelease cell, showing a prerelease only when it is actually newer than the
	/// stable version. A prerelease at or below stable has already been superseded.
	/// </summary>
	/// <param name="repository">The repository to render.</param>
	/// <returns>The markdown cell.</returns>
	private static string RenderPrereleaseCell(RepoFacts repository)
	{
		if (!string.IsNullOrEmpty(repository.NuGetPrereleaseVersion) &&
			SemanticVersion.IsGreater(repository.NuGetPrereleaseVersion, repository.NuGetStableVersion))
		{
			return $"|![NuGet Prerelease]({BadgeBuilder.Build(string.Empty, $"v{repository.NuGetPrereleaseVersion}", BadgeColors.NuGet, "nuget")})";
		}

		if (!string.IsNullOrEmpty(repository.ReleasePrereleaseVersion) &&
			SemanticVersion.IsGreater(repository.ReleasePrereleaseVersion, repository.ReleaseStableVersion))
		{
			return $"|![GitHub Prerelease]({BadgeBuilder.Build(string.Empty, $"v{repository.ReleasePrereleaseVersion}", BadgeColors.GitHub, "github")})";
		}

		return EmptyCell;
	}

	/// <summary>
	/// Renders the winget availability cell.
	/// </summary>
	/// <param name="repository">The repository to render.</param>
	/// <returns>The markdown cell.</returns>
	private static string RenderWingetCell(RepoFacts repository) =>
		string.IsNullOrEmpty(repository.WingetVersion)
			? EmptyCell
			: $"|![winget]({BadgeBuilder.Build(string.Empty, $"v{repository.WingetVersion}", BadgeColors.Winget, "windows")})";

	/// <summary>
	/// Renders the commit activity cell, left blank when there has been no activity in the window.
	/// </summary>
	/// <param name="repository">The repository to render.</param>
	/// <returns>The markdown cell.</returns>
	private static string RenderActivityCell(RepoFacts repository) =>
		repository.CommitActivity > 0
			? $"|![Activity]({BadgeBuilder.Build(string.Empty, repository.CommitActivity.ToString(CultureInfo.InvariantCulture), BadgeColors.GitHub, "github")})"
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

		return $"|![Status]({BadgeBuilder.Build(string.Empty, message, color, "github")})";
	}

	/// <summary>
	/// Renders the README quality cell.
	/// </summary>
	/// <param name="repository">The repository to render.</param>
	/// <returns>The markdown cell.</returns>
	private static string RenderReadmeCell(RepoFacts repository)
	{
		(string message, string color) = repository.ReadmePasses
			? ("passing", BadgeColors.Success)
			: ("failing", BadgeColors.Failure);

		return $"|![README]({BadgeBuilder.Build(string.Empty, message, color, "mdbook")})";
	}
}
