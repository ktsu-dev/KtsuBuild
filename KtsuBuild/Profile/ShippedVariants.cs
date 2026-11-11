// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Profile;

using System.Text.RegularExpressions;
using KtsuBuild.Utilities;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// What a repository ships, derived from the SDK each of its projects declares.
/// </summary>
public enum ShippedVariant
{
	/// <summary>A NuGet package other projects reference. Declared by plain <c>ktsu.Sdk</c>.</summary>
	Library,

	/// <summary>A command line program. Declared by <c>ktsu.Sdk.ConsoleApp</c>.</summary>
	ConsoleApp,

	/// <summary>
	/// An application. Declared by <c>ktsu.Sdk.App</c>, a platform SDK on its own, or a literal
	/// <c>OutputType</c> of <c>Exe</c> or <c>WinExe</c> on plain <c>ktsu.Sdk</c>.
	/// </summary>
	App,

	/// <summary>A .NET tool installed with <c>dotnet tool install</c>. Declared by <c>ktsu.Sdk.Tool</c>.</summary>
	Tool,
}

/// <summary>
/// Works out what a repository ships by reading the SDK declarations in its project files.
/// </summary>
/// <remarks>
/// The project files are the source of truth. A repository's NuGet package name does not always
/// follow its repository name, so looking the package up by repository name misses libraries that
/// ship under a different name.
/// </remarks>
public static partial class ShippedVariants
{
	// Project files come from a remote repository, so matching is bounded rather than left to run for
	// as long as a pathological input takes.
	private const int MatchTimeoutMilliseconds = 2000;

	/// <summary>
	/// Determines whether a project is part of what the repository ships.
	/// </summary>
	/// <param name="path">The project path, relative to the repository root.</param>
	/// <returns><see langword="false"/> for tests, benchmarks, samples, examples, and demos, which
	/// support the deliverable rather than being it.</returns>
	/// <remarks>
	/// The rule itself lives in <see cref="SupportingProjects"/>, because the release path needs the
	/// same answer: a project this reports as not shipped must not have RID zips attached to the
	/// release either.
	/// </remarks>
	public static bool IsShippingProject(string path) => !SupportingProjects.IsSupporting(path);

	/// <summary>
	/// Reads what a single project ships from its SDK declarations.
	/// </summary>
	/// <param name="projectContent">The contents of the project file.</param>
	/// <returns>The variants the project declares. Empty when the project does not use the ktsu SDK at all.</returns>
	/// <remarks>
	/// A project that declares plain <c>ktsu.Sdk</c> and nothing else is a library, because the SDK
	/// packs library projects by default. One that also sets a literal <c>OutputType</c> of
	/// <c>Exe</c> or <c>WinExe</c> is an app, and one with <c>IsPackable</c> false is not a library,
	/// because the release publishes neither as a package. Platform SDKs such as <c>ktsu.Sdk.Windows</c> say which
	/// platform a project targets rather than what kind of thing it is, so a kind suffix alongside
	/// one wins. On their own they still set <c>OutputType</c>, which is why
	/// <c>DotNetService.IsExecutableProject</c> treats a bare <c>ktsu.Sdk.Windows</c> as an
	/// executable and attaches RID zips to the release; such a project ships an application, not a
	/// package.
	/// </remarks>
	public static IReadOnlyList<ShippedVariant> FromProject(string? projectContent)
	{
		if (string.IsNullOrEmpty(projectContent))
		{
			return [];
		}

		HashSet<ShippedVariant> variants = [];
		bool declaresSdk = false;
		bool declaresPlatform = false;

		foreach (Match match in SdkDeclarationRegex().Matches(projectContent))
		{
			declaresSdk = true;
			string variant = match.Groups["variant"].Value;

			if (variant.Equals("ConsoleApp", StringComparison.OrdinalIgnoreCase))
			{
				variants.Add(ShippedVariant.ConsoleApp);
			}
			else if (variant.Equals("App", StringComparison.OrdinalIgnoreCase))
			{
				variants.Add(ShippedVariant.App);
			}
			else if (variant.Equals("Tool", StringComparison.OrdinalIgnoreCase))
			{
				variants.Add(ShippedVariant.Tool);
			}
			else if (SdkReferencePatterns.PlatformSuffixes.Contains(variant, StringComparer.OrdinalIgnoreCase))
			{
				declaresPlatform = true;
			}
		}

		if (variants.Count == 0 && declaresPlatform)
		{
			// The platform SDK is the only thing saying what the project produces, and what it
			// produces is a binary. A kind suffix, when there is one, has already said it better.
			variants.Add(ShippedVariant.App);
		}
		else if (declaresSdk && variants.Count == 0)
		{
			if (OutputTypeExeRegex().IsMatch(projectContent) || OutputTypeWinExeRegex().IsMatch(projectContent))
			{
				// The release publishes RID zips for exactly these projects, through
				// DotNetService.IsExecutableProject, so the row says what the release attaches.
				// A literal OutputType does not say whether the program is windowed or a command
				// line one (Exe is also what a desktop app on Linux or macOS declares), so it is
				// an app; a command line program says so with ktsu.Sdk.ConsoleApp.
				variants.Add(ShippedVariant.App);
			}
			else if (!NotPackableRegex().IsMatch(projectContent))
			{
				variants.Add(ShippedVariant.Library);
			}
		}

		return Order(variants);
	}

