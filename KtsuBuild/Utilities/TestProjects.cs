// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Utilities;

using System;
using System.IO;
using System.Text.RegularExpressions;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Recognises test projects by their name and by what their project file declares.
/// </summary>
/// <remarks>
/// <c>DotNet.DotNetService</c> (which projects to test, and which to leave out of release) and
/// <c>Winget.ProjectDetector</c> (whether a repository is library-only) need the same answer.
/// A narrower copy in the winget detector once let a test project named for neither convention
/// count as an application there while release skipped it (ktsu-dev/KtsuBuild#300).
/// </remarks>
public static class TestProjects
{
#pragma warning disable SYSLIB1045 // GeneratedRegex not available in netstandard2.0/2.1
	private static readonly Regex SdkTestRegex = new(@"Sdk(?:\s+Name)?\s*=\s*""[^""]*\.Test[/""]", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexDefaults.MatchTimeout);
	// MSTest.Sdk and the Microsoft.NET.Test.Sdk package both set IsTestProject in their own props,
	// so a project using either never spells the property out. MSTest.Sdk is matched in the
	// attribute form, versioned or not and in a ;-separated list, and in the <Sdk Name> element form.
	private static readonly Regex MSTestSdkRegex = new(@"Sdk(?:\s+Name)?\s*=\s*""(?:[^""]*;)?\s*MSTest\.Sdk(?:/[^"";]*)?\s*[;""]", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexDefaults.MatchTimeout);
	private static readonly Regex TestSdkPackageRegex = new(@"<PackageReference\b[^>]*\bInclude\s*=\s*""\s*Microsoft\.NET\.Test\.Sdk\s*""", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexDefaults.MatchTimeout);
#pragma warning restore SYSLIB1045

	/// <summary>
	/// Determines whether a project file is a test project.
	/// </summary>
	/// <param name="projectPath">The path of the project file.</param>
	/// <returns><see langword="true"/> when the project's name or content marks it as a test project;
	/// <see langword="false"/> when it does not, or the file does not exist.</returns>
	public static bool IsTestProject(string projectPath)
	{
		Ensure.NotNull(projectPath);
		if (!File.Exists(projectPath))
		{
			return false;
		}

		string fileName = Path.GetFileNameWithoutExtension(projectPath);
		string dirName = Path.GetFileName(Path.GetDirectoryName(projectPath) ?? string.Empty);

		// Check name patterns
		if (fileName.EndsWith(".Test", StringComparison.OrdinalIgnoreCase) ||
			fileName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
			dirName.EndsWith(".Test", StringComparison.OrdinalIgnoreCase) ||
			dirName == "Test" ||
			dirName == "Tests")
		{
			return true;
		}

		// Check content for a test SDK, the test SDK package, or IsTestProject
		string content = File.ReadAllText(projectPath);
		return content.Contains("<IsTestProject>true</IsTestProject>", StringComparison.OrdinalIgnoreCase) ||
			   content.Contains("Sdk=\"Microsoft.NET.Sdk.Test\"", StringComparison.OrdinalIgnoreCase) ||
			   SdkTestRegex.IsMatch(content) ||
			   MSTestSdkRegex.IsMatch(content) ||
			   TestSdkPackageRegex.IsMatch(content);
	}
}
