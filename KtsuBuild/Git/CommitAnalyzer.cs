// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Git;

using System.Text.RegularExpressions;
using KtsuBuild.Abstractions;
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

#pragma warning disable SYSLIB1045 // GeneratedRegex not available in netstandard2.0/2.1
	private static readonly Regex SkipCiRegex = new(@"\[skip ci\]|\[ci skip\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
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
		foreach (string message in messages)
		{
			if (message.Contains("[major]", StringComparison.OrdinalIgnoreCase))
			{
				return (VersionType.Major, $"Explicit [major] tag found in commit message: {message}");
			}
		}

		string minorReason = string.Empty;
		string patchReason = string.Empty;
		string preReason = string.Empty;

		foreach (string message in messages)
		{
			if (message.Contains("[minor]", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(minorReason))
			{
				minorReason = $"Explicit [minor] tag found in commit message: {message}";
			}
			else if (message.Contains("[patch]", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(patchReason))
			{
				patchReason = $"Explicit [patch] tag found in commit message: {message}";
			}
			else if (message.Contains("[pre]", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(preReason))
			{
				preReason = $"Explicit [pre] tag found in commit message: {message}";
			}
		}

		if (!string.IsNullOrEmpty(minorReason))
		{
			return (VersionType.Minor, minorReason);
		}

		if (!string.IsNullOrEmpty(patchReason))
		{
			return (VersionType.Patch, patchReason);
		}

		if (!string.IsNullOrEmpty(preReason))
		{
			return (VersionType.Prerelease, preReason);
		}

		// Check for meaningful commits (not bot/PR merges)
		bool hasMeaningfulCommits = messages.Any(static m =>
			!BotPatterns.Any(p => m.Contains(p, StringComparison.OrdinalIgnoreCase)) &&
			!PrPatterns.Any(p => Regex.IsMatch(m, p, RegexOptions.IgnoreCase)));

		if (hasMeaningfulCommits)
		{
			// Check for public API changes
			bool hasApiChanges = await CheckForApiChangesAsync(workingDirectory, range, cancellationToken).ConfigureAwait(false);
			if (hasApiChanges)
			{
				return (VersionType.Minor, "Public API changes detected (additions, removals, or modifications)");
			}

			return (VersionType.Patch, "Found changes warranting at least a patch version");
		}

		return (VersionType.Prerelease, "No significant changes detected");
	}

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

		foreach (string pattern in apiChangePatterns)
		{
			if (Regex.IsMatch(diff, pattern, RegexOptions.Multiline))
			{
				return true;
			}
		}

		return false;
	}
}
