// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.DotNet;

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using KtsuBuild.Abstractions;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Implementation of .NET SDK operations.
/// </summary>
/// <param name="processRunner">The process runner.</param>
/// <param name="logger">The build logger.</param>
public class DotNetService(IProcessRunner processRunner, IBuildLogger logger) : IDotNetService
{
	private const string QuietLogger = "-logger:\"Microsoft.Build.Logging.ConsoleLogger,Microsoft.Build;Summary;ForceNoAlign;ShowTimestamp;ShowCommandLine;Verbosity=quiet\"";

#pragma warning disable SYSLIB1045 // GeneratedRegex not available in netstandard2.0/2.1
	private static readonly Regex OutputTypeExeRegex = new(@"<OutputType>\s*Exe\s*</OutputType>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
	private static readonly Regex OutputTypeWinExeRegex = new(@"<OutputType>\s*WinExe\s*</OutputType>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
	private static readonly Regex SdkAppRegex = new(@"Sdk=""[^""]*\.App[/""]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
	private static readonly Regex SdkIosRegex = new(@"Sdk=""[^""]*\.Ios[/""]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
	private static readonly Regex SdkTestRegex = new(@"Sdk=""[^""]*\.Test[/""]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
	private static readonly Regex TargetFrameworkRegex = new(@"<TargetFrameworks?>\s*([^<]+?)\s*</TargetFrameworks?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
#pragma warning restore SYSLIB1045

	/// <inheritdoc/>
	public async Task RestoreAsync(string workingDirectory, bool lockedMode = true, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		logger.WriteStepHeader("Restoring Dependencies");

		string args = $"restore {QuietLogger}";
		int exitCode;
		if (lockedMode)
		{
			args += " --locked-mode";
		}

		exitCode = await processRunner.RunWithCallbackAsync(
			"dotnet",
			args,
			workingDirectory,
			logger.WriteInfo,
			logger.WriteError,
			cancellationToken).ConfigureAwait(false);

		if (exitCode != 0)
		{
			throw new InvalidOperationException($"Restore failed with exit code {exitCode}");
		}
	}

	/// <inheritdoc/>
	public async Task BuildAsync(string workingDirectory, string configuration = "Release", string? additionalArgs = null, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		logger.WriteStepHeader("Building Solution");

		string args = $"build --configuration {configuration} {QuietLogger} --no-incremental --no-restore";
		if (!string.IsNullOrEmpty(additionalArgs))
		{
			args += $" {additionalArgs}";
		}

		int exitCode = await processRunner.RunWithCallbackAsync(
			"dotnet",
			args,
			workingDirectory,
			logger.WriteInfo,
			logger.WriteError,
			cancellationToken).ConfigureAwait(false);

		if (exitCode != 0)
		{
			logger.WriteWarning($"Build failed with exit code {exitCode}. Retrying with detailed verbosity...");

			// Retry with more verbose output
			exitCode = await processRunner.RunWithCallbackAsync(
				"dotnet",
				args,
				workingDirectory,
				logger.WriteInfo,
				logger.WriteError,
				cancellationToken).ConfigureAwait(false);

			if (exitCode != 0)
			{
				// Log all project files for diagnostic purposes
				string[] projects = Directory.GetFiles(workingDirectory, "*.csproj", SearchOption.AllDirectories);
				logger.WriteWarning($"Build failed twice. Found {projects.Length} project file(s):");
				foreach (string proj in projects)
				{
					logger.WriteWarning($"  - {proj}");
				}

				throw new InvalidOperationException($"Build failed with exit code {exitCode}");
			}
		}
	}

	/// <inheritdoc/>
	public async Task TestAsync(string workingDirectory, string configuration = "Release", string? coverageOutputPath = null, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		logger.WriteStepHeader("Running Tests with Coverage");

		// Check for test projects (only those buildable on the current host)
		List<string> testProjects = [.. GetBuildableProjects(workingDirectory).Where(IsTestProject)];
		if (testProjects.Count == 0)
		{
			logger.WriteInfo("No test projects found in solution. Skipping test execution.");
			return;
		}

		logger.WriteInfo($"Found {testProjects.Count} test project(s)");

		string resultsPath = coverageOutputPath ?? "coverage";
		string testResultsPath = Path.Combine(resultsPath, "TestResults");
		Directory.CreateDirectory(testResultsPath);

		string args = $"test --configuration {configuration} --coverage --coverage-output-format xml " +
			$"--coverage-output \"coverage.xml\" --results-directory \"{testResultsPath}\" " +
			$"--report-trx --report-trx-filename TestResults.trx";

		int exitCode = await processRunner.RunWithCallbackAsync(
			"dotnet",
			args,
			workingDirectory,
			logger.WriteInfo,
			logger.WriteError,
			cancellationToken).ConfigureAwait(false);

		if (exitCode != 0)
		{
			throw new InvalidOperationException($"Tests failed with exit code {exitCode}");
		}

		// Copy coverage file to expected location
		CopyCoverageFile(workingDirectory, resultsPath);
	}

	/// <inheritdoc/>
	public async Task PackAsync(string workingDirectory, string outputPath, string configuration = "Release", string? releaseNotesFile = null, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(outputPath);
		logger.WriteStepHeader("Packaging Libraries");

		Directory.CreateDirectory(outputPath);

		// Get non-test project files to pack individually (only those buildable on the current host)
		List<string> packableProjects = [.. GetBuildableProjects(workingDirectory)
			.Where(p => !IsTestProject(p))];

		if (packableProjects.Count == 0)
		{
			logger.WriteInfo("No .NET library projects found to package");
			return;
		}

		string releaseNotesArg = string.Empty;
		if (!string.IsNullOrEmpty(releaseNotesFile) && File.Exists(releaseNotesFile))
		{
			string absolutePath = Path.GetFullPath(releaseNotesFile);
			logger.WriteInfo($"Using release notes from file: {absolutePath}");
			releaseNotesArg = $" -p:PackageReleaseNotesFile=\"{absolutePath}\"";
		}

		// Pack each non-test project individually
		foreach (string project in packableProjects)
		{
			string projectName = Path.GetFileNameWithoutExtension(project);
			logger.WriteInfo($"Packing {projectName}...");

			string args = $"pack \"{project}\" --configuration {configuration} {QuietLogger} --no-build --output \"{outputPath}\"{releaseNotesArg}";

			int exitCode = await processRunner.RunWithCallbackAsync(
				"dotnet",
				args,
				workingDirectory,
				logger.WriteInfo,
				logger.WriteError,
				cancellationToken).ConfigureAwait(false);

			if (exitCode != 0)
			{
				logger.WriteWarning($"Pack failed for {projectName} with exit code {exitCode}");
			}
		}

		// Report on created packages
		string[] packages = Directory.GetFiles(outputPath, "*.nupkg");
		if (packages.Length > 0)
		{
			logger.WriteInfo($"Created {packages.Length} packages in {outputPath}");
			foreach (string package in packages)
			{
				logger.WriteInfo($"  - {Path.GetFileName(package)}");
			}
		}
		else
		{
			logger.WriteInfo("No packages were created (projects may not be configured for packaging)");
		}
	}

	/// <inheritdoc/>
	public async Task PublishAsync(
		string workingDirectory,
		string projectPath,
		string outputPath,
		string runtime,
		string configuration = "Release",
		bool selfContained = true,
		bool singleFile = false,
		CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(projectPath);
		Ensure.NotNull(outputPath);
		Ensure.NotNull(runtime);
		Directory.CreateDirectory(outputPath);

		string args = $"publish \"{projectPath}\" --configuration {configuration} --runtime {runtime} " +
			$"--self-contained {selfContained.ToString().ToLowerInvariant()} --output \"{outputPath}\" " +
			$"-p:PublishSingleFile={singleFile.ToString().ToLowerInvariant()} " +
			$"-p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false {QuietLogger}";

		int exitCode = await processRunner.RunWithCallbackAsync(
			"dotnet",
			args,
			workingDirectory,
			logger.WriteInfo,
			logger.WriteError,
			cancellationToken).ConfigureAwait(false);

		if (exitCode != 0)
		{
			throw new InvalidOperationException($"Publish failed for {projectPath} ({runtime}) with exit code {exitCode}");
		}
	}

	/// <inheritdoc/>
	public async Task BuildIosAsync(
		string workingDirectory,
		string projectPath,
		string runtimeIdentifier,
		string configuration = "Release",
		bool codeSigning = false,
		CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(projectPath);
		Ensure.NotNull(runtimeIdentifier);
		logger.WriteStepHeader($"Building iOS Head ({runtimeIdentifier})");

		// Unsigned by default: disable signing and empty the signing properties so
		// the build needs no certificate or provisioning profile. BuildIpa stays
		// off — packaging the .ipa is the signed release path, not this one.
		string signingArgs = codeSigning
			? string.Empty
			: " -p:EnableCodeSigning=false -p:CodesignKey= -p:CodesignProvision=";

		string args = $"build \"{projectPath}\" --configuration {configuration} " +
			$"-p:RuntimeIdentifier={runtimeIdentifier} -p:BuildIpa=false{signingArgs} {QuietLogger}";

		int exitCode = await processRunner.RunWithCallbackAsync(
			"dotnet",
			args,
			workingDirectory,
			logger.WriteInfo,
			logger.WriteError,
			cancellationToken).ConfigureAwait(false);

		if (exitCode != 0)
		{
			throw new InvalidOperationException($"iOS build failed for {projectPath} ({runtimeIdentifier}) with exit code {exitCode}");
		}
	}

	/// <inheritdoc/>
	public IReadOnlyList<string> GetIosHeads(string workingDirectory)
	{
		Ensure.NotNull(workingDirectory);
		return [.. GetProjectFiles(workingDirectory)
			.Where(p => GetProjectPlatform(p) == ProjectPlatform.Ios && IsExecutableProject(p))];
	}

	/// <summary>
	/// Finds the <c>.app</c> bundles produced by an iOS build under a search root,
	/// optionally restricted to those whose path contains a runtime-identifier
	/// segment (for example <c>ios-arm64</c>). Returns an empty list when the root
	/// does not exist.
	/// </summary>
	/// <param name="searchRoot">The directory to search (typically <c>bin/{configuration}</c> under the head).</param>
	/// <param name="ridSegment">An optional runtime-identifier path segment to filter on.</param>
	/// <returns>The matching <c>.app</c> bundle directory paths.</returns>
	public static IReadOnlyList<string> FindAppBundles(string searchRoot, string? ridSegment = null)
	{
		Ensure.NotNull(searchRoot);
		if (!Directory.Exists(searchRoot))
		{
			return [];
		}

		IEnumerable<string> bundles = Directory.GetDirectories(searchRoot, "*.app", SearchOption.AllDirectories);
		if (!string.IsNullOrEmpty(ridSegment))
		{
			bundles = bundles.Where(b => b.Contains(ridSegment, StringComparison.OrdinalIgnoreCase));
		}

		return [.. bundles];
	}

	/// <summary>
	/// Lists the top-level entries of an app bundle's <c>Frameworks</c> directory
	/// (the embedded native frameworks and dylibs). Returns an empty list when the
	/// bundle has no <c>Frameworks</c> directory.
	/// </summary>
	/// <param name="appBundlePath">Path to the <c>.app</c> bundle.</param>
	/// <returns>The names of the embedded native frameworks.</returns>
	public static IReadOnlyList<string> GetEmbeddedNativeFrameworks(string appBundlePath)
	{
		Ensure.NotNull(appBundlePath);
		string frameworksDir = Path.Combine(appBundlePath, "Frameworks");
		if (!Directory.Exists(frameworksDir))
		{
			return [];
		}

		return [.. Directory.GetFileSystemEntries(frameworksDir)
			.Select(Path.GetFileName)
			.Where(n => !string.IsNullOrEmpty(n))
			.Cast<string>()];
	}

	/// <summary>
	/// Checks whether an app bundle embeds a native library whose file name starts
	/// with the supplied name, searching the whole bundle (a framework's binary
	/// lives inside a <c>.framework</c> directory, so the match is recursive). This
	/// guards the launch-crash class where a native asset resolves to the wrong
	/// target framework and is silently left out of the device bundle.
	/// </summary>
	/// <param name="appBundlePath">Path to the <c>.app</c> bundle.</param>
	/// <param name="libraryName">The native library name to look for (for example <c>libSkiaSharp</c>).</param>
	/// <returns>True if a matching native library is embedded in the bundle.</returns>
	public static bool BundleContainsNativeLibrary(string appBundlePath, string libraryName)
	{
		Ensure.NotNull(appBundlePath);
		Ensure.NotNull(libraryName);
		if (!Directory.Exists(appBundlePath))
		{
			return false;
		}

		return Directory.EnumerateFileSystemEntries(appBundlePath, "*", SearchOption.AllDirectories)
			.Select(Path.GetFileName)
			.Any(n => n is not null && n.StartsWith(libraryName, StringComparison.OrdinalIgnoreCase));
	}

	/// <inheritdoc/>
	public IReadOnlyList<string> GetProjectFiles(string workingDirectory)
	{
		Ensure.NotNull(workingDirectory);
		return [.. Directory.GetFiles(workingDirectory, "*.csproj", SearchOption.AllDirectories)];
	}

	/// <inheritdoc/>
	public IReadOnlyList<string> GetBuildableProjects(string workingDirectory)
	{
		Ensure.NotNull(workingDirectory);
		return [.. GetProjectFiles(workingDirectory).Where(CanBuildOnCurrentHost)];
	}

	/// <inheritdoc/>
	public ProjectPlatform GetProjectPlatform(string projectPath)
	{
		Ensure.NotNull(projectPath);
		if (!File.Exists(projectPath))
		{
			return ProjectPlatform.Neutral;
		}

		return ClassifyTargetFrameworks(GetTargetFrameworks(File.ReadAllText(projectPath)));
	}

	/// <inheritdoc/>
	public bool CanBuildOnCurrentHost(string projectPath) =>
		CanPlatformBuildOnHost(
			GetProjectPlatform(projectPath),
			RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
			RuntimeInformation.IsOSPlatform(OSPlatform.OSX));

	/// <summary>
	/// Determines whether a project of the given platform can be built on a host
	/// described by the supplied flags. A neutral project builds anywhere; a
	/// Windows project needs a Windows host; an iOS project needs a macOS host.
	/// </summary>
	/// <param name="platform">The project's platform classification.</param>
	/// <param name="hostIsWindows">Whether the host is Windows.</param>
	/// <param name="hostIsMacOs">Whether the host is macOS.</param>
	/// <returns>True if the host can build the project.</returns>
	public static bool CanPlatformBuildOnHost(ProjectPlatform platform, bool hostIsWindows, bool hostIsMacOs) =>
		platform switch
		{
			ProjectPlatform.Windows => hostIsWindows,
			ProjectPlatform.Ios => hostIsMacOs,
			_ => true,
		};

	/// <summary>
	/// Classifies a set of target frameworks into a single <see cref="ProjectPlatform"/>.
	/// A project with any neutral target framework is treated as neutral (it can be
	/// built on any host, selecting a framework where needed). A project whose target
	/// frameworks are all iOS, or all Windows, is classified accordingly. Anything else
	/// (including mixes of platform-specific frameworks) is treated as neutral so it is
	/// not filtered out.
	/// </summary>
	/// <param name="targetFrameworks">The target framework monikers.</param>
	/// <returns>The platform classification.</returns>
	public static ProjectPlatform ClassifyTargetFrameworks(IEnumerable<string> targetFrameworks)
	{
		Ensure.NotNull(targetFrameworks);

		bool anyNeutral = false;
		bool anyIos = false;
		bool anyWindows = false;

		foreach (string tfm in targetFrameworks)
		{
			if (string.IsNullOrWhiteSpace(tfm))
			{
				continue;
			}

			if (tfm.Contains("-ios", StringComparison.OrdinalIgnoreCase))
			{
				anyIos = true;
			}
			else if (tfm.Contains("-windows", StringComparison.OrdinalIgnoreCase))
			{
				anyWindows = true;
			}
			else if (!tfm.Contains('-'))
			{
				anyNeutral = true;
			}
		}

		if (anyNeutral)
		{
			return ProjectPlatform.Neutral;
		}

		if (anyIos && !anyWindows)
		{
			return ProjectPlatform.Ios;
		}

		if (anyWindows && !anyIos)
		{
			return ProjectPlatform.Windows;
		}

		return ProjectPlatform.Neutral;
	}

	/// <inheritdoc/>
	public bool IsExecutableProject(string projectPath)
	{
		Ensure.NotNull(projectPath);
		if (!File.Exists(projectPath))
		{
			return false;
		}

		string content = File.ReadAllText(projectPath);

		return OutputTypeExeRegex.IsMatch(content) ||
			   OutputTypeWinExeRegex.IsMatch(content) ||
			   SdkAppRegex.IsMatch(content) ||
			   SdkIosRegex.IsMatch(content);
	}

	private static IEnumerable<string> GetTargetFrameworks(string projectContent)
	{
		foreach (Match match in TargetFrameworkRegex.Matches(projectContent))
		{
			foreach (string tfm in match.Groups[1].Value.Split([';'], StringSplitOptions.RemoveEmptyEntries))
			{
				yield return tfm.Trim();
			}
		}
	}

	/// <inheritdoc/>
	public bool IsTestProject(string projectPath)
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

		// Check content for test SDK or IsTestProject
		string content = File.ReadAllText(projectPath);
		return content.Contains("<IsTestProject>true</IsTestProject>", StringComparison.OrdinalIgnoreCase) ||
			   content.Contains("Sdk=\"Microsoft.NET.Sdk.Test\"", StringComparison.OrdinalIgnoreCase) ||
			   SdkTestRegex.IsMatch(content);
	}

	private void CopyCoverageFile(string workingDirectory, string outputPath)
	{
		string[] coverageFiles = Directory.GetFiles(workingDirectory, "coverage.xml", SearchOption.AllDirectories);
		if (coverageFiles.Length > 0)
		{
			string latestFile = coverageFiles
				.Select(f => new FileInfo(f))
				.OrderByDescending(f => f.LastWriteTime)
				.First()
				.FullName;

			string targetPath = Path.Combine(outputPath, "coverage.xml");
			File.Copy(latestFile, targetPath, overwrite: true);
			logger.WriteInfo($"Coverage file copied to: {targetPath}");
		}
		else
		{
			logger.WriteWarning("No coverage file found");
		}
	}
}
