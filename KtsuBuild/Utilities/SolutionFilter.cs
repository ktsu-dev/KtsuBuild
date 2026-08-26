// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Utilities;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// Builds solution filter (<c>.slnf</c>) files, so a test run can cover a subset of a solution in
/// one invocation rather than one invocation per project.
/// </summary>
/// <remarks>
/// A filter is the only way to narrow a workspace-wide <c>dotnet test</c> without giving up the
/// single invocation. Looping over projects instead costs one test host startup each, which was
/// measured slower overall than a single unpinned run.
/// </remarks>
public static class SolutionFilter
{
	private static readonly JsonSerializerOptions FilterJson = new() { WriteIndented = true };

	/// <summary>
	/// Finds the solution file for a workspace, preferring the XML format when both are present.
	/// </summary>
	/// <param name="workspace">The workspace directory.</param>
	/// <returns>The solution path, or null when the workspace has none.</returns>
	public static string? FindSolution(string workspace)
	{
		Ensure.NotNull(workspace);

		// Each format is ordered within itself, then the XML format is preferred as a whole.
		// Sorting the combined list instead would put "Sample.sln" ahead of "Sample.slnx" and
		// silently drop the preference.
		return Directory.EnumerateFiles(workspace, "*.slnx").OrderBy(p => p, StringComparer.Ordinal)
			.Concat(Directory.EnumerateFiles(workspace, "*.sln").OrderBy(p => p, StringComparer.Ordinal))
			.FirstOrDefault();
	}

	/// <summary>
	/// Reads the project paths a solution contains, as they are written in the solution and
	/// relative to it.
	/// </summary>
	/// <param name="solutionPath">Path to a <c>.sln</c> or <c>.slnx</c> file.</param>
	/// <returns>The project paths, using the separator the solution itself uses.</returns>
	public static IReadOnlyList<string> ReadProjects(string solutionPath)
	{
		Ensure.NotNull(solutionPath);

		string text = File.ReadAllText(solutionPath);

		return Path.GetExtension(solutionPath).Equals(".slnx", StringComparison.OrdinalIgnoreCase)
			? ParseSlnx(text)
			: ParseSln(text);
	}

	/// <summary>
	/// Parses the XML solution format, which lists every project as a <c>Project</c> element with a
	/// <c>Path</c> attribute, optionally nested inside solution folders.
	/// </summary>
	/// <param name="text">The solution file contents.</param>
	/// <returns>The project paths.</returns>
	public static IReadOnlyList<string> ParseSlnx(string text)
	{
		Ensure.NotNull(text);

		return [.. XDocument.Parse(text)
			.Descendants("Project")
			.Select(e => (string?)e.Attribute("Path"))
			.Where(p => !string.IsNullOrWhiteSpace(p))
			.Select(p => p!)];
	}

	/// <summary>
	/// Parses the classic solution format, whose project lines carry the path as the second quoted
	/// field. Solution folders share the same line shape, so anything without a project file
	/// extension is dropped.
	/// </summary>
	/// <param name="text">The solution file contents.</param>
	/// <returns>The project paths.</returns>
	public static IReadOnlyList<string> ParseSln(string text)
	{
		Ensure.NotNull(text);

		List<string> projects = [];

		foreach (string line in text.Split('\n'))
		{
			string trimmed = line.TrimStart();

			if (!trimmed.StartsWith("Project(", StringComparison.Ordinal))
			{
				continue;
			}

			// Project("{TypeGuid}") = "Name", "relative\\path.csproj", "{ProjectGuid}".
			// Splitting on the quote puts the path in the sixth field. Solution folders share the
			// same line shape, so the extension is what tells the two apart.
			string[] fields = trimmed.Split('"');

			if (fields.Length > 5 && fields[5].EndsWith("proj", StringComparison.OrdinalIgnoreCase))
			{
				projects.Add(fields[5]);
			}
		}

		return projects;
	}

	/// <summary>
	/// Determines whether a project path matches a glob pattern. Matching is case insensitive and
	/// runs against the forward-slash form of the path, so one pattern works whichever platform
	/// wrote the solution.
	/// </summary>
	/// <param name="projectPath">The project path, in any separator style.</param>
	/// <param name="pattern">A glob supporting <c>*</c> and <c>**</c>.</param>
	/// <returns>True when the pattern matches.</returns>
	public static bool Matches(string projectPath, string pattern)
	{
		Ensure.NotNull(projectPath);
		Ensure.NotNull(pattern);

		string normalized = projectPath.Replace('\\', '/');
		string normalizedPattern = pattern.Replace('\\', '/');

		StringBuilder regex = new("^");

		for (int i = 0; i < normalizedPattern.Length; i++)
		{
			char c = normalizedPattern[i];

			if (c == '*')
			{
				// `**` crosses directory separators; a single `*` does not.
				if (i + 1 < normalizedPattern.Length && normalizedPattern[i + 1] == '*')
				{
					regex.Append(".*");
					i++;
					continue;
				}

				regex.Append("[^/]*");
				continue;
			}

			regex.Append(Regex.Escape(c.ToString(CultureInfo.InvariantCulture)));
		}

		regex.Append('$');

		return Regex.IsMatch(normalized, regex.ToString(), RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));
	}

	/// <summary>
	/// Writes a solution filter covering every project in a solution except those matching the
	/// supplied patterns.
	/// </summary>
	/// <param name="solutionPath">The solution to filter.</param>
	/// <param name="excludePatterns">Globs matched against each project's path within the solution.</param>
	/// <param name="filterPath">Where to write the filter.</param>
	/// <returns>The paths that were excluded, for the caller to report.</returns>
	public static IReadOnlyList<string> Write(string solutionPath, IReadOnlyList<string> excludePatterns, string filterPath)
	{
		Ensure.NotNull(solutionPath);
		Ensure.NotNull(excludePatterns);
		Ensure.NotNull(filterPath);

		IReadOnlyList<string> all = ReadProjects(solutionPath);
		List<string> kept = [];
		List<string> excluded = [];

		foreach (string project in all)
		{
			if (excludePatterns.Any(pattern => Matches(project, pattern)))
			{
				excluded.Add(project);
			}
			else
			{
				kept.Add(project);
			}
		}

		// The filter format expects Windows separators and a solution path relative to the filter,
		// which sits beside the solution.
		var payload = new
		{
			solution = new
			{
				path = Path.GetFileName(solutionPath),
				projects = kept.Select(p => p.Replace('/', '\\')).OrderBy(p => p, StringComparer.Ordinal).ToArray(),
			},
		};

		File.WriteAllText(filterPath, JsonSerializer.Serialize(payload, FilterJson));

		return excluded;
	}
}
