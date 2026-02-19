// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Metadata;

using System.Text.RegularExpressions;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Parses TAGS.md files into GitHub-compatible repository topics.
/// </summary>
public static partial class TagsParser
{
	/// <summary>
	/// Parses a TAGS.md file into a list of GitHub-compatible topics.
	/// </summary>
	/// <param name="filePath">The path to the TAGS.md file.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A deduplicated list of valid GitHub topics.</returns>
	public static async Task<IReadOnlyList<string>> ParseAsync(string filePath, CancellationToken cancellationToken = default)
	{
		if (!File.Exists(filePath))
		{
			return [];
		}

		string content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
		return Parse(content);
	}

	/// <summary>
	/// Parses a semicolon-separated tags string into a list of GitHub-compatible topics.
	/// </summary>
	/// <param name="content">The raw tags content.</param>
	/// <returns>A deduplicated list of valid GitHub topics.</returns>
	public static IReadOnlyList<string> Parse(string content)
	{
		if (string.IsNullOrWhiteSpace(content))
		{
			return [];
		}

		return [.. content
			.Split(';')
			.Select(static tag => tag.Trim())
			.Select(static tag => tag.ToLowerInvariant())
			.Select(static tag => tag.Replace(' ', '-'))
			.Select(static tag => InvalidTopicChars().Replace(tag, string.Empty))
			.Where(static tag => tag.Length > 0)
			.Where(static tag => tag.Length <= 50)
			.Distinct()];
	}

#if NET7_0_OR_GREATER
	[GeneratedRegex("[^a-z0-9-]")]
	private static partial Regex InvalidTopicChars();
#else
	private static readonly Regex s_invalidTopicChars = new("[^a-z0-9-]", RegexOptions.Compiled);
	private static Regex InvalidTopicChars() => s_invalidTopicChars;
#endif
}
