// Copyright (c) 2023-2026 ktsu-dev contributors

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

	/// <summary>
	/// Sentinel "from" tag standing in for the beginning of history, used when a
	/// repository has no earlier tag to diff against.
	/// </summary>
	private const string InitialTag = "v0.0.0";

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
		string previousTag = hasTags ? tags[0] : InitialTag;
		string currentTag = $"v{version}";

		logger.WriteInfo($"Generating changelog from {previousTag} to {currentTag} (commit: {commitHash})");

		StringBuilder changelog = new();
		string latestVersionNotes;

		// When the version is already released (a scheduled or manual run on a tagged HEAD), the
		// history below renders its entry from the tag. Writing a separate current-version entry as
		// well put the same heading in CHANGELOG.md twice, the first a "no significant changes" stub
		// diffed from the tag to itself, and that stub replaced the real notes in LATEST_CHANGELOG.md.
		int currentTagIndex = IndexOfTag(tags, currentTag);
		if (currentTagIndex >= 0)
		{
			latestVersionNotes = await GetTagNotesAsync(workingDirectory, tags, currentTagIndex, lineEnding, cancellationToken).ConfigureAwait(false);
		}
		else
		{
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
		}

		// Add entries for all previous versions
		changelog.Append(await GetHistoricalNotesAsync(workingDirectory, tags, lineEnding, cancellationToken).ConfigureAwait(false));

		// Write full changelog
		string changelogPath = Path.Combine(outputPath, "CHANGELOG.md");
		await LineEndingHelper.WriteFileAsync(changelogPath, changelog.ToString(), lineEnding, cancellationToken).ConfigureAwait(false);

		latestVersionNotes = TruncateToNuGetLimit(latestVersionNotes, lineEnding);

		// Write latest changelog
		string latestPath = Path.Combine(outputPath, latestChangelogFileName);
		await LineEndingHelper.WriteFileAsync(latestPath, latestVersionNotes, lineEnding, cancellationToken).ConfigureAwait(false);
		logger.WriteInfo($"Latest version changelog saved to: {latestPath}");
	}

	/// <summary>
	/// Builds the changelog entries for every already-released tag.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="tags">The tags, newest first.</param>
	/// <param name="lineEnding">The line ending to use.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The concatenated entries, empty when there are no version tags.</returns>
	private async Task<string> GetHistoricalNotesAsync(string workingDirectory, IReadOnlyList<string> tags, string lineEnding, CancellationToken cancellationToken)
	{
		StringBuilder notes = new();
		for (int i = 0; i < tags.Count; i++)
		{
			notes.Append(await GetTagNotesAsync(workingDirectory, tags, i, lineEnding, cancellationToken).ConfigureAwait(false));
		}

		return notes.ToString();
	}

	/// <summary>
	/// Builds the changelog entry for one already-released tag, diffed from the tag before it.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="tags">The tags, newest first.</param>
	/// <param name="index">The index of the tag in <paramref name="tags"/>.</param>
	/// <param name="lineEnding">The line ending to use.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The entry, empty when the tag is not a version tag.</returns>
	private async Task<string> GetTagNotesAsync(string workingDirectory, IReadOnlyList<string> tags, int index, string lineEnding, CancellationToken cancellationToken)
	{
		string tag = tags[index];
		if (!tag.StartsWith('v'))
		{
			return string.Empty;
		}

		string fromTag = index < tags.Count - 1 ? tags[index + 1] : InitialTag;
		if (!fromTag.StartsWith('v'))
		{
			fromTag = InitialTag;
		}

		return await GetVersionNotesAsync(workingDirectory, tags, fromTag, tag, null, lineEnding, cancellationToken).ConfigureAwait(false);
	}

	private static int IndexOfTag(IReadOnlyList<string> tags, string tag)
	{
		for (int i = 0; i < tags.Count; i++)
		{
			if (string.Equals(tags[i], tag, StringComparison.Ordinal))
			{
				return i;
			}
		}

		return -1;
	}

	/// <summary>
	/// Truncates release notes that exceed the NuGet release-notes length limit.
	/// </summary>
	/// <param name="notes">The release notes.</param>
	/// <param name="lineEnding">The line ending to use.</param>
	/// <returns>The notes, truncated with a trailing marker when they were too long.</returns>
	private string TruncateToNuGetLimit(string notes, string lineEnding)
	{
		if (notes.Length <= MaxReleaseNotesLength)
		{
			return notes;
		}

		logger.WriteWarning($"Release notes exceed {MaxReleaseNotesLength} characters ({notes.Length}). Truncating to fit NuGet limit.");
		string truncationMessage = $"{lineEnding}{lineEnding}... (truncated due to NuGet length limits)";
		int targetLength = MaxReleaseNotesLength - truncationMessage.Length - 10;
		return notes[..targetLength] + truncationMessage;
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
		string? fromSha = resolvedFromTag == InitialTag ? null : await gitService.GetTagCommitHashAsync(workingDirectory, resolvedFromTag, cancellationToken).ConfigureAwait(false);
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
		bool isPrerelease = ParseVersion(toTag).IsPrerelease;

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

		// Exclude [skip ci] commits before deciding whether there is anything to report, not while
		// emitting. Testing the count on one list and emitting from another produced a "Changes
		// since X:" header above an empty list, because the progressive relaxation in
		// ApplyMultiLevelFiltering re-admits the bot's own [skip ci] metadata commits once no real
		// commit survives level 1. That empty section differed from the previous run's output, so
		// each release was followed by a couple of pointless metadata commits.
		List<CommitInfo> reportableCommits = [.. filteredCommits.Where(static c => !c.Subject.Contains("[skip ci]", StringComparison.OrdinalIgnoreCase))];

		if (reportableCommits.Count > 0)
		{
			if (resolvedFromTag != InitialTag)
			{
				sb.Append($"Changes since {resolvedFromTag}:{lineEnding}{lineEnding}");
			}

			foreach (CommitInfo commit in reportableCommits)
			{
				sb.Append($"- {commit.FormattedEntry}{lineEnding}");
			}

			sb.Append(lineEnding);
		}
		else if (resolvedFromTag == InitialTag)
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
	/// <para>
	/// Relaxing this far can return nothing but bot or [skip ci] commits, so callers must decide
	/// whether the result is worth reporting rather than assuming a non-empty list means there is
	/// something to show.
	/// </para>
	/// </summary>
	private static List<CommitInfo> ApplyMultiLevelFiltering(List<CommitInfo> commits, bool isPrerelease)
	{
		if (commits.Count == 0)
		{
			return [];
		}

		// Level 1: Standard filtering - exclude bots AND merge/version-update commits
		// For prerelease versions, don't filter "Update VERSION to" commits
		List<CommitInfo> level1 = [.. commits.Where(c => !BotCommitFilter.IsBotCommit(c) && !IsMergeCommit(c) && (isPrerelease || !IsVersionUpdateCommit(c)))];
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

		// Level 3: no filtering, every commit in the range. The empty case already returned at the
		// top of this method, so this always returns a non-empty list and is the final level.
		//
		// There was a level 4 below here that re-admitted version update commits for prereleases.
		// It was unreachable, because this return is unconditional, and it was also redundant:
		// levels 1 and 2 already keep version update commits when isPrerelease is set.
		return commits;
	}

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
		if (fromTag == InitialTag)
		{
			return fromTag;
		}

		(int toMajor, int toMinor, int toPatch, bool toIsPrerelease, int toPrerelease) = ParseVersion(toTag);
		(int fromMajor, int fromMinor, int fromPatch, bool fromIsPrerelease, _) = ParseVersion(fromTag);

		// A prerelease promoted to stable keeps the same X.Y.Z while it loses its prerelease
		// suffix, so it is treated the same as a patch bump.
		bool prereleasePromotedToStable = !toIsPrerelease && toMajor == fromMajor && toMinor == fromMinor && toPatch == fromPatch && fromIsPrerelease;

		// Calculate the search version based on version type
		int searchMajor;
		int searchMinor;
		int searchPatch;
		int searchPrerelease;

		if (toIsPrerelease && toPrerelease <= 1)
		{
			// The first prerelease of a series, or one with no number to count back from (rc1, dev),
			// has no earlier prerelease to diff against. Searching for "prerelease 0" would match the
			// stable X.Y.Z tag, which comes after it, and produce an empty range, so diff against the
			// adjacent earlier tag instead.
			return fromTag;
		}
		else if (toIsPrerelease)
		{
			// Prerelease: look for previous prerelease in same series
			searchMajor = toMajor;
			searchMinor = toMinor;
			searchPatch = toPatch;
			searchPrerelease = toPrerelease - 1;
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
		else if (toPatch > fromPatch || prereleasePromotedToStable)
		{
			// Patch bump, or a prerelease promoted to stable (same X.Y.Z, prerelease went
			// from N to 0). Both show changes since the version before this patch.
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

		// A prerelease searches for the previous prerelease, anything else for a stable tag, so a
		// numberless prerelease such as v1.0.0-dev is never taken for v1.0.0.
		(int, int, int, bool, int) searchVersion = (searchMajor, searchMinor, searchPatch, toIsPrerelease, searchPrerelease);

		// A previous prerelease must be in the same series (alpha.2 for alpha.3, not beta.2), and older
		// than toTag. allTags is newest first, so once a later series on the same X.Y.Z exists, its tag
		// with the same number would otherwise be found first and give an empty range.
		string? searchLabel = toIsPrerelease ? GetPrereleaseLabel(toTag) : null;
		int start = toIsPrerelease ? IndexOfTag(allTags, toTag) + 1 : 0;

		// If no matching tag found, fall back to the original fromTag
		return FindTag(allTags, start, searchVersion, searchLabel) ?? fromTag;
	}

	/// <summary>
	/// Finds the first tag from <paramref name="start"/> whose parsed version is
	/// <paramref name="searchVersion"/> and, when <paramref name="searchLabel"/> is set, whose
	/// prerelease label matches it.
	/// </summary>
	private static string? FindTag(IReadOnlyList<string> allTags, int start, (int, int, int, bool, int) searchVersion, string? searchLabel)
	{
		for (int i = start; i < allTags.Count; i++)
		{
			string tag = allTags[i];
			if (ParseVersion(tag) == searchVersion
				&& (searchLabel is null || string.Equals(GetPrereleaseLabel(tag), searchLabel, StringComparison.Ordinal)))
			{
				return tag;
			}
		}

		return null;
	}

	private static string DetermineVersionType(string fromTag, string toTag)
	{
		(int fromMajor, int fromMinor, int fromPatch, _, _) = ParseVersion(fromTag);
		(int toMajor, int toMinor, int toPatch, bool toIsPrerelease, _) = ParseVersion(toTag);

		if (toIsPrerelease)
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

	/// <summary>
	/// Returns a tag's prerelease label as <see cref="VersionCalculator.ParsePrerelease"/> reads it
	/// (<c>alpha</c> for <c>v2.0.0-alpha.3</c>), or <see langword="null"/> for a stable tag.
	/// </summary>
	private static string? GetPrereleaseLabel(string tag)
	{
		int buildIndex = tag.IndexOf('+', StringComparison.Ordinal);
		string version = buildIndex >= 0 ? tag[..buildIndex] : tag;
		int prereleaseIndex = version.IndexOf('-', StringComparison.Ordinal);
		return prereleaseIndex >= 0 ? VersionCalculator.ParsePrerelease(version[(prereleaseIndex + 1)..]).Label : null;
	}

	/// <summary>
	/// Parses a version tag. Any <c>-</c> suffix makes it a prerelease, and the prerelease number is
	/// read by <see cref="VersionCalculator.ParsePrerelease"/>, so both classes agree about every tag:
	/// <c>rc.1</c> and <c>alpha.beta.1</c> are number 1, while <c>rc1</c> and <c>dev</c> are number 0.
	/// </summary>
	private static (int Major, int Minor, int Patch, bool IsPrerelease, int Prerelease) ParseVersion(string tag)
	{
		string version = tag.TrimStart('v');
		int buildIndex = version.IndexOf('+', StringComparison.Ordinal);
		if (buildIndex >= 0)
		{
			version = version[..buildIndex];
		}

		int prereleaseIndex = version.IndexOf('-', StringComparison.Ordinal);
		bool isPrerelease = prereleaseIndex >= 0;
		string[] mainParts = (isPrerelease ? version[..prereleaseIndex] : version).Split('.');

		int major = mainParts.Length > 0 && int.TryParse(mainParts[0], out int m) ? m : 0;
		int minor = mainParts.Length > 1 && int.TryParse(mainParts[1], out int n) ? n : 0;
		int patch = mainParts.Length > 2 && int.TryParse(mainParts[2], out int p) ? p : 0;
		int prerelease = isPrerelease ? VersionCalculator.ParsePrerelease(version[(prereleaseIndex + 1)..]).Number : 0;

		return (major, minor, patch, isPrerelease, prerelease);
	}
}
