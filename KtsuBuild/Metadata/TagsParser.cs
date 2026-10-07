// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Metadata;

using System.Text.RegularExpressions;
using KtsuBuild.Abstractions;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Parses TAGS.md files into GitHub-compatible repository topics.
/// </summary>
public static partial class TagsParser
{
	/// <summary>The most topics GitHub allows on one repository.</summary>
	public const int MaxTopics = 20;

	/// <summary>The longest topic GitHub allows.</summary>
	private const int MaxTopicLength = 50;

	/// <summary>
	/// Parses a TAGS.md file into a list of GitHub-compatible topics.
	/// </summary>
	/// <param name="filePath">The path to the TAGS.md file.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A deduplicated list of valid GitHub topics.</returns>
	public static Task<IReadOnlyList<string>> ParseAsync(string filePath, CancellationToken cancellationToken = default) =>
		ParseAsync(filePath, null, cancellationToken);

	/// <summary>
	/// Parses a TAGS.md file into a list of GitHub-compatible topics, warning about any topics dropped
	/// to stay within GitHub's limit.
	/// </summary>
	/// <param name="filePath">The path to the TAGS.md file.</param>
	/// <param name="logger">The logger that reports dropped topics, or <see langword="null"/> to drop them silently.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A deduplicated list of valid GitHub topics.</returns>
	public static async Task<IReadOnlyList<string>> ParseAsync(string filePath, IBuildLogger? logger, CancellationToken cancellationToken = default)
	{
		if (!File.Exists(filePath))
		{
			return [];
		}

		string content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
		return Parse(content, logger);
	}

	/// <summary>
	/// Parses a semicolon-separated tags string into a list of GitHub-compatible topics.
	/// </summary>
	/// <param name="content">The raw tags content.</param>
	/// <returns>A deduplicated list of valid GitHub topics.</returns>
	public static IReadOnlyList<string> Parse(string content) => Parse(content, null);

	/// <summary>
	/// Parses a semicolon-separated tags string into a list of GitHub-compatible topics, warning about
	/// any topics dropped to stay within GitHub's limit.
	/// </summary>
	/// <param name="content">The raw tags content.</param>
	/// <param name="logger">The logger that reports dropped topics, or <see langword="null"/> to drop them silently.</param>
	/// <returns>A deduplicated list of at most <see cref="MaxTopics"/> valid GitHub topics.</returns>
	/// <remarks>
	/// GitHub replaces a repository's topics in one request and rejects the whole set when any entry
	/// breaks its rules, so every rule is enforced here rather than letting one bad tag leave the
	/// topics stale.
	/// </remarks>
	public static IReadOnlyList<string> Parse(string content, IBuildLogger? logger)
	{
		if (string.IsNullOrWhiteSpace(content))
		{
			return [];
		}

		string[] topics = [.. content
			.Split(';')
			.Select(static tag => tag.Trim())
			.Select(static tag => tag.ToLowerInvariant())
			.Select(static tag => tag.Replace(' ', '-'))
			.Select(static tag => InvalidTopicChars().Replace(tag, string.Empty))
			.Select(static tag => tag.Trim('-'))
			.Where(static tag => tag.Length > 0)
			.Where(static tag => tag.Length <= MaxTopicLength)
			.Distinct()];

		if (topics.Length > MaxTopics)
		{
			logger?.WriteWarning($"TAGS.md has {topics.Length} topics but GitHub allows {MaxTopics}, so these are not applied: {string.Join(", ", topics.Skip(MaxTopics))}");
		}

		return [.. topics.Take(MaxTopics)];
	}

#if NET7_0_OR_GREATER
	[GeneratedRegex("[^a-z0-9-]")]
	private static partial Regex InvalidTopicChars();
#else
	private static readonly Regex s_invalidTopicChars = new("[^a-z0-9-]", RegexOptions.Compiled);
	private static Regex InvalidTopicChars() => s_invalidTopicChars;
#endif
}
