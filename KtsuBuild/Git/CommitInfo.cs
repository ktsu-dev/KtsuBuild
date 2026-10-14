// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Git;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Represents information about a Git commit.
/// </summary>
public record CommitInfo
{
	// <id>+<login>@users.noreply.github.com, or the older <login>@users.noreply.github.com. A login is
	// alphanumerics and single hyphens, and cannot start or end with a hyphen.
	private static readonly Regex NoreplyEmail = new(
		@"^(?:\d+\+)?(?<login>[A-Za-z0-9](?:-?[A-Za-z0-9])*)@users\.noreply\.github\.com$",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
		TimeSpan.FromSeconds(1));

	/// <summary>
	/// Gets the short commit hash.
	/// </summary>
	public required string Hash { get; init; }

	/// <summary>
	/// Gets the commit subject (first line of message).
	/// </summary>
	public required string Subject { get; init; }

	/// <summary>
	/// Gets the author name.
	/// </summary>
	public required string Author { get; init; }

	/// <summary>
	/// Gets the author email, or an empty string when it is not known.
	/// </summary>
	public string AuthorEmail { get; init; } = string.Empty;

	/// <summary>
	/// Gets the author's GitHub login, derived from a GitHub noreply email address, or null when the email does
	/// not identify one.
	/// </summary>
	/// <remarks>
	/// The author name is a display name, not a login, so it is never used as one. Bot accounts are not
	/// matched, because their logins end in <c>[bot]</c> and have no profile page at github.com/&lt;login&gt;.
	/// </remarks>
	public string? GitHubLogin
	{
		get
		{
			Match match = NoreplyEmail.Match(AuthorEmail);
			return match.Success ? match.Groups["login"].Value : null;
		}
	}

	/// <summary>
	/// Gets a formatted entry for changelog. The author links to their GitHub profile when their login is known,
	/// and is written as plain text otherwise.
	/// </summary>
	/// <remarks>
	/// Angle brackets in the subject are escaped outside code spans, because GitHub reads <c>&lt;T&gt;</c> or
	/// <c>&lt;Sdk Name&gt;</c> as an HTML tag and deletes it from the rendered changelog and release notes.
	/// </remarks>
	public string FormattedEntry =>
		GitHubLogin is { } login
			? $"{EscapeMarkdown(Subject)} ([@{login}](https://github.com/{login}))"
			: $"{EscapeMarkdown(Subject)} ({Author})";

	/// <summary>
	/// Escapes <c>&lt;</c> and <c>&gt;</c> in <paramref name="text"/> with a backslash, leaving code spans as they
	/// are, since an escape inside one would be shown literally.
	/// </summary>
	/// <param name="text">The Markdown text to escape.</param>
	/// <returns>The text with its angle brackets escaped outside code spans.</returns>
	internal static string EscapeMarkdown(string text)
	{
		StringBuilder escaped = new(text.Length);
		int i = 0;
		while (i < text.Length)
		{
			char c = text[i];
			if (c == '`')
			{
				// A code span opens with a run of backticks and closes at the next run of the same length.
				// A run with no closing match is literal text.
				int run = BacktickRunLength(text, i);
				int close = FindClosingRun(text, i + run, run);
				int end = close < 0 ? i + run : close + run;
				escaped.Append(text, i, end - i);
				i = end;
				continue;
			}

			if (c is '<' or '>')
			{
				escaped.Append('\\');
			}

			escaped.Append(c);
			i++;
		}

		return escaped.ToString();
	}

	private static int BacktickRunLength(string text, int start)
	{
		int end = start;
		while (end < text.Length && text[end] == '`')
		{
			end++;
		}

		return end - start;
	}

	private static int FindClosingRun(string text, int start, int length)
	{
		int i = start;
		while (i < text.Length)
		{
			if (text[i] != '`')
			{
				i++;
				continue;
			}

			int run = BacktickRunLength(text, i);
			if (run == length)
			{
				return i;
			}

			i += run;
		}

		return -1;
	}
}
