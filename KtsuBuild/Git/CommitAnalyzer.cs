// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Git;

using System.Text.RegularExpressions;
using KtsuBuild.Abstractions;
using KtsuBuild.Utilities;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Analyzes commits to determine version bump type.
/// </summary>
/// <param name="gitService">The Git service.</param>
public class CommitAnalyzer(IGitService gitService)
{
	/// <summary>
	/// Patterns to exclude PR merge commits.
	/// </summary>
	private static readonly string[] PrPatterns = ["Merge pull request", "Merge branch 'main'", "Updated packages in", "Update.*package version"];

	/// <summary>
	/// Explicit version tags, in descending order of precedence.
	/// </summary>
	private static readonly (string Tag, VersionType Type)[] ExplicitVersionTags =
	[
		("[major]", VersionType.Major),
		("[minor]", VersionType.Minor),
		("[patch]", VersionType.Patch),
		("[pre]", VersionType.Prerelease),
	];

#pragma warning disable SYSLIB1045 // GeneratedRegex not available in netstandard2.0/2.1
	private static readonly Regex SkipCiRegex = new(@"\[skip ci\]|\[ci skip\]", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexDefaults.MatchTimeout);
#pragma warning restore SYSLIB1045

	/// <summary>
	/// Analyzes the commit range and determines the version type.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="range">The commit range to analyze.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A tuple of version type and reason.</returns>
	public async Task<(VersionType Type, string Reason)> AnalyzeAsync(string workingDirectory, string range, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(range);

		IReadOnlyList<CommitInfo> commits = await gitService.GetCommitsAsync(workingDirectory, range, cancellationToken).ConfigureAwait(false);
		IReadOnlyList<string> messages = [.. commits.Select(static c => c.Subject)];

		if (messages.Count == 0)
		{
			return (VersionType.Skip, "No commits found in the specified range");
		}

		// Check if all commits are skip-ci commits
		if (messages.All(SkipCiRegex.IsMatch))
		{
			return (VersionType.Skip, "All commits contain [skip ci] tag, skipping release");
		}

		// Check for explicit version markers
		if (FindExplicitVersionTag(messages) is { } explicitVersion)
		{
			return explicitVersion;
		}

		// Check for meaningful commits (not bot/PR merges)
		if (!HasMeaningfulCommits(commits))
		{
			return (VersionType.Prerelease, "No significant changes detected");
		}

		// Check for public API changes
		bool hasApiChanges = await CheckForApiChangesAsync(workingDirectory, range, cancellationToken).ConfigureAwait(false);

		return hasApiChanges
			? (VersionType.Minor, "Public API changes detected (additions, removals, or modifications)")
			: (VersionType.Patch, "Found changes warranting at least a patch version");
	}

	/// <summary>
	/// Finds the highest-precedence explicit version tag present in the commit messages.
	/// </summary>
	/// <param name="messages">The commit messages to scan.</param>
	/// <returns>The version type and reason, or <see langword="null"/> when no tag is present.</returns>
	private static (VersionType Type, string Reason)? FindExplicitVersionTag(IReadOnlyList<string> messages)
	{
		foreach ((string tag, VersionType type) in ExplicitVersionTags)
		{
			string? tagged = messages.FirstOrDefault(m => m.Contains(tag, StringComparison.OrdinalIgnoreCase));
			if (tagged is not null)
			{
				return (type, $"Explicit {tag} tag found in commit message: {tagged}");
			}
		}

		return null;
	}

	/// <summary>
	/// Determines whether any commit is something other than a bot commit or a PR merge.
	/// </summary>
	/// <param name="commits">The commits to scan.</param>
	/// <returns><see langword="true"/> when at least one commit is meaningful.</returns>
	private static bool HasMeaningfulCommits(IReadOnlyList<CommitInfo> commits) =>
		commits.Any(static c =>
			!BotCommitFilter.IsBotCommit(c) &&
			!PrPatterns.Any(p => Regex.IsMatch(c.Subject, p, RegexOptions.IgnoreCase, RegexDefaults.MatchTimeout)));

