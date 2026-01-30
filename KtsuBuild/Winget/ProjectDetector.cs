// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Winget;

using System.Text.RegularExpressions;
using System.Xml.Linq;
using static Polyfill;

/// <summary>
/// Detects project type and extracts metadata for Winget manifest generation.
/// </summary>
public class ProjectDetector
{
	private static readonly Regex TitleRegex = new(@"^#\s+(.+?)(?=\r?\n|$)", RegexOptions.Multiline | RegexOptions.Compiled);
	/// <summary>
	/// Detects project information from the repository.
	/// </summary>
	/// <param name="rootDirectory">The repository root directory.</param>
	/// <returns>The detected project info.</returns>
	public ProjectInfo Detect(string rootDirectory)
	{
		Ensure.NotNull(rootDirectory);
		var projectInfo = new ProjectInfo();

		// Try to get version from VERSION.md
		string versionFile = Path.Combine(rootDirectory, "VERSION.md");
		if (File.Exists(versionFile))
		{
			projectInfo.Version = File.ReadAllText(versionFile).Trim();
		}

		// Try to get publisher from AUTHORS.md
		string authorsFile = Path.Combine(rootDirectory, "AUTHORS.md");
		if (File.Exists(authorsFile))
		{
			string[] lines = File.ReadAllLines(authorsFile);
			if (lines.Length > 0)
			{
				projectInfo.Publisher = lines[0].Trim();
			}
		}

		// Try to get description from README.md
		string readmeFile = Path.Combine(rootDirectory, "README.md");
		if (File.Exists(readmeFile))
		{
			string content = File.ReadAllText(readmeFile);

			// Extract name from title
			var titleMatch = TitleRegex.Match(content);
			if (titleMatch.Success)
			{
				projectInfo.Name = titleMatch.Groups[1].Value.Trim();
			}

			// Extract short description (first non-title, non-empty line)
			string[] lines = content.Split('\n');
			foreach (string line in lines.Skip(1))
			{
				string trimmed = line.Trim();
				if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith('#'))
				{
					// Check for > style quote descriptions
					if (trimmed.StartsWith('>'))
					{
						projectInfo.ShortDescription = trimmed[1..].Trim();
					}
					else
					{
						projectInfo.ShortDescription = trimmed;
					}
					break;
				}
			}
		}

		// Try to get full description from DESCRIPTION.md
		string descFile = Path.Combine(rootDirectory, "DESCRIPTION.md");
		if (File.Exists(descFile))
		{
			projectInfo.Description = File.ReadAllText(descFile).Trim();
		}
		else if (!string.IsNullOrEmpty(projectInfo.ShortDescription))
		{
			projectInfo.Description = projectInfo.ShortDescription;
		}

		// Try to get tags from TAGS.md
		string tagsFile = Path.Combine(rootDirectory, "TAGS.md");
		if (File.Exists(tagsFile))
		{
			string tagsContent = File.ReadAllText(tagsFile).Trim();
			projectInfo.Tags = [.. tagsContent.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(t => t.Replace(" ", "-").Replace("--", "-"))
				.Take(10)];
		}

		// Detect project type
		DetectCSharpProject(rootDirectory, projectInfo);
		DetectNodeProject(rootDirectory, projectInfo);
		DetectRustProject(rootDirectory, projectInfo);

		// Fall back to directory name if no name found
		if (string.IsNullOrEmpty(projectInfo.Name))
		{
			projectInfo.Name = Path.GetFileName(rootDirectory);
		}

