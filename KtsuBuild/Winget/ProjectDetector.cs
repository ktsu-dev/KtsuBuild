// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Winget;

using System.Text.RegularExpressions;
using System.Xml.Linq;
using KtsuBuild.Metadata;
using KtsuBuild.Utilities;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Detects project type and extracts metadata for Winget manifest generation.
/// </summary>
public static class ProjectDetector
{
	/// <summary>
	/// Project type identifier for C# repositories.
	/// </summary>
	private const string CSharpProjectType = "csharp";

#pragma warning disable SYSLIB1045 // GeneratedRegex not available in netstandard2.0/2.1
	private static readonly Regex TitleRegex = new(@"^#\s+(.+?)(?=\r?\n|$)", RegexOptions.Multiline | RegexOptions.Compiled, RegexDefaults.MatchTimeout);
	private static readonly Regex OutputTypeExeRegex = new(SdkReferencePatterns.OutputTypeExe, RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexDefaults.MatchTimeout);
	private static readonly Regex OutputTypeWinExeRegex = new(SdkReferencePatterns.OutputTypeWinExe, RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexDefaults.MatchTimeout);
	// Matches every executable ktsu.Sdk suffix, in both the single-attribute form
	// (<Project Sdk="ktsu.Sdk.App/1.0.0">) and the multi-element form (<Sdk Name="ktsu.Sdk.App" />)
	// that ktsu.Sdk.* projects actually use. SdkReferencePatterns holds the suffix list, shared
	// with DotNet.DotNetService so the two classifications cannot drift apart.
	private static readonly Regex SdkExecutableRegex = new(SdkReferencePatterns.ExecutableSdk, RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexDefaults.MatchTimeout);
	private static readonly Regex OutputTypeLibraryRegex = new(@"<OutputType>\s*Library\s*</OutputType>", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexDefaults.MatchTimeout);
	private static readonly Regex SdkLibRegex = new(@"Sdk=""[^""]*\.Lib[/""]", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexDefaults.MatchTimeout);
	private static readonly Regex NodeNameRegex = new(@"""name""\s*:\s*""([^""]+)""", RegexOptions.Compiled, RegexDefaults.MatchTimeout);
	private static readonly Regex NodeDescriptionRegex = new(@"""description""\s*:\s*""([^""]+)""", RegexOptions.Compiled, RegexDefaults.MatchTimeout);
	private static readonly Regex CargoNameRegex = new(@"name\s*=\s*""([^""]+)""", RegexOptions.Compiled, RegexDefaults.MatchTimeout);
#pragma warning restore SYSLIB1045
	/// <summary>
	/// Detects project information from the repository.
	/// </summary>
	/// <param name="rootDirectory">The repository root directory.</param>
	/// <returns>The detected project info.</returns>
	/// <remarks>The repository name is taken from the root directory's name. Use
	/// <see cref="Detect(string, string?)"/> when the real repository name is known.</remarks>
	public static ProjectInfo Detect(string rootDirectory) => Detect(rootDirectory, repositoryName: null);

	/// <summary>
	/// Detects project information from the repository.
	/// </summary>
	/// <param name="rootDirectory">The repository root directory.</param>
	/// <param name="repositoryName">The repository's name (for example, from its GitHub remote), used with the root
	/// directory's name to recognise its main project. May be null when it isn't known.</param>
	/// <returns>The detected project info.</returns>
	public static ProjectInfo Detect(string rootDirectory, string? repositoryName)
	{
		Ensure.NotNull(rootDirectory);
		string[] repoNames = ResolveRepositoryNames(rootDirectory, repositoryName);
		ProjectInfo projectInfo = new();

		ApplyVersion(rootDirectory, projectInfo);
		ApplyPublisher(rootDirectory, projectInfo);
		ApplyReadme(rootDirectory, projectInfo);
		ApplyDescription(rootDirectory, projectInfo);
		ApplyTags(rootDirectory, projectInfo);

		// Detect project type
		DetectCSharpProject(rootDirectory, repoNames, projectInfo);
		DetectNodeProject(rootDirectory, projectInfo);
		DetectRustProject(rootDirectory, projectInfo);

		// After the project file, so a declared license expression is known
		ApplyLicense(rootDirectory, projectInfo);
		ApplyCopyright(rootDirectory, projectInfo);

		// Fall back to directory name if no name found
		if (string.IsNullOrEmpty(projectInfo.Name))
		{
			projectInfo.Name = repoNames.FirstOrDefault() ?? string.Empty;
		}

		return projectInfo;
	}

