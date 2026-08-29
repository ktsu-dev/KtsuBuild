// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Profile;

using System.Text.RegularExpressions;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Decides which project in a repository is the primary deliverable, and whether that deliverable is
/// an application or a library.
/// </summary>
public static partial class ProjectClassifier
{
	/// <summary>
	/// Picks the project file that best represents what a repository ships.
	/// </summary>
	/// <param name="repositoryName">The repository name, used to recognize conventionally named projects.</param>
	/// <param name="projectPaths">Every project file path in the repository, relative to its root.</param>
	/// <returns>The chosen path, or <see langword="null"/> when the repository has no projects.</returns>
	/// <remarks>
	/// An application project wins over a library project in the same repository, because a repository
	/// that ships an app is about the app even when a supporting library sits beside it.
	/// </remarks>
	public static string? SelectPrimaryProject(string repositoryName, IEnumerable<string> projectPaths)
	{
		Ensure.NotNull(repositoryName);
		Ensure.NotNull(projectPaths);

		List<string> all = [.. projectPaths];
		if (all.Count == 0)
		{
			return null;
		}

		// Supporting projects say nothing about what the repository ships. If they are all there is,
		// fall back to the unfiltered list rather than reporting nothing.
		List<string> candidates = [.. all.Where(static path => !SupportingProjectRegex().IsMatch(path))];
		if (candidates.Count == 0)
		{
			candidates = all;
		}

		string escapedName = Regex.Escape(repositoryName);

		string? application = FirstMatch(candidates, $@"(^|/){escapedName}\.(ConsoleApp|App)\.csproj$");
		if (application is not null)
		{
			return application;
		}

		string? library = FirstMatch(candidates, $@"(^|/){escapedName}\.csproj$")
			?? FirstMatch(candidates, $@"(^|/){escapedName}\.Core\.csproj$");
		if (library is not null)
		{
			return library;
		}

		// Nothing matches the naming convention, so the shallowest path is the best guess at the
		// project a reader would consider primary. Ties break on the path itself to stay deterministic.
		return candidates
			.OrderBy(static path => path.Length)
			.ThenBy(static path => path, StringComparer.Ordinal)
			.First();
	}

	/// <summary>
	/// Determines whether a project file declares one of the application SDK variants.
	/// </summary>
	/// <param name="projectContent">The contents of the project file.</param>
	/// <returns><see langword="true"/> when the project builds an application.</returns>
	public static bool IsApplication(string? projectContent) =>
		!string.IsNullOrEmpty(projectContent) && ApplicationSdkRegex().IsMatch(projectContent);

	private static string? FirstMatch(List<string> candidates, string pattern)
	{
		Regex regex = new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		return candidates.Find(regex.IsMatch);
	}

	/// <summary>
	/// Matches test, benchmark, sample, and example projects, which support the deliverable rather than being it.
	/// </summary>
	/// <returns>The compiled regex.</returns>
	[GeneratedRegex(@"(Benchmark|Test|Sample|Example)s?\.csproj$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex SupportingProjectRegex();

	/// <summary>
	/// Matches both ways a project can declare an application SDK: the <c>Sdk</c> attribute on
	/// <c>Project</c>, and a nested <c>Sdk</c> element with a <c>Name</c> attribute.
	/// </summary>
	/// <returns>The compiled regex.</returns>
	[GeneratedRegex(@"Sdk\s*=\s*""ktsu\.Sdk\.(ConsoleApp|App)(/[\d\.]+)?""|<Sdk\s+Name\s*=\s*""ktsu\.Sdk\.(ConsoleApp|App)(/[\d\.]+)?""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex ApplicationSdkRegex();
}