	private async Task<bool> CheckForApiChangesAsync(string workingDirectory, string range, CancellationToken cancellationToken)
	{
		string diff = await gitService.GetDiffAsync(workingDirectory, range, "*.cs", cancellationToken).ConfigureAwait(false);
		if (string.IsNullOrEmpty(diff))
		{
			return false;
		}

		string productionDiff = WithoutTestFiles(diff);
		return ApiChangePatterns.Any(pattern => pattern.IsMatch(productionDiff));
	}

	/// <summary>
	/// Modifiers that may sit between the access modifier and the type or member they qualify.
	/// </summary>
	private const string ModifierChain = @"(\s+(static|async|virtual|override|abstract|sealed|readonly|partial|new|required|extern|unsafe|volatile|ref|file|internal|event|const))*";

	/// <summary>
	/// Lines in a diff that add or remove a public or protected type or member. A member's return or
	/// field type may be generic, nullable, an array or a tuple, and a method may be generic.
	/// </summary>
#pragma warning disable SYSLIB1045 // GeneratedRegex not available in netstandard2.0/2.1
	private static readonly Regex[] ApiChangePatterns =
	[
		// Types, including records with a primary constructor and delegates
		new($@"^[+-][ \t]*(public|protected){ModifierChain}[ \t]+(class|interface|enum|struct|record|delegate)\b", RegexOptions.Multiline, RegexDefaults.MatchTimeout),

		// Methods, properties, fields, constants and events: a type, a name, then (, <, {, =, => or ;
		new($@"^[+-][ \t]*(public|protected){ModifierChain}[ \t]+[\w.<>\[\]?,() ]+?[ \t]+@?\w+[ \t]*(\(|<|\{{|=|;)", RegexOptions.Multiline, RegexDefaults.MatchTimeout),

		// Constructors: an access modifier followed directly by the type name and its parameters
		new(@"^[+-][ \t]*(public|protected)[ \t]+\w+[ \t]*\(", RegexOptions.Multiline, RegexDefaults.MatchTimeout),
	];

	private static readonly Regex DiffFileHeader = new(@"^diff --git a/(?<path>\S+) b/", RegexOptions.Multiline, RegexDefaults.MatchTimeout);
#pragma warning restore SYSLIB1045

	/// <summary>
	/// Drops the sections of a <c>git diff</c> that belong to test code, so a test class, whose methods
	/// MSTest requires to be public, is not mistaken for public API. A diff without file headers is
	/// returned as it is.
	/// </summary>
	/// <param name="diff">The output of <c>git diff</c>.</param>
	/// <returns>The diff without the sections for files under a test project or test folder.</returns>
	internal static string WithoutTestFiles(string diff)
	{
		MatchCollection headers = DiffFileHeader.Matches(diff);
		if (headers.Count == 0)
		{
			return diff;
		}

		System.Text.StringBuilder kept = new(diff[..headers[0].Index]);
		for (int i = 0; i < headers.Count; i++)
		{
			int sectionEnd = i + 1 < headers.Count ? headers[i + 1].Index : diff.Length;
			if (!IsTestPath(headers[i].Groups["path"].Value))
			{
				kept.Append(diff, headers[i].Index, sectionEnd - headers[i].Index);
			}
		}

		return kept.ToString();
	}

	/// <summary>
	/// Whether a repository path lies in test code: under a project named <c>*.Test</c>, <c>*.Tests</c>,
	/// <c>*.UnitTests</c> or <c>*.IntegrationTests</c>, or under a <c>test</c> or <c>tests</c> folder.
	/// </summary>
	/// <param name="path">The path as git reports it, with forward slashes.</param>
	/// <returns><see langword="true"/> when a directory in the path names test code.</returns>
	internal static bool IsTestPath(string path)
	{
		string[] directories = path.Split('/');
		return directories.Take(directories.Length - 1).Any(static d =>
			d.Equals("test", StringComparison.OrdinalIgnoreCase) ||
			d.Equals("tests", StringComparison.OrdinalIgnoreCase) ||
			d.EndsWith(".Test", StringComparison.OrdinalIgnoreCase) ||
			d.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
			d.EndsWith(".UnitTests", StringComparison.OrdinalIgnoreCase) ||
			d.EndsWith(".IntegrationTests", StringComparison.OrdinalIgnoreCase));
	}
}
