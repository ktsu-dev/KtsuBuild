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
	/// The ktsu.Sdk name suffixes that select a target platform rather than a kind of project.
	/// </summary>
	/// <remarks>
	/// These say which platform a project targets, but they also set <c>OutputType</c>, so a project
	/// that declares one and no kind suffix still produces a runnable binary rather than a package.
	/// </remarks>
	private const string PlatformSuffixPattern = "Ios|Windows|Linux|macOS";

	/// <summary>
	/// The platform suffixes of <see cref="PlatformSuffixPattern"/>, for callers that match a
	/// captured suffix against the list rather than running the pattern over a whole project file.
	/// </summary>
	/// <remarks>
	/// Compare with <see cref="StringComparer.OrdinalIgnoreCase"/>, so that casing variants such as
	/// <c>ktsu.Sdk.iOS</c> and <c>ktsu.Sdk.MacOS</c> also match.
	/// </remarks>
	internal static readonly string[] PlatformSuffixes = PlatformSuffixPattern.Split('|');

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
	private const string ExecutableSuffixes = "App|ConsoleApp|" + PlatformSuffixPattern;

	/// <summary>
	/// Matches a reference to an SDK that produces an executable, in either the single-attribute
	/// form (<c>&lt;Project Sdk="ktsu.Sdk.App/1.0.0"&gt;</c>) or the multi-element form
	/// (<c>&lt;Sdk Name="ktsu.Sdk.ConsoleApp" /&gt;</c>) that ktsu.Sdk.* projects actually use.
	/// Compile it with <see cref="System.Text.RegularExpressions.RegexOptions.IgnoreCase"/>, so
	/// that casing variants such as <c>ktsu.Sdk.iOS</c> and <c>ktsu.Sdk.MacOS</c> also match.
	/// </summary>
	internal const string ExecutableSdk = SdkReferenceOpen + ExecutableSuffixes + SdkReferenceClose;

	/// <summary>
	/// Matches a reference to <c>ktsu.Sdk.iOS</c>, in either the single-attribute or the
	/// multi-element form of <see cref="ExecutableSdk"/>. That SDK sets the <c>-ios</c> target
	/// framework itself, so a consuming project often carries no literal target framework and
	/// can only be recognized as iOS by the SDK it references. Compile it with
	/// <see cref="System.Text.RegularExpressions.RegexOptions.IgnoreCase"/>.
	/// </summary>
	internal const string IosSdk = SdkReferenceOpen + "Ios" + SdkReferenceClose;

	/// <summary>
	/// The start of an SDK reference up to the alternation of name suffixes: the
	/// <c>Sdk="…</c> attribute or <c>Sdk Name="…</c> element, through the dot before the suffix.
	/// </summary>
	private const string SdkReferenceOpen = @"Sdk(?:\s+Name)?\s*=\s*""[^""]*\.(?:";

	/// <summary>
	/// The end of an SDK reference after the name suffix: a version separator or the closing quote.
	/// </summary>
	private const string SdkReferenceClose = @")[/""]";

	/// <summary>
	/// Matches a literal <c>&lt;OutputType&gt;Exe&lt;/OutputType&gt;</c>, how a project on plain
	/// <c>ktsu.Sdk</c> says it builds a runnable binary rather than a package.
	/// </summary>
	internal const string OutputTypeExe = @"<OutputType>\s*Exe\s*</OutputType>";

	/// <summary>
	/// Matches a literal <c>&lt;OutputType&gt;WinExe&lt;/OutputType&gt;</c>, the windowed form of
	/// <see cref="OutputTypeExe"/>.
	/// </summary>
	internal const string OutputTypeWinExe = @"<OutputType>\s*WinExe\s*</OutputType>";

	/// <summary>
	/// Matches <c>&lt;IsPackable&gt;false&lt;/IsPackable&gt;</c>, which keeps a project out of the
	/// packages a release publishes.
	/// </summary>
	internal const string NotPackable = @"<IsPackable>\s*false\s*</IsPackable>";
}
