// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Metadata;

using System.Text;
using KtsuBuild.Abstractions;
using KtsuBuild.Git;
using KtsuBuild.Utilities;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Generates changelog files from Git history.
/// </summary>
/// <param name="gitService">The Git service.</param>
/// <param name="logger">The build logger.</param>
public class ChangelogGenerator(IGitService gitService, IBuildLogger logger)
{
	private const int MaxReleaseNotesLength = 35000; // NuGet limit

	private static readonly string[] BotPatterns = ["[bot]", "github", "ProjectDirector", "SyncFileContents"];
	private static readonly string[] MergePatterns = ["Merge pull request", "Merge branch 'main'", "Updated packages in"];
	private static readonly string[] VersionUpdatePatterns = ["Update VERSION to"];

	/// <summary>
	/// Generates CHANGELOG.md and LATEST_CHANGELOG.md files.
	/// </summary>
	/// <param name="version">The current version.</param>
	/// <param name="commitHash">The current commit hash.</param>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="outputPath">The output directory.</param>
	/// <param name="lineEnding">The line ending to use.</param>
	/// <param name="latestChangelogFileName">The filename for the latest changelog.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public async Task GenerateAsync(
		string version,
		string commitHash,
		string workingDirectory,
		string outputPath,
		string lineEnding,
		string latestChangelogFileName = "LATEST_CHANGELOG.md",
		CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(version);
		Ensure.NotNull(commitHash);
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(outputPath);
		Ensure.NotNull(lineEnding);

		IReadOnlyList<string> tags = await gitService.GetTagsAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
		bool hasTags = tags.Count > 0;
		string previousTag = hasTags ? tags[0] : "v0.0.0";
		string currentTag = $"v{version}";

		logger.WriteInfo($"Generating changelog from {previousTag} to {currentTag} (commit: {commitHash})");

		StringBuilder changelog = new();
		string latestVersionNotes = string.Empty;

		// Generate entry for current version
		string versionNotes = await GetVersionNotesAsync(workingDirectory, tags, previousTag, currentTag, commitHash, lineEnding, cancellationToken).ConfigureAwait(false);

		if (!string.IsNullOrWhiteSpace(versionNotes))
		{
			changelog.Append(versionNotes);
			latestVersionNotes = versionNotes;
		}
		else
		{
			string minimalEntry = $"## {currentTag}{lineEnding}{lineEnding}Initial release or no significant changes since {previousTag}.{lineEnding}{lineEnding}";
			changelog.Append(minimalEntry);
			latestVersionNotes = minimalEntry;
		}

		// Add entries for all previous versions
		if (hasTags)
		{
			for (int i = 0; i < tags.Count; i++)
			{
				string tag = tags[i];
				if (!tag.StartsWith('v'))
				{
					continue;
				}

				string fromTag = i < tags.Count - 1 ? tags[i + 1] : "v0.0.0";
				if (!fromTag.StartsWith('v'))
				{
					fromTag = "v0.0.0";
				}

				string notes = await GetVersionNotesAsync(workingDirectory, tags, fromTag, tag, null, lineEnding, cancellationToken).ConfigureAwait(false);
				changelog.Append(notes);
			}
		}

		// Write full changelog
		string changelogPath = Path.Combine(outputPath, "CHANGELOG.md");
		await LineEndingHelper.WriteFileAsync(changelogPath, changelog.ToString(), lineEnding, cancellationToken).ConfigureAwait(false);

		// Truncate latest version notes if needed
		if (latestVersionNotes.Length > MaxReleaseNotesLength)
		{
			logger.WriteWarning($"Release notes exceed {MaxReleaseNotesLength} characters ({latestVersionNotes.Length}). Truncating to fit NuGet limit.");
			string truncationMessage = $"{lineEnding}{lineEnding}... (truncated due to NuGet length limits)";
			int targetLength = MaxReleaseNotesLength - truncationMessage.Length - 10;
			latestVersionNotes = latestVersionNotes[..targetLength] + truncationMessage;
		}

