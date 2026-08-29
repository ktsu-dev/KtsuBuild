// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Profile;

using System.Text.RegularExpressions;
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

	/// <summary>A windowed application. Declared by <c>ktsu.Sdk.App</c>.</summary>
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

	private static readonly string[] SupportingDirectories =
	[
		"test", "tests", "example", "examples", "sample", "samples",
		"benchmark", "benchmarks", "demo", "demos",
	];

	/// <summary>
	/// Determines whether a project is part of what the repository ships.
	/// </summary>
	/// <param name="path">The project path, relative to the repository root.</param>
	/// <returns><see langword="false"/> for tests, benchmarks, samples, examples, and demos, which
	/// support the deliverable rather than being it.</returns>
	/// <remarks>
	/// Both the file name and the directories above it are checked. ImGuiApp keeps its demo
	/// applications under <c>examples/</c> with names that say nothing about being demos, so filtering
	/// on the file name alone would report the repository as shipping applications it does not.
	/// </remarks>
	public static bool IsShippingProject(string path)
	{
		Ensure.NotNull(path);

		if (SupportingProjectRegex().IsMatch(path))
		{
			return false;
		}

		string[] segments = path.Split('/', '\\');
		for (int i = 0; i < segments.Length - 1; i++)
		{
			if (SupportingDirectories.Contains(segments[i], StringComparer.OrdinalIgnoreCase))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Reads what a single project ships from its SDK declarations.
	/// </summary>
	/// <param name="projectContent">The contents of the project file.</param>
	/// <returns>The variants the project declares. Empty when the project does not use the ktsu SDK at all.</returns>
	/// <remarks>
	/// A project that declares plain <c>ktsu.Sdk</c> and nothing else is a library, because the SDK
	/// packs library projects by default. Platform SDKs such as <c>ktsu.Sdk.Windows</c> say which
	/// platform a project targets rather than what kind of thing it is, so they are ignored here.
	/// </remarks>
	public static IReadOnlyList<ShippedVariant> FromProject(string? projectContent)
	{
		if (string.IsNullOrEmpty(projectContent))
		{
			return [];
		}

		HashSet<ShippedVariant> variants = [];
		bool declaresSdk = false;

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
		}

		if (declaresSdk && variants.Count == 0)
		{
			variants.Add(ShippedVariant.Library);
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
	/// Determines whether a set of variants includes something a user runs rather than references.
	/// </summary>
	/// <param name="variants">The variants to check.</param>
	/// <returns><see langword="true"/> when the repository ships a runnable program.</returns>
	public static bool IncludesExecutable(IEnumerable<ShippedVariant> variants)
	{
		Ensure.NotNull(variants);

		return variants.Any(static v => v is ShippedVariant.ConsoleApp or ShippedVariant.App or ShippedVariant.Tool);
	}

	/// <summary>
	/// Puts variants into a fixed order so a repository's badges do not reshuffle between runs.
	/// </summary>
	/// <param name="variants">The variants to order.</param>
	/// <returns>The ordered variants.</returns>
	private static IReadOnlyList<ShippedVariant> Order(IEnumerable<ShippedVariant> variants) =>
		[.. variants.OrderBy(static v => v)];

	/// <summary>
	/// Matches test, benchmark, sample, example, and demo projects by file name.
	/// </summary>
	/// <returns>The compiled regex.</returns>
	/// <remarks>
	/// Demo belongs here for the same reason sample and example do. They name the same thing, and a
	/// demo left in the list reports its SDK as something the repository ships, so Keybinding would
	/// claim a command line program it does not have.
	/// </remarks>
	[GeneratedRegex(
		@"(Benchmark|Test|Sample|Example|Demo)s?\.csproj$",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
		MatchTimeoutMilliseconds)]
	private static partial Regex SupportingProjectRegex();

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
}