		return projectInfo;
	}

	/// <summary>
	/// Checks if the project is a library-only project (no executables).
	/// </summary>
	/// <param name="rootDirectory">The repository root directory.</param>
	/// <param name="projectInfo">The detected project info.</param>
	/// <returns>True if this is a library-only project.</returns>
	public bool IsLibraryOnlyProject(string rootDirectory, ProjectInfo projectInfo)
	{
		Ensure.NotNull(rootDirectory);
		Ensure.NotNull(projectInfo);

		if (projectInfo.Type == "csharp")
		{
			string[] csprojFiles = Directory.GetFiles(rootDirectory, "*.csproj", SearchOption.AllDirectories);
			string repoName = Path.GetFileName(rootDirectory);

			bool hasApplications = false;
			bool isMainProjectLibrary = false;

			foreach (string csproj in csprojFiles)
			{
				string content = File.ReadAllText(csproj);
				string projectName = Path.GetFileNameWithoutExtension(csproj);

				// Skip test and demo projects
				if (IsTestOrDemoProject(projectName, content))
				{
					continue;
				}

				bool isMainProject = projectName == repoName || projectName.StartsWith($"{repoName}.");
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

			return isMainProjectLibrary && !hasApplications;
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

	private static void DetectCSharpProject(string rootDirectory, ProjectInfo projectInfo)
	{
		string[] csprojFiles = Directory.GetFiles(rootDirectory, "*.csproj", SearchOption.AllDirectories);
		if (csprojFiles.Length == 0)
		{
			return;
		}

		projectInfo.Type = "csharp";
		string csproj = csprojFiles[0];
		string content = File.ReadAllText(csproj);

		// Try to parse as XML
		try
		{
			var doc = XDocument.Parse(content);

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

			if (string.IsNullOrEmpty(projectInfo.Description))
			{
				projectInfo.Description = doc.Descendants("Description").FirstOrDefault()?.Value ?? string.Empty;
				if (!string.IsNullOrEmpty(projectInfo.Description) && string.IsNullOrEmpty(projectInfo.ShortDescription))
				{
					projectInfo.ShortDescription = projectInfo.Description.Split('.')[0] + ".";
				}
			}

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
				projectInfo.Tags = [.. tags.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
			}

			// Get executable name
			string? wingetExe = doc.Descendants("WinGetPackageExecutable").FirstOrDefault()?.Value;
			projectInfo.ExecutableName = wingetExe ?? $"{projectInfo.Name}.exe";

			// Get command alias
			string? cmdAlias = doc.Descendants("WinGetCommandAlias").FirstOrDefault()?.Value;
			projectInfo.CommandAlias = cmdAlias;
		}
		catch
		{
			// Fall back to regex parsing
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
		if (!projectInfo.Tags.Contains("csharp"))
		{
			projectInfo.Tags.Add("csharp");
		}

		// Default file extensions
		if (projectInfo.FileExtensions.Count == 0)
		{
			projectInfo.FileExtensions = ["cs", "json", "xml", "config", "txt"];
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
		var nameMatch = Regex.Match(content, "\"name\"\\s*:\\s*\"([^\"]+)\"");
		if (nameMatch.Success && string.IsNullOrEmpty(projectInfo.Name))
		{
			projectInfo.Name = nameMatch.Groups[1].Value;
		}

		var descMatch = Regex.Match(content, "\"description\"\\s*:\\s*\"([^\"]+)\"");
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

		var nameMatch = Regex.Match(content, "name\\s*=\\s*\"([^\"]+)\"");
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

	private static bool IsExecutableProject(string content)
	{
		return Regex.IsMatch(content, "<OutputType>\\s*Exe\\s*</OutputType>", RegexOptions.IgnoreCase) ||
			   Regex.IsMatch(content, "<OutputType>\\s*WinExe\\s*</OutputType>", RegexOptions.IgnoreCase) ||
			   Regex.IsMatch(content, "Sdk=\"[^\"]*\\.App[/\"]", RegexOptions.IgnoreCase);
	}

	private static bool IsLibraryProject(string content)
	{
		return Regex.IsMatch(content, "<OutputType>\\s*Library\\s*</OutputType>", RegexOptions.IgnoreCase) ||
			   content.Contains("<PackageId>", StringComparison.OrdinalIgnoreCase) ||
			   content.Contains("<GeneratePackageOnBuild>true</GeneratePackageOnBuild>", StringComparison.OrdinalIgnoreCase) ||
			   content.Contains("<IsPackable>true</IsPackable>", StringComparison.OrdinalIgnoreCase) ||
			   Regex.IsMatch(content, "Sdk=\"[^\"]*\\.Lib[/\"]", RegexOptions.IgnoreCase) ||
			   content.Contains("<TargetFrameworks>", StringComparison.OrdinalIgnoreCase);
	}

}
