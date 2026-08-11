// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Utilities;

/// <summary>
/// Shared defaults applied to every regular expression in the library.
/// </summary>
internal static class RegexDefaults
{
	/// <summary>
	/// The match timeout applied to every regular expression, so that a pathological
	/// input cannot hang the build through catastrophic backtracking.
	/// </summary>
	internal static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(5);
}