		// Write latest changelog
		string latestPath = Path.Combine(outputPath, latestChangelogFileName);
		await LineEndingHelper.WriteFileAsync(latestPath, latestVersionNotes, lineEnding, cancellationToken).ConfigureAwait(false);
		logger.WriteInfo($"Latest version changelog saved to: {latestPath}");
	}

	private async Task<string> GetVersionNotesAsync(
		string workingDirectory,
		IReadOnlyList<string> allTags,
		string fromTag,
		string toTag,
		string? toSha,
		string lineEnding,
		CancellationToken cancellationToken)
	{
		// Step 5: Calculate the correct "from" tag based on version type
		string resolvedFromTag = FindSearchTag(allTags, fromTag, toTag);

		// Get commit range
		string? fromSha = resolvedFromTag == "v0.0.0" ? null : await gitService.GetTagCommitHashAsync(workingDirectory, resolvedFromTag, cancellationToken).ConfigureAwait(false);
		string? resolvedToSha = toSha ?? await gitService.GetTagCommitHashAsync(workingDirectory, toTag, cancellationToken).ConfigureAwait(false);

		if (resolvedToSha is null)
		{
			return string.Empty;
		}

		string range = fromSha is null ? resolvedToSha : $"{fromSha}..{resolvedToSha}";

		// Get all commits in range
		IReadOnlyList<CommitInfo> allCommits = await gitService.GetCommitsAsync(workingDirectory, range, cancellationToken).ConfigureAwait(false);
		List<CommitInfo> uniqueCommits = [.. allCommits.DistinctBy(static c => c.Hash)];

		// Determine if this is a prerelease version
		(_, _, _, int toPrerelease) = ParseVersion(toTag);
		bool isPrerelease = toPrerelease > 0;

		// Step 4: Multi-level commit filtering (4-level progressive relaxation)
		List<CommitInfo> filteredCommits = ApplyMultiLevelFiltering(uniqueCommits, isPrerelease);

		// Determine version type for header
		string versionType = DetermineVersionType(resolvedFromTag, toTag);

		// Build changelog entry
		StringBuilder sb = new();
		sb.Append($"## {toTag}");
		if (!string.IsNullOrEmpty(versionType))
		{
			sb.Append($" ({versionType})");
		}

		sb.Append(lineEnding);
		sb.Append(lineEnding);

		if (filteredCommits.Count > 0)
		{
			if (resolvedFromTag != "v0.0.0")
			{
				sb.Append($"Changes since {resolvedFromTag}:{lineEnding}{lineEnding}");
			}

			foreach (CommitInfo commit in filteredCommits.Where(static c => !c.Subject.Contains("[skip ci]", StringComparison.OrdinalIgnoreCase)))
			{
				sb.Append($"- {commit.FormattedEntry}{lineEnding}");
			}

			sb.Append(lineEnding);
		}
		else if (resolvedFromTag == "v0.0.0")
		{
			sb.Append($"Initial release.{lineEnding}{lineEnding}");
		}
		else
		{
			sb.Append($"No significant changes detected since {resolvedFromTag}.{lineEnding}{lineEnding}");
		}

		return sb.ToString();
	}

	/// <summary>
	/// Applies multi-level progressive filtering to find meaningful commits.
	/// Level 1: Exclude bots, merges, and version update commits (for stable releases)
	/// Level 2: Exclude merges only (include bot commits)
	/// Level 3: No filtering (all commits)
	/// Level 4: Specifically include version update commits (for prereleases)
	/// </summary>
	private static List<CommitInfo> ApplyMultiLevelFiltering(List<CommitInfo> commits, bool isPrerelease)
	{
		if (commits.Count == 0)
		{
			return [];
		}

		// Level 1: Standard filtering - exclude bots AND merge/version-update commits
		// For prerelease versions, don't filter "Update VERSION to" commits
		List<CommitInfo> level1 = [.. commits.Where(c => !IsBotCommit(c) && !IsMergeCommit(c) && (isPrerelease || !IsVersionUpdateCommit(c)))];
		if (level1.Count > 0)
		{
			return level1;
		}

		// Level 2: Relaxed - exclude merge commits only (include bots)
		List<CommitInfo> level2 = [.. commits.Where(c => !IsMergeCommit(c) && (isPrerelease || !IsVersionUpdateCommit(c)))];
		if (level2.Count > 0)
		{
			return level2;
		}

		// Level 3: No filtering - all commits in range
		if (commits.Count > 0)
		{
			return commits;
		}

		// Level 4: For prerelease only - specifically include version update commits
		if (isPrerelease)
		{
			return [.. commits.Where(static c => IsVersionUpdateCommit(c))];
		}

		return [];
	}

	private static bool IsBotCommit(CommitInfo commit) =>
		BotPatterns.Any(p => commit.Subject.Contains(p, StringComparison.OrdinalIgnoreCase) ||
							 commit.Author.Contains(p, StringComparison.OrdinalIgnoreCase));

	private static bool IsMergeCommit(CommitInfo commit) =>
		MergePatterns.Any(p => commit.Subject.Contains(p, StringComparison.OrdinalIgnoreCase));

	private static bool IsVersionUpdateCommit(CommitInfo commit) =>
		VersionUpdatePatterns.Any(p => commit.Subject.Contains(p, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Finds the correct "from" tag based on the version type of the "to" tag.
	/// This ensures changelogs show the complete set of changes for a version type,
	/// not just changes since the immediately previous tag.
	/// </summary>
	private static string FindSearchTag(IReadOnlyList<string> allTags, string fromTag, string toTag)
	{
		if (fromTag == "v0.0.0")
		{
			return fromTag;
		}

		(int toMajor, int toMinor, int toPatch, int toPrerelease) = ParseVersion(toTag);
		(int fromMajor, int fromMinor, int fromPatch, int fromPrerelease) = ParseVersion(fromTag);

		// Calculate the search version based on version type
		int searchMajor;
		int searchMinor;
		int searchPatch;
		int searchPrerelease;

		if (toPrerelease != 0)
		{
			// Prerelease: look for previous prerelease in same series
			searchMajor = toMajor;
			searchMinor = toMinor;
			searchPatch = toPatch;
			searchPrerelease = toPrerelease - 1;
		}
		else if (toMajor == fromMajor && toMinor == fromMinor && toPatch == fromPatch && fromPrerelease != 0)
		{
			// Prerelease promoted to stable (same X.Y.Z, prerelease went from N to 0)
			// Show changes since the version before the prerelease series
			searchMajor = toMajor;
			searchMinor = toMinor;
			searchPatch = toPatch - 1;
			searchPrerelease = 0;
		}
		else if (toMajor > fromMajor)
		{
			// Major bump: show changes since previous major
			searchMajor = toMajor - 1;
			searchMinor = 0;
			searchPatch = 0;
			searchPrerelease = 0;
		}
		else if (toMinor > fromMinor)
		{
			// Minor bump: show changes since previous minor
			searchMajor = toMajor;
			searchMinor = toMinor - 1;
			searchPatch = 0;
			searchPrerelease = 0;
		}
		else if (toPatch > fromPatch)
		{
			// Patch bump: show changes since previous patch
			searchMajor = toMajor;
			searchMinor = toMinor;
			searchPatch = toPatch - 1;
			searchPrerelease = 0;
		}
		else
		{
			// No recognizable version change, use the adjacent tag
			return fromTag;
		}

		// Convert search version to 4-component form for matching
		string searchVersion = $"{searchMajor}.{searchMinor}.{searchPatch}.{searchPrerelease}";

		// Look for a matching tag in the available tags
		foreach (string tag in allTags)
		{
			(int tagMajor, int tagMinor, int tagPatch, int tagPrerelease) = ParseVersion(tag);
			string tagVersion = $"{tagMajor}.{tagMinor}.{tagPatch}.{tagPrerelease}";

			if (searchVersion == tagVersion)
			{
				return tag;
			}
		}

		// If no matching tag found, fall back to the original fromTag
		return fromTag;
	}

	private static string DetermineVersionType(string fromTag, string toTag)
	{
		(int Major, int Minor, int Patch, int Prerelease) = ParseVersion(fromTag);
		int fromMajor = Major;
		int fromMinor = Minor;
		int fromPatch = Patch;
		(Major, Minor, Patch, Prerelease) = ParseVersion(toTag);
		int toMajor = Major;
		int toMinor = Minor;
		int toPatch = Patch;
		int toPrerelease = Prerelease;

		if (toPrerelease > 0)
		{
			return "prerelease";
		}

		if (toMajor > fromMajor)
		{
			return "major";
		}

		if (toMinor > fromMinor)
		{
			return "minor";
		}

		if (toPatch > fromPatch)
		{
			return "patch";
		}

		return string.Empty;
	}

	private static (int Major, int Minor, int Patch, int Prerelease) ParseVersion(string tag)
	{
		string version = tag.TrimStart('v');
		string[] mainParts = version.Split('-')[0].Split('.');

		int major = mainParts.Length > 0 && int.TryParse(mainParts[0], out int m) ? m : 0;
		int minor = mainParts.Length > 1 && int.TryParse(mainParts[1], out int n) ? n : 0;
		int patch = mainParts.Length > 2 && int.TryParse(mainParts[2], out int p) ? p : 0;
		int prerelease = 0;

		if (version.Contains('-'))
		{
			string prePart = version.Split('-')[1];
			string[] preParts = prePart.Split('.');
			if (preParts.Length > 1 && int.TryParse(preParts[1], out int pre))
			{
				prerelease = pre;
			}
		}

		return (major, minor, patch, prerelease);
	}
}
