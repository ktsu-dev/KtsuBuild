// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Git;

#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Recognizes commits made by bots and automation, so version analysis and changelog
/// generation agree on which commits to leave out.
/// </summary>
internal static class BotCommitFilter
{
	/// <summary>
	/// Patterns matched against the commit author only. They name an identity, so matching them
	/// against a subject would catch human commits that merely mention it, such as "Fix GitHubApiClient".
	/// </summary>
	private static readonly string[] AuthorPatterns = ["[bot]", "github-actions"];

	/// <summary>
	/// Patterns matched against the commit subject, for automation that commits under a human identity.
	/// </summary>
	private static readonly string[] SubjectPatterns = ["ProjectDirector", "SyncFileContents"];

	/// <summary>
	/// Values the whole author or subject must equal: KtsuBuild's own fallback identity and its metadata
	/// commit subject. They are matched in full, so a human commit that only mentions them still counts.
	/// </summary>
	private static readonly string[] ExactAuthors = [AutomationIdentity.Name];
	private static readonly string[] ExactSubjects = [AutomationIdentity.MetadataCommitMessage];

	/// <summary>
	/// Determines whether a commit was made by a bot or by automation.
	/// </summary>
	/// <param name="commit">The commit to check.</param>
	/// <returns><see langword="true"/> when the author or the subject identifies automation.</returns>
	internal static bool IsBotCommit(CommitInfo commit)
	{
		Ensure.NotNull(commit);
		string author = commit.Author.Trim();
		string subject = commit.Subject.Trim();
		return AuthorPatterns.Any(p => author.Contains(p, StringComparison.OrdinalIgnoreCase)) ||
			ExactAuthors.Any(a => author.Equals(a, StringComparison.OrdinalIgnoreCase)) ||
			SubjectPatterns.Any(p => subject.Contains(p, StringComparison.OrdinalIgnoreCase)) ||
			ExactSubjects.Any(s => subject.Equals(s, StringComparison.OrdinalIgnoreCase));
	}
}
