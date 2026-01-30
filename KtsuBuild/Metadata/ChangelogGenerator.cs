// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Metadata;

using System.Text;
using KtsuBuild.Abstractions;
using KtsuBuild.Git;
using KtsuBuild.Utilities;
using static Polyfill;

/// <summary>
/// Generates changelog files from Git history.
/// </summary>
/// <param name="gitService">The Git service.</param>
/// <param name="logger">The build logger.</param>
public class ChangelogGenerator(IGitService gitService, IBuildLogger logger)
{
	private const int MaxReleaseNotesLength = 35000; // NuGet limit

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

		var tags = await gitService.GetTagsAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
		bool hasTags = tags.Count > 0;
		string previousTag = hasTags ? tags[0] : "v0.0.0";
		string currentTag = $"v{version}";

		logger.WriteInfo($"Generating changelog from {previousTag} to {currentTag} (commit: {commitHash})");

		var changelog = new StringBuilder();
		string latestVersionNotes = string.Empty;

		// Generate entry for current version
		string versionNotes = await GetVersionNotesAsync(workingDirectory, previousTag, currentTag, commitHash, lineEnding, cancellationToken).ConfigureAwait(false);

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

				string notes = await GetVersionNotesAsync(workingDirectory, fromTag, tag, null, lineEnding, cancellationToken).ConfigureAwait(false);
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
		string fromTag,
		string toTag,
		string? toSha,
		string lineEnding,
		CancellationToken cancellationToken)
	{
		// Get commit range
		string? fromSha = fromTag == "v0.0.0" ? null : await gitService.GetTagCommitHashAsync(workingDirectory, fromTag, cancellationToken).ConfigureAwait(false);
		string? resolvedToSha = toSha ?? await gitService.GetTagCommitHashAsync(workingDirectory, toTag, cancellationToken).ConfigureAwait(false);

		if (resolvedToSha is null)
		{
			return string.Empty;
		}

		string range = fromSha is null ? resolvedToSha : $"{fromSha}..{resolvedToSha}";

		// Get commits
		var commits = await gitService.GetCommitsAsync(workingDirectory, range, cancellationToken).ConfigureAwait(false);

		// Filter out bot and PR merge commits
		var filteredCommits = commits
			.Where(c => !IsBotOrMergeCommit(c.Subject))
			.DistinctBy(c => c.Hash)
			.ToList();

		// Build changelog entry
		var sb = new StringBuilder();
		string versionType = DetermineVersionType(fromTag, toTag);

		sb.Append($"## {toTag}");
		if (!string.IsNullOrEmpty(versionType))
		{
			sb.Append($" ({versionType})");
		}
		sb.Append(lineEnding);
		sb.Append(lineEnding);

		if (filteredCommits.Count > 0)
		{
			if (fromTag != "v0.0.0")
			{
				sb.Append($"Changes since {fromTag}:{lineEnding}{lineEnding}");
			}

			foreach (var commit in filteredCommits.Where(c => !c.Subject.Contains("[skip ci]", StringComparison.OrdinalIgnoreCase)))
			{
				sb.Append($"- {commit.FormattedEntry}{lineEnding}");
			}

			sb.Append(lineEnding);
		}
		else if (fromTag == "v0.0.0")
		{
			sb.Append($"Initial release.{lineEnding}{lineEnding}");
		}
		else
		{
			sb.Append($"No significant changes detected since {fromTag}.{lineEnding}{lineEnding}");
		}

		return sb.ToString();
	}

	private static bool IsBotOrMergeCommit(string subject)
	{
		string[] botPatterns = ["[bot]", "github", "ProjectDirector", "SyncFileContents"];
		string[] mergePatterns = ["Merge pull request", "Merge branch 'main'", "Updated packages in", "Update VERSION to"];

		return botPatterns.Any(p => subject.Contains(p, StringComparison.OrdinalIgnoreCase)) ||
			   mergePatterns.Any(p => subject.Contains(p, StringComparison.OrdinalIgnoreCase));
	}

	private static string DetermineVersionType(string fromTag, string toTag)
	{
		var fromVersion = ParseVersion(fromTag);
		var toVersion = ParseVersion(toTag);

		if (toVersion.Prerelease > 0)
		{
			return "prerelease";
		}

		if (toVersion.Major > fromVersion.Major)
		{
			return "major";
		}

		if (toVersion.Minor > fromVersion.Minor)
		{
			return "minor";
		}

		if (toVersion.Patch > fromVersion.Patch)
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
