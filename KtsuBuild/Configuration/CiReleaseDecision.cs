// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Configuration;

/// <summary>
/// Decides what the CI pipeline does about the release, separately from doing it.
/// </summary>
/// <remarks>
/// Two questions look the same and are not. "Should this run publish?" may be answered no because
/// the caller asked for the release to happen in a later job, while "should a release happen at
/// all?" is still yes. Consuming workflows gate their publishing jobs on the second answer, so
/// folding the first into it would leave those jobs skipped and silent.
/// <para>
/// <see cref="ShouldReleaseOutput"/> therefore takes no suppression parameter. That is deliberate:
/// the invariant is carried by the signature rather than by a comment, so a later edit cannot
/// accidentally feed the flag into the reported answer without changing the shape of the call.
/// </para>
/// </remarks>
public static class CiReleaseDecision
{
	/// <summary>
	/// Determines whether this run performs the release itself.
	/// </summary>
	/// <param name="shouldRelease">Whether the build configuration permits a release, meaning an official repo, on main, untagged.</param>
	/// <param name="releaseSkipped">Whether the version increment suppressed the release, which is how <c>[skip ci]</c> and a run with no meaningful changes behave.</param>
	/// <param name="suppressedByFlag">Whether the caller asked this run not to release, leaving it to a later step or job.</param>
	/// <returns><see langword="true"/> only when a release is warranted and nothing suppresses it.</returns>
	public static bool ShouldExecuteRelease(bool shouldRelease, bool releaseSkipped, bool suppressedByFlag) =>
		shouldRelease && !releaseSkipped && !suppressedByFlag;

	/// <summary>
	/// Determines what the <c>should_release</c> step output reports to later jobs.
	/// </summary>
	/// <param name="shouldRelease">Whether the build configuration permits a release, meaning an official repo, on main, untagged.</param>
	/// <param name="releaseSkipped">Whether the version increment suppressed the release.</param>
	/// <returns><c>"true"</c> when a release is warranted, otherwise <c>"false"</c>, as the literal text GitHub Actions compares against.</returns>
	public static string ShouldReleaseOutput(bool shouldRelease, bool releaseSkipped) =>
		shouldRelease && !releaseSkipped ? "true" : "false";
}
