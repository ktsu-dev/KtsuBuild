// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Git;

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
	public string FormattedEntry =>
		GitHubLogin is { } login
			? $"{Subject} ([@{login}](https://github.com/{login}))"
			: $"{Subject} ({Author})";
}
