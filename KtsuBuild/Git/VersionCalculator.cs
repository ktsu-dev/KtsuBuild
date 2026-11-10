// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Git;

using System.Globalization;
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
		ParsedVersion parsed = ParseVersion(lastVersion);

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
			WasPrerelease = parsed.IsPrerelease,
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
			versionInfo.Major = parsed.Major;
			versionInfo.Minor = parsed.Minor;
			versionInfo.Patch = parsed.Patch;
			versionInfo.IsPrerelease = parsed.IsPrerelease;
			versionInfo.PrereleaseNumber = parsed.PrereleaseNum;
			versionInfo.PrereleaseLabel = parsed.PrereleaseLabel;
		}
		else
		{
			CalculateNewVersion(versionInfo, parsed, incrementType);
		}

		logger.WriteInfo($"\nVersion decision:");
		logger.WriteInfo($"Previous version: {lastVersion}");
		logger.WriteInfo($"New version: {versionInfo.Version}");
		logger.WriteInfo($"Reason: {incrementReason}");

		return versionInfo;
	}

	private readonly record struct ParsedVersion(int Major, int Minor, int Patch, bool IsPrerelease, int PrereleaseNum, string PrereleaseLabel);

	/// <summary>
	/// Splits a version into its numeric core and its prerelease label and number.
	/// </summary>
	/// <remarks>
	/// The core is the text before the first <c>-</c>, so any prerelease label leaves major, minor and
	/// patch intact. A prerelease whose last dot-separated identifier is a number, such as <c>dev.1</c> or
	/// <c>preview.3</c>, keeps everything before that number as its label. Any other prerelease, such as
	/// <c>rc1</c>, becomes the label as a whole with number 0, so the next prerelease is <c>rc1.1</c>,
	/// which sorts above <c>rc1</c>.
	/// </remarks>
	private static ParsedVersion ParseVersion(string version)
	{
		int buildIndex = version.IndexOf('+', StringComparison.Ordinal);
		if (buildIndex >= 0)
		{
			version = version[..buildIndex];
		}

		int prereleaseIndex = version.IndexOf('-', StringComparison.Ordinal);
		bool isPrerelease = prereleaseIndex >= 0;
		string core = isPrerelease ? version[..prereleaseIndex] : version;
		string prerelease = isPrerelease ? version[(prereleaseIndex + 1)..] : string.Empty;

		string[] parts = core.Split('.');
		int major = parts.Length > 0 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int m) ? m : 1;
		int minor = parts.Length > 1 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : 0;
		int patch = parts.Length > 2 && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int p) ? p : 0;

		int prereleaseNum = 0;
		string prereleaseLabel = "pre";

		if (isPrerelease && prerelease.Length > 0)
		{
			int lastDot = prerelease.LastIndexOf('.');
			string lastIdentifier = lastDot >= 0 ? prerelease[(lastDot + 1)..] : prerelease;
			bool endsInNumber = int.TryParse(lastIdentifier, NumberStyles.None, CultureInfo.InvariantCulture, out int number);

			if (endsInNumber && lastDot > 0)
			{
				// dev.1, preview.3, alpha.beta.2
				prereleaseLabel = prerelease[..lastDot];
				prereleaseNum = number;
			}
			else if (!endsInNumber)
			{
				// rc1, dev, rc.1.x: keep the whole prerelease as the label, so label.1 sorts above it
				prereleaseLabel = prerelease;
			}

			// A purely numeric prerelease (1.2.3-4) keeps the "pre" label: pre.1 sorts above any number.
		}

		return new ParsedVersion(major, minor, patch, isPrerelease, prereleaseNum, prereleaseLabel);
	}

	private static void CalculateNewVersion(
		VersionInfo info,
		ParsedVersion lastVersion,
		VersionType incrementType)
	{
		int newMajor = lastVersion.Major;
		int newMinor = lastVersion.Minor;
		int newPatch = lastVersion.Patch;
		int newPrereleaseNum = 0;
		bool isPrerelease = false;

		switch (incrementType)
		{
			case VersionType.Major:
				// A prerelease of X.0.0 releases as X.0.0, as semver inc does
				if (!(lastVersion.IsPrerelease && lastVersion.Minor == 0 && lastVersion.Patch == 0))
				{
					newMajor = lastVersion.Major + 1;
				}
				newMinor = 0;
				newPatch = 0;
				break;

			case VersionType.Minor:
				// A prerelease of X.Y.0 releases as X.Y.0, as semver inc does
				if (!(lastVersion.IsPrerelease && lastVersion.Patch == 0))
				{
					newMinor = lastVersion.Minor + 1;
				}
				newPatch = 0;
				break;

			case VersionType.Patch:
				if (!lastVersion.IsPrerelease)
				{
					newPatch = lastVersion.Patch + 1;
				}
				// If was prerelease, just drop the prerelease suffix
				break;

			case VersionType.Prerelease:
				if (lastVersion.IsPrerelease)
				{
					// Bump prerelease number
					newPrereleaseNum = lastVersion.PrereleaseNum + 1;
					isPrerelease = true;
				}
				else
				{
					// Start new prerelease series
					newPatch = lastVersion.Patch + 1;
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
		info.PrereleaseLabel = lastVersion.PrereleaseLabel;

		info.Version = isPrerelease
			? $"{newMajor}.{newMinor}.{newPatch}-{lastVersion.PrereleaseLabel}.{newPrereleaseNum}"
			: $"{newMajor}.{newMinor}.{newPatch}";
	}
}
