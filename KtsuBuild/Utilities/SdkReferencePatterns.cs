// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Utilities;

/// <summary>
/// Regular-expression patterns that recognize ktsu.Sdk references inside a project file.
/// </summary>
/// <remarks>
/// The same classification is made in two places — <c>DotNet.DotNetService</c> (RID zip
/// publishing and iOS head detection) and <c>Winget.ProjectDetector</c> (library-only
/// classification for manifest generation) — and the two copies have drifted apart before.
/// Both now compile the patterns defined here.
/// </remarks>
internal static class SdkReferencePatterns
{
	/// <summary>
	/// The ktsu.Sdk name suffixes whose projects produce a runnable binary.
	/// </summary>
	/// <remarks>
	/// These SDKs set <c>OutputType</c> themselves, so a consuming project carries no literal
	/// <c>&lt;OutputType&gt;</c> element and can only be recognized by the SDK it references.
	/// <para>
	/// <c>ktsu.Sdk.Tool</c> is deliberately absent: tool packages are framework-dependent and
	/// RID-agnostic, so publishing RID zips for them would attach a set of archives that no
	/// runtime identifier actually selects.
	/// </para>
	/// </remarks>
	private const string ExecutableSuffixes = "App|ConsoleApp|Ios|Windows|Linux|macOS";

	/// <summary>
	/// Matches a reference to an SDK that produces an executable, in either the single-attribute
	/// form (<c>&lt;Project Sdk="ktsu.Sdk.App/1.0.0"&gt;</c>) or the multi-element form
	/// (<c>&lt;Sdk Name="ktsu.Sdk.ConsoleApp" /&gt;</c>) that ktsu.Sdk.* projects actually use.
	/// Compile it with <see cref="System.Text.RegularExpressions.RegexOptions.IgnoreCase"/>, so
	/// that casing variants such as <c>ktsu.Sdk.iOS</c> and <c>ktsu.Sdk.MacOS</c> also match.
	/// </summary>
	internal const string ExecutableSdk =
		@"Sdk(?:\s+Name)?\s*=\s*""[^""]*\.(?:" + ExecutableSuffixes + @")[/""]";
}