	/// <summary>
	/// Works out the names a repository's main project may carry, most authoritative first.
	/// </summary>
	/// <param name="rootDirectory">The repository root directory.</param>
	/// <param name="repositoryName">The repository's name, when known.</param>
	/// <returns>
	/// <paramref name="repositoryName"/> when it is given, then the root directory's name, read after any trailing
	/// directory separators are removed so <c>./Repo/</c> names <c>Repo</c> rather than nothing. Empty and repeated
	/// names are left out.
	/// </returns>
	private static string[] ResolveRepositoryNames(string rootDirectory, string? repositoryName)
	{
		string folderName = Path.GetFileName(rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		return [.. new[] { repositoryName, folderName }
			.Where(static name => !string.IsNullOrEmpty(name))
			.Select(static name => name!)
			.Distinct(StringComparer.OrdinalIgnoreCase)];
	}

	/// <summary>
	/// Whether a project name is the repository's main project, or one of its named variants
	/// (<c>&lt;Repo&gt;.App</c>, <c>&lt;Repo&gt;.Tool</c> and so on). Clone folders often differ from the repository
	/// name only in case, so the comparison ignores case.
	/// </summary>
	private static bool IsMainProjectName(string projectName, string repoName) =>
		string.Equals(projectName, repoName, StringComparison.OrdinalIgnoreCase)
		|| projectName.StartsWith($"{repoName}.", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Reads the project version from VERSION.md, when present.
	/// </summary>
	private static void ApplyVersion(string rootDirectory, ProjectInfo projectInfo)
	{
		string versionFile = Path.Combine(rootDirectory, "VERSION.md");
		if (File.Exists(versionFile))
		{
			projectInfo.Version = File.ReadAllText(versionFile).Trim();
		}
	}

	/// <summary>
	/// Names the license from LICENSE.md, when present.
	/// </summary>
	/// <remarks>
	/// LICENSE.md is the source of truth. An MIT LICENSE.md is published as MIT. Any other LICENSE.md
	/// is kept as written by the metadata generator, so it is published under the project file's
	/// <c>PackageLicenseExpression</c> when that names something other than MIT, and otherwise under
	/// LICENSE.md's own first line.
	/// </remarks>
	private static void ApplyLicense(string rootDirectory, ProjectInfo projectInfo)
	{
		string licenseFile = Path.Combine(rootDirectory, "LICENSE.md");
		if (!File.Exists(licenseFile))
		{
			return;
		}

		string content = File.ReadAllText(licenseFile);
		if (!LicenseGenerator.IsCustomLicense(content))
		{
			projectInfo.License = "MIT";
			return;
		}

		if (projectInfo.License.Equals("MIT", StringComparison.OrdinalIgnoreCase))
		{
			projectInfo.License = FirstNonEmptyLine(content).TrimStart('#').Trim();
		}
	}

	/// <summary>
	/// Reads the copyright notice from the first line of COPYRIGHT.md, when present.
	/// </summary>
	private static void ApplyCopyright(string rootDirectory, ProjectInfo projectInfo)
	{
		string copyrightFile = Path.Combine(rootDirectory, "COPYRIGHT.md");
		if (!File.Exists(copyrightFile))
		{
			return;
		}

		string copyright = FirstNonEmptyLine(File.ReadAllText(copyrightFile));
		if (copyright.Length > 0)
		{
			projectInfo.Copyright = copyright;
		}
	}

	private static string FirstNonEmptyLine(string content) =>
		content.Split('\n').Select(static line => line.Trim()).FirstOrDefault(static line => line.Length > 0) ?? string.Empty;

	/// <summary>
	/// Reads the publisher from the first line of AUTHORS.md, when present.
	/// </summary>
	private static void ApplyPublisher(string rootDirectory, ProjectInfo projectInfo)
	{
		string authorsFile = Path.Combine(rootDirectory, "AUTHORS.md");
		if (!File.Exists(authorsFile))
		{
			return;
		}

		string[] lines = File.ReadAllLines(authorsFile);
		if (lines.Length > 0)
		{
			projectInfo.Publisher = lines[0].Trim();
		}
	}

	/// <summary>
	/// Reads the project name from the README.md title and the short description from the
	/// first non-title, non-empty line beneath it.
	/// </summary>
	private static void ApplyReadme(string rootDirectory, ProjectInfo projectInfo)
	{
		string readmeFile = Path.Combine(rootDirectory, "README.md");
		if (!File.Exists(readmeFile))
		{
			return;
		}

		string content = File.ReadAllText(readmeFile);

		// Extract name from title
		Match titleMatch = TitleRegex.Match(content);
		if (titleMatch.Success)
		{
			projectInfo.Name = titleMatch.Groups[1].Value.Trim();
		}

		// Extract short description (first non-title, non-empty line)
		string[] lines = content.Split('\n');
		string? description = lines
			.Skip(1)
			.Select(static line => line.Trim())
			.FirstOrDefault(static trimmed => !string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith('#'));

		if (description is not null)
		{
			// Check for > style quote descriptions
			projectInfo.ShortDescription = description.StartsWith('>') ? description[1..].Trim() : description;
		}
	}

	/// <summary>
	/// Reads the full description from DESCRIPTION.md, falling back to the short
	/// description taken from the README.
	/// </summary>
	private static void ApplyDescription(string rootDirectory, ProjectInfo projectInfo)
	{
		string descFile = Path.Combine(rootDirectory, "DESCRIPTION.md");
		if (File.Exists(descFile))
		{
			projectInfo.Description = File.ReadAllText(descFile).Trim();
		}
		else if (!string.IsNullOrEmpty(projectInfo.ShortDescription))
		{
			projectInfo.Description = projectInfo.ShortDescription;
		}
	}

	/// <summary>
	/// Reads up to ten normalized tags from TAGS.md, when present.
	/// </summary>
	private static void ApplyTags(string rootDirectory, ProjectInfo projectInfo)
	{
		string tagsFile = Path.Combine(rootDirectory, "TAGS.md");
		if (!File.Exists(tagsFile))
		{
			return;
		}

		string tagsContent = File.ReadAllText(tagsFile).Trim();
		projectInfo.Tags = [.. tagsContent.Split(';', StringSplitOptions.RemoveEmptyEntries)
			.Select(static t => t.Trim())
			.Where(static t => !string.IsNullOrEmpty(t))
			.Select(static t => t.Replace(" ", "-").Replace("--", "-"))
			.Take(10)];
	}

	/// <summary>
	/// Checks if the project is a library-only project (no executables).
	/// </summary>
	/// <param name="rootDirectory">The repository root directory.</param>
	/// <param name="projectInfo">The detected project info.</param>
	/// <returns>True if this is a library-only project.</returns>
	/// <remarks>The repository name is taken from the root directory's name. Use
	/// <see cref="IsLibraryOnlyProject(string, ProjectInfo, string?)"/> when the real repository name is known.</remarks>
	public static bool IsLibraryOnlyProject(string rootDirectory, ProjectInfo projectInfo) =>
		IsLibraryOnlyProject(rootDirectory, projectInfo, repositoryName: null);

	/// <summary>
	/// Checks if the project is a library-only project (no executables).
	/// </summary>
	/// <param name="rootDirectory">The repository root directory.</param>
	/// <param name="projectInfo">The detected project info.</param>
	/// <param name="repositoryName">The repository's name (for example, from its GitHub remote), used with the root
	/// directory's name to recognise its main project. May be null when it isn't known.</param>
	/// <returns>True if this is a library-only project.</returns>
	public static bool IsLibraryOnlyProject(string rootDirectory, ProjectInfo projectInfo, string? repositoryName)
	{
		Ensure.NotNull(rootDirectory);
		Ensure.NotNull(projectInfo);

		if (projectInfo.Type == CSharpProjectType)
		{
			return IsLibraryOnlyCSharpProject(rootDirectory, ResolveRepositoryNames(rootDirectory, repositoryName));
		}

		if (projectInfo.Type == "node")
		{
			string packageJsonPath = Path.Combine(rootDirectory, "package.json");
			if (File.Exists(packageJsonPath))
			{
				string content = File.ReadAllText(packageJsonPath);
				// Check if it has no bin field (library)
				return !content.Contains("\"bin\"");
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether a C# repository publishes only libraries, by classifying every
	/// non-test project in it.
	/// </summary>
	/// <param name="rootDirectory">The repository root directory.</param>
	/// <param name="repoNames">The names the repository's main project may carry.</param>
	/// <returns>True when the repository has nothing executable to package.</returns>
	private static bool IsLibraryOnlyCSharpProject(string rootDirectory, string[] repoNames)
	{
		string[] csprojFiles = Directory.GetFiles(rootDirectory, "*.csproj", SearchOption.AllDirectories);

		bool hasApplications = false;
		bool isMainProjectLibrary = false;
		bool hasNonTestProjects = false;

		foreach (string csproj in csprojFiles)
		{
			string content = File.ReadAllText(csproj);
			string projectName = Path.GetFileNameWithoutExtension(csproj);

			// Skip test and demo projects
			if (IsTestOrDemoProject(projectName, content))
			{
				continue;
			}

			hasNonTestProjects = true;

			bool isMainProject = repoNames.Any(repoName => IsMainProjectName(projectName, repoName));
			bool isExecutable = IsExecutableProject(content);
			bool isLibrary = IsLibraryProject(content) || !isExecutable;

			if (isLibrary && isMainProject)
			{
				isMainProjectLibrary = true;
			}

			if (isExecutable)
			{
				hasApplications = true;
			}
		}

		// If all projects are test/demo, there's nothing to package
		if (!hasNonTestProjects)
		{
			return true;
		}

		return isMainProjectLibrary && !hasApplications;
	}

	private static void DetectCSharpProject(string rootDirectory, string[] repoNames, ProjectInfo projectInfo)
	{
		string[] csprojFiles = Directory.GetFiles(rootDirectory, "*.csproj", SearchOption.AllDirectories);
		if (csprojFiles.Length == 0)
		{
			return;
		}

		projectInfo.Type = CSharpProjectType;
		string csproj = SelectPrimaryProject(csprojFiles, repoNames);
		string content = File.ReadAllText(csproj);

		// Try to parse as XML
		try
		{
			ApplyCsprojMetadata(XDocument.Parse(content), csproj, projectInfo);
		}
		catch (System.Xml.XmlException)
		{
			// Fall back to simple fallback
			if (string.IsNullOrEmpty(projectInfo.Name))
			{
				projectInfo.Name = Path.GetFileNameWithoutExtension(csproj);
			}

			projectInfo.ExecutableName = $"{projectInfo.Name}.exe";
		}

		// Add C# tags
		if (!projectInfo.Tags.Contains("dotnet"))
		{
			projectInfo.Tags.Add("dotnet");
		}
		if (!projectInfo.Tags.Contains(CSharpProjectType))
		{
			projectInfo.Tags.Add(CSharpProjectType);
		}

		// Default file extensions
		if (projectInfo.FileExtensions.Count == 0)
		{
			projectInfo.FileExtensions = ["cs", "json", "xml", "config", "txt"];
		}
	}

	/// <summary>
	/// Chooses the project file whose metadata describes what the repository actually ships.
	/// </summary>
	/// <param name="csprojFiles">Every project file found under the repository root.</param>
	/// <param name="repoNames">The names the repository's main project may carry, most authoritative first.</param>
	/// <returns>The project file to read the Winget manifest metadata from.</returns>
	/// <remarks>
	/// <see cref="Directory.GetFiles(string, string, SearchOption)"/> enumerates in filesystem
	/// order, so the first entry is neither stable across machines nor meaningful. Ordering the
	/// candidates and preferring the repository's own project keeps the generated manifest
	/// deterministic and away from test and demo projects, matching how
	/// <see cref="IsLibraryOnlyCSharpProject"/> already classifies the same repository.
	/// </remarks>
	private static string SelectPrimaryProject(string[] csprojFiles, string[] repoNames)
	{
		string[] ordered = [.. csprojFiles.OrderBy(static path => path, StringComparer.Ordinal)];

		string[] candidates = [.. ordered.Where(static path =>
			!IsTestOrDemoProject(Path.GetFileNameWithoutExtension(path), File.ReadAllText(path)))];

		// A repository of nothing but test and demo projects has no better project to describe
		// itself with, so fall back to the full list rather than to no project at all.
		if (candidates.Length == 0)
		{
			candidates = ordered;
		}

		foreach (string repoName in repoNames)
		{
			string? match = candidates.FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), repoName, StringComparison.OrdinalIgnoreCase))
				?? candidates.FirstOrDefault(path => IsMainProjectName(Path.GetFileNameWithoutExtension(path), repoName));
			if (match is not null)
			{
				return match;
			}
		}

		return candidates[0];
	}

	/// <summary>
	/// Fills in any project metadata not already detected from the repository files, using
	/// the MSBuild properties in a parsed project file.
	/// </summary>
	/// <param name="doc">The parsed project file.</param>
	/// <param name="csproj">The path to the project file, used for name fallbacks.</param>
	/// <param name="projectInfo">The project info to fill in.</param>
	private static void ApplyCsprojMetadata(XDocument doc, string csproj, ProjectInfo projectInfo)
	{
		if (string.IsNullOrEmpty(projectInfo.Name))
		{
			projectInfo.Name = doc.Descendants("AssemblyName").FirstOrDefault()?.Value ??
				doc.Descendants("Product").FirstOrDefault()?.Value ??
				Path.GetFileNameWithoutExtension(csproj);
		}

		if (string.IsNullOrEmpty(projectInfo.Version))
		{
			projectInfo.Version = doc.Descendants("Version").FirstOrDefault()?.Value ?? "1.0.0";
		}

		ApplyCsprojDescription(doc, projectInfo);

		if (string.IsNullOrEmpty(projectInfo.Publisher))
		{
			string? authors = doc.Descendants("Authors").FirstOrDefault()?.Value;
			if (!string.IsNullOrEmpty(authors))
			{
				projectInfo.Publisher = authors.Split(',')[0].Trim();
			}
		}

		// Get tags
		string? tags = doc.Descendants("PackageTags").FirstOrDefault()?.Value;
		if (!string.IsNullOrEmpty(tags) && projectInfo.Tags.Count == 0)
		{
			projectInfo.Tags = [.. tags.Split(';', StringSplitOptions.RemoveEmptyEntries)
				.Select(static t => t.Trim())
				.Where(static t => !string.IsNullOrEmpty(t))];
		}

		// Get executable name
		string? wingetExe = doc.Descendants("WinGetPackageExecutable").FirstOrDefault()?.Value;
		projectInfo.ExecutableName = wingetExe ?? $"{projectInfo.Name}.exe";

		// Get command alias
		projectInfo.CommandAlias = doc.Descendants("WinGetCommandAlias").FirstOrDefault()?.Value;

		string? licenseExpression = doc.Descendants("PackageLicenseExpression").FirstOrDefault()?.Value;
		if (!string.IsNullOrWhiteSpace(licenseExpression))
		{
			projectInfo.License = licenseExpression.Trim();
		}
	}

	/// <summary>
	/// Fills in the description (and short description) from the project file, when they
	/// were not already detected from the repository files.
	/// </summary>
	private static void ApplyCsprojDescription(XDocument doc, ProjectInfo projectInfo)
	{
		if (!string.IsNullOrEmpty(projectInfo.Description))
		{
			return;
		}

		projectInfo.Description = doc.Descendants("Description").FirstOrDefault()?.Value ?? string.Empty;
		if (!string.IsNullOrEmpty(projectInfo.Description) && string.IsNullOrEmpty(projectInfo.ShortDescription))
		{
			projectInfo.ShortDescription = projectInfo.Description.Split('.')[0] + ".";
		}
	}

	private static void DetectNodeProject(string rootDirectory, ProjectInfo projectInfo)
	{
		string packageJsonPath = Path.Combine(rootDirectory, "package.json");
		if (!File.Exists(packageJsonPath))
		{
			return;
		}

		projectInfo.Type = "node";
		string content = File.ReadAllText(packageJsonPath);

		// Simple JSON parsing with regex
		Match nameMatch = NodeNameRegex.Match(content);
		if (nameMatch.Success && string.IsNullOrEmpty(projectInfo.Name))
		{
			projectInfo.Name = nameMatch.Groups[1].Value;
		}

		Match descMatch = NodeDescriptionRegex.Match(content);
		if (descMatch.Success && string.IsNullOrEmpty(projectInfo.ShortDescription))
		{
			projectInfo.ShortDescription = descMatch.Groups[1].Value;
		}

		projectInfo.ExecutableName = $"{projectInfo.Name}.js";

		if (!projectInfo.Tags.Contains("nodejs"))
		{
			projectInfo.Tags.Add("nodejs");
		}
		if (!projectInfo.Tags.Contains("javascript"))
		{
			projectInfo.Tags.Add("javascript");
		}

		if (projectInfo.FileExtensions.Count == 0)
		{
			projectInfo.FileExtensions = ["js", "json", "ts", "html", "css"];
		}
	}

	private static void DetectRustProject(string rootDirectory, ProjectInfo projectInfo)
	{
		string cargoPath = Path.Combine(rootDirectory, "Cargo.toml");
		if (!File.Exists(cargoPath))
		{
			return;
		}

		projectInfo.Type = "rust";
		string content = File.ReadAllText(cargoPath);

		Match nameMatch = CargoNameRegex.Match(content);
		if (nameMatch.Success && string.IsNullOrEmpty(projectInfo.Name))
		{
			projectInfo.Name = nameMatch.Groups[1].Value;
		}

		projectInfo.ExecutableName = projectInfo.Name;

		if (!projectInfo.Tags.Contains("rust"))
		{
			projectInfo.Tags.Add("rust");
		}

		if (projectInfo.FileExtensions.Count == 0)
		{
			projectInfo.FileExtensions = ["rs", "toml"];
		}
	}

	private static bool IsTestOrDemoProject(string projectName, string content)
	{
		return projectName.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
			   projectName.Contains("Demo", StringComparison.OrdinalIgnoreCase) ||
			   projectName.Contains("Example", StringComparison.OrdinalIgnoreCase) ||
			   projectName.Contains("Sample", StringComparison.OrdinalIgnoreCase) ||
			   content.Contains("Sdk=\"Microsoft.NET.Sdk.Test\"", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsExecutableProject(string content) =>
		OutputTypeExeRegex.IsMatch(content) ||
		OutputTypeWinExeRegex.IsMatch(content) ||
		SdkExecutableRegex.IsMatch(content);

	private static bool IsLibraryProject(string content) =>
		OutputTypeLibraryRegex.IsMatch(content) ||
		content.Contains("<PackageId>", StringComparison.OrdinalIgnoreCase) ||
		content.Contains("<GeneratePackageOnBuild>true</GeneratePackageOnBuild>", StringComparison.OrdinalIgnoreCase) ||
		content.Contains("<IsPackable>true</IsPackable>", StringComparison.OrdinalIgnoreCase) ||
		SdkLibRegex.IsMatch(content) ||
		content.Contains("<TargetFrameworks>", StringComparison.OrdinalIgnoreCase);
}
