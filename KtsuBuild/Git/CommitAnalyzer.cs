// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

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
	/// Patterns to exclude bot commits.
	/// </summary>
	private static readonly string[] BotPatterns = ["[bot]", "github", "ProjectDirector", "SyncFileContents"];

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

		IReadOnlyList<string> messages = await gitService.GetCommitMessagesAsync(workingDirectory, range, cancellationToken).ConfigureAwait(false);

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
		if (!HasMeaningfulCommits(messages))
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
	/// <param name="messages">The commit messages to scan.</param>
	/// <returns><see langword="true"/> when at least one commit is meaningful.</returns>
	private static bool HasMeaningfulCommits(IReadOnlyList<string> messages) =>
		messages.Any(static m =>
			!BotPatterns.Any(p => m.Contains(p, StringComparison.OrdinalIgnoreCase)) &&
			!PrPatterns.Any(p => Regex.IsMatch(m, p, RegexOptions.IgnoreCase, RegexDefaults.MatchTimeout)));

	private async Task<bool> CheckForApiChangesAsync(string workingDirectory, string range, CancellationToken cancellationToken)
	{
		string diff = await gitService.GetDiffAsync(workingDirectory, range, "*.cs", cancellationToken).ConfigureAwait(false);
		if (string.IsNullOrEmpty(diff))
		{
			return false;
		}

		// Check for public API changes in the diff
		string[] apiChangePatterns =
		[
			@"^\+\s*(public|protected)\s+(class|interface|enum|struct|record)\s+\w+", // Added public types
			@"^\+\s*(public|protected)\s+\w+\s+\w+\s*\(", // Added public methods
			@"^\+\s*(public|protected)\s+\w+(\s+\w+)*\s*\{", // Added public properties
			@"^\-\s*(public|protected)\s+(class|interface|enum|struct|record)\s+\w+", // Removed public types
			@"^\-\s*(public|protected)\s+\w+\s+\w+\s*\(", // Removed public methods
			@"^\-\s*(public|protected)\s+\w+(\s+\w+)*\s*\{", // Removed public properties
			@"^\+\s*public\s+const\s", // Added public constants
			@"^\-\s*public\s+const\s", // Removed public constants
		];

		return apiChangePatterns.Any(pattern => Regex.IsMatch(diff, pattern, RegexOptions.Multiline, RegexDefaults.MatchTimeout));
	}
}
