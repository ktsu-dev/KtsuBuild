// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Git;

/// <summary>
/// The identity and subject KtsuBuild commits and tags with when the checkout has no identity of
/// its own. <see cref="BotCommitFilter"/> reads the same values, so what KtsuBuild writes is always
/// recognised as automation when the next version is worked out.
/// </summary>
internal static class AutomationIdentity
{
	/// <summary>
	/// The fallback author name. It has a space, so the <c>github-actions</c> author pattern does not match it.
	/// </summary>
	internal const string Name = "Github Actions";

	/// <summary>
	/// The fallback author email.
	/// </summary>
	internal const string Email = "actions@users.noreply.github.com";

	/// <summary>
	/// The subject of the metadata commit the pipeline makes.
	/// </summary>
	internal const string MetadataCommitMessage = "[bot][skip ci] Update Metadata";
}
