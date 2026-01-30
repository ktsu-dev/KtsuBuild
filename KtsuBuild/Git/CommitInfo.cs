// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Git;

/// <summary>
/// Represents information about a Git commit.
/// </summary>
public record CommitInfo
{
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
	/// Gets a formatted entry for changelog.
	/// </summary>
	public string FormattedEntry => $"{Subject} ([@{Author}](https://github.com/{Author}))";
}
