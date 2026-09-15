// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Utilities;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Recognises the projects that support a repository's deliverables — tests, benchmarks, samples,
/// examples, and demos — rather than being one.
/// </summary>
/// <remarks>
/// The rule lives here rather than beside either caller because two unrelated parts of the build
/// need the same answer: the profile README must not claim a repository ships its demo, and the
/// release must not attach RID zips for it. A second copy of the patterns would let the two drift,
/// and the profile page and the release it links to would disagree about what the repository ships.
/// </remarks>
public static partial class SupportingProjects
{
	// Project files can come from a remote repository, so matching is bounded rather than left to
	// run for as long as a pathological input takes.
	private const int MatchTimeoutMilliseconds = 2000;

	// An explicit array leaves no doubt which Split overload runs. Passing two chars partially
	// matches Split(char, int, StringSplitOptions), where the second char would read as a count.
	private static readonly char[] DirectorySeparators = ['/', '\\'];

	private static readonly string[] SupportingDirectories =
	[
		"test", "tests", "example", "examples", "sample", "samples",
		"benchmark", "benchmarks", "demo", "demos",
	];

	/// <summary>
	/// Determines whether a project supports the deliverable rather than being it.
	/// </summary>
	/// <param name="relativePath">The project path, relative to the repository root.</param>
	/// <returns><see langword="true"/> for tests, benchmarks, samples, examples, and demos.</returns>
	/// <remarks>
	/// The path has to be relative to the repository root. An absolute one drags in the directories
	/// above the repository, which say nothing about it — a workspace checked out under
	/// <c>samples/</c> would report every project it contains as supporting. Callers holding an
	/// absolute path should use the <see cref="IsSupporting(string, string)"/> overload.
	/// <para>
	/// Both the file name and the directories above it are checked. ImGuiApp keeps its demo
	/// applications under <c>examples/</c> with names that say nothing about being demos, so
	/// filtering on the file name alone would miss them.
	/// </para>
	/// </remarks>
	public static bool IsSupporting(string relativePath)
	{
		Ensure.NotNull(relativePath);

		if (SupportingProjectRegex().IsMatch(relativePath))
		{
			return true;
		}

		string[] segments = relativePath.Split(DirectorySeparators);
		for (int i = 0; i < segments.Length - 1; i++)
		{
			if (SupportingDirectories.Contains(segments[i], StringComparer.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether a project discovered under a workspace supports the deliverable rather
	/// than being it.
	/// </summary>
	/// <param name="workspace">The repository root the project was discovered under.</param>
	/// <param name="projectPath">The project path, absolute or relative to <paramref name="workspace"/>.</param>
	/// <returns><see langword="true"/> for tests, benchmarks, samples, examples, and demos.</returns>
	/// <remarks>
	/// Project discovery returns absolute paths, so the path is made relative to the workspace
	/// before it is judged. Judging it whole would let the directories above the checkout decide:
	/// a clone at <c>~/samples/ImGuiApp</c> would publish nothing at all.
	/// </remarks>
	public static bool IsSupporting(string workspace, string projectPath)
	{
		Ensure.NotNull(workspace);
		Ensure.NotNull(projectPath);

		return IsSupporting(Path.GetRelativePath(workspace, projectPath));
	}

	/// <summary>
	/// Matches test, benchmark, sample, example, and demo projects by file name.
	/// </summary>
	/// <returns>The compiled regex.</returns>
	/// <remarks>
	/// Demo belongs here for the same reason sample and example do. They name the same thing, and a
	/// demo left in the list reports its SDK as something the repository ships, so Keybinding would
	/// claim a command line program it does not have.
	/// </remarks>
	[GeneratedRegex(
		@"(Benchmark|Test|Sample|Example|Demo)s?\.csproj$",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
		MatchTimeoutMilliseconds)]
	private static partial Regex SupportingProjectRegex();
}
