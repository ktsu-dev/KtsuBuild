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
/// Calculates semantic versions based on Git history.
/// </summary>
/// <param name="gitService">The Git service.</param>
/// <param name="logger">The build logger.</param>
public class VersionCalculator(IGitService gitService, IBuildLogger logger)
{
	private readonly CommitAnalyzer _commitAnalyzer = new(gitService);

#pragma warning disable SYSLIB1045 // GeneratedRegex not available in netstandard2.0/2.1
	private static readonly Regex PrereleaseRegex = new(@"-(?:alpha|beta|rc|pre).*$", RegexOptions.Compiled);
	private static readonly Regex PrereleaseNumberRegex = new(@"-(?:(alpha|beta|rc|pre))\.(\d+)", RegexOptions.Compiled);
#pragma warning restore SYSLIB1045

	/// <summary>
	/// Gets comprehensive version information from Git history.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="commitHash">The current commit hash.</param>
	/// <param name="initialVersion">The initial version to use if no tags exist.</param>
	/// <param name="forcedVersionType">Optional forced version type (overrides auto-detection).</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The version info.</returns>
	public async Task<VersionInfo> GetVersionInfoAsync(
		string workingDirectory,
		string commitHash,
		string initialVersion = "1.0.0",
		VersionType? forcedVersionType = null,
		CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(commitHash);

		logger.WriteStepHeader("Analyzing Version Information");
		logger.WriteInfo($"Analyzing repository for version information...");
		logger.WriteInfo($"Commit hash: {commitHash}");

		IReadOnlyList<string> tags = await gitService.GetTagsAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
		logger.WriteInfo($"Found {tags.Count} tag(s)");

		bool usingFallbackTag = tags.Count == 0;
		string lastTag = usingFallbackTag ? $"v{initialVersion}-pre.0" : tags[0];

		if (usingFallbackTag)
		{
			logger.WriteInfo($"No tags found. Using fallback: {lastTag}");
		}
		else
		{
			logger.WriteInfo($"Using last tag: {lastTag}");
		}

		// Parse version from tag
		string lastVersion = lastTag.TrimStart('v');
		(int major, int minor, int patch, bool isPrerelease, int prereleaseNum, string prereleaseLabel) = ParseVersion(lastVersion);

		// Get first commit and tag commit
		string firstCommit = await gitService.GetFirstCommitAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
		string? lastTagCommit = usingFallbackTag
			? firstCommit
			: await gitService.GetTagCommitHashAsync(workingDirectory, lastTag, cancellationToken).ConfigureAwait(false);

		lastTagCommit ??= firstCommit;

		// Define commit range
		string commitRange = $"{lastTagCommit}..{commitHash}";
		logger.WriteInfo($"Analyzing commit range: {commitRange}");

		// Determine increment type
		VersionType incrementType;
		string incrementReason;

		if (forcedVersionType.HasValue)
		{
			incrementType = forcedVersionType.Value;
			incrementReason = $"Forced version bump: {incrementType}";
			logger.WriteInfo($"Using forced version type: {incrementType}");
		}
		else
		{
			// Analyze commits to determine increment type
			(incrementType, incrementReason) = await _commitAnalyzer.AnalyzeAsync(workingDirectory, commitRange, cancellationToken).ConfigureAwait(false);
		}

		logger.WriteInfo($"Version increment type: {incrementType}");
		logger.WriteInfo($"Reason: {incrementReason}");

		// Calculate new version
		VersionInfo versionInfo = new()
		{
			LastTag = lastTag,
			LastVersion = lastVersion,
			WasPrerelease = isPrerelease,
			VersionIncrement = incrementType,
			IncrementReason = incrementReason,
			FirstCommit = firstCommit,
			LastCommit = commitHash,
			LastTagCommit = lastTagCommit,
			UsingFallbackTag = usingFallbackTag,
			CommitRange = commitRange,
		};

		if (incrementType == VersionType.Skip)
		{
			// Use the same version, don't increment
			versionInfo.Version = lastVersion;
			versionInfo.Major = major;
			versionInfo.Minor = minor;
			versionInfo.Patch = patch;
			versionInfo.IsPrerelease = isPrerelease;
			versionInfo.PrereleaseNumber = prereleaseNum;
			versionInfo.PrereleaseLabel = prereleaseLabel;
		}
		else
		{
			CalculateNewVersion(versionInfo, major, minor, patch, isPrerelease, prereleaseNum, prereleaseLabel, incrementType);
		}

		logger.WriteInfo($"\nVersion decision:");
		logger.WriteInfo($"Previous version: {lastVersion}");
		logger.WriteInfo($"New version: {versionInfo.Version}");
		logger.WriteInfo($"Reason: {incrementReason}");

		return versionInfo;
	}

	private static (int Major, int Minor, int Patch, bool IsPrerelease, int PrereleaseNum, string PrereleaseLabel) ParseVersion(string version)
	{
		bool isPrerelease = version.Contains('-');
		string cleanVersion = PrereleaseRegex.Replace(version, string.Empty);

		string[] parts = cleanVersion.Split('.');
		int major = parts.Length > 0 && int.TryParse(parts[0], out int m) ? m : 1;
		int minor = parts.Length > 1 && int.TryParse(parts[1], out int n) ? n : 0;
		int patch = parts.Length > 2 && int.TryParse(parts[2], out int p) ? p : 0;

		int prereleaseNum = 0;
		string prereleaseLabel = "pre";

		if (isPrerelease)
		{
			Match match = PrereleaseNumberRegex.Match(version);
			if (match.Success)
			{
				prereleaseNum = int.Parse(match.Groups[2].Value);
				prereleaseLabel = match.Groups[1].Success ? match.Groups[1].Value : "pre";
			}
		}

		return (major, minor, patch, isPrerelease, prereleaseNum, prereleaseLabel);
	}

	private static void CalculateNewVersion(
		VersionInfo info,
		int lastMajor,
		int lastMinor,
		int lastPatch,
		bool wasPrerelease,
		int lastPrereleaseNum,
		string prereleaseLabel,
		VersionType incrementType)
	{
		int newMajor = lastMajor;
		int newMinor = lastMinor;
		int newPatch = lastPatch;
		int newPrereleaseNum = 0;
		bool isPrerelease = false;

		switch (incrementType)
		{
			case VersionType.Major:
				newMajor = lastMajor + 1;
				newMinor = 0;
				newPatch = 0;
				break;

			case VersionType.Minor:
				newMinor = lastMinor + 1;
				newPatch = 0;
				break;

			case VersionType.Patch:
				if (!wasPrerelease)
				{
					newPatch = lastPatch + 1;
				}
				// If was prerelease, just drop the prerelease suffix
				break;

			case VersionType.Prerelease:
				if (wasPrerelease)
				{
					// Bump prerelease number
					newPrereleaseNum = lastPrereleaseNum + 1;
					isPrerelease = true;
				}
				else
				{
					// Start new prerelease series
					newPatch = lastPatch + 1;
					newPrereleaseNum = 1;
					isPrerelease = true;
				}
				break;
		}

		info.Major = newMajor;
		info.Minor = newMinor;
		info.Patch = newPatch;
		info.IsPrerelease = isPrerelease;
		info.PrereleaseNumber = newPrereleaseNum;
		info.PrereleaseLabel = prereleaseLabel;

		info.Version = isPrerelease
			? $"{newMajor}.{newMinor}.{newPatch}-{prereleaseLabel}.{newPrereleaseNum}"
			: $"{newMajor}.{newMinor}.{newPatch}";
	}
}