	/// <summary>
	/// Combines the variants found across a repository's projects.
	/// </summary>
	/// <param name="perProject">The variants each project declares.</param>
	/// <returns>The combined variants in a stable order.</returns>
	public static IReadOnlyList<ShippedVariant> Combine(IEnumerable<IReadOnlyList<ShippedVariant>> perProject)
	{
		Ensure.NotNull(perProject);

		HashSet<ShippedVariant> combined = [];
		foreach (IReadOnlyList<ShippedVariant> variants in perProject)
		{
			combined.UnionWith(variants);
		}

		return Order(combined);
	}

	/// <summary>
	/// Gets the short label a variant is shown under.
	/// </summary>
	/// <param name="variant">The variant to label.</param>
	/// <returns>The label.</returns>
	public static string ToLabel(ShippedVariant variant) => variant switch
	{
		ShippedVariant.Library => "lib",
		ShippedVariant.ConsoleApp => "cli",
		ShippedVariant.App => "app",
		ShippedVariant.Tool => "tool",
		_ => variant.ToString().ToLowerInvariant(),
	};

	/// <summary>
	/// Puts variants into a fixed order so a repository's badges do not reshuffle between runs.
	/// </summary>
	/// <param name="variants">The variants to order.</param>
	/// <returns>The ordered variants.</returns>
	private static IReadOnlyList<ShippedVariant> Order(IEnumerable<ShippedVariant> variants) =>
		[.. variants.OrderBy(static v => v)];

	/// <summary>
	/// Matches both ways a project declares an SDK, capturing the variant suffix when there is one:
	/// the <c>Sdk</c> attribute on <c>Project</c>, and a nested <c>Sdk</c> element with a <c>Name</c>.
	/// </summary>
	/// <returns>The compiled regex.</returns>
	[GeneratedRegex(
		"""(?:Sdk\s*=\s*"|<Sdk\s+Name\s*=\s*")ktsu\.Sdk(?:\.(?<variant>[A-Za-z]+))?(?:/[\d.]+)?"?""",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
		MatchTimeoutMilliseconds)]
	private static partial Regex SdkDeclarationRegex();

	[GeneratedRegex(SdkReferencePatterns.OutputTypeExe, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
	private static partial Regex OutputTypeExeRegex();

	[GeneratedRegex(SdkReferencePatterns.OutputTypeWinExe, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
	private static partial Regex OutputTypeWinExeRegex();

	[GeneratedRegex(SdkReferencePatterns.NotPackable, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
	private static partial Regex NotPackableRegex();
}
