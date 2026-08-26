// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.DotNet;

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using KtsuBuild.Abstractions;
using KtsuBuild.Utilities;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Implementation of .NET SDK operations.
/// </summary>
/// <param name="processRunner">The process runner.</param>
/// <param name="logger">The build logger.</param>
/// <param name="toolsDirectory">
/// Where helper tools such as <c>dotnet-coverage</c> are installed. Defaults to a directory under
/// the system temp path, which survives between runs so an agent installs each tool once.
/// </param>
public class DotNetService(IProcessRunner processRunner, IBuildLogger logger, string? toolsDirectory = null) : IDotNetService
{
	/// <summary>
	/// The .NET CLI executable name.
	/// </summary>
	private const string DotNetCli = "dotnet";

	/// <summary>
	/// The build configuration used when the caller does not specify one.
	/// </summary>
	private const string DefaultConfiguration = "Release";

	private const string QuietLogger = "-logger:\"Microsoft.Build.Logging.ConsoleLogger,Microsoft.Build;Summary;ForceNoAlign;ShowTimestamp;ShowCommandLine;Verbosity=quiet\"";

#pragma warning disable SYSLIB1045 // GeneratedRegex not available in netstandard2.0/2.1
	private static readonly Regex OutputTypeExeRegex = new(@"<OutputType>\s*Exe\s*</OutputType>", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexDefaults.MatchTimeout);
	private static readonly Regex OutputTypeWinExeRegex = new(@"<OutputType>\s*WinExe\s*</OutputType>", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexDefaults.MatchTimeout);
	private static readonly Regex SdkAppRegex = new(@"Sdk=""[^""]*\.App[/""]", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexDefaults.MatchTimeout);
	private static readonly Regex SdkIosRegex = new(@"Sdk=""[^""]*\.Ios[/""]", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexDefaults.MatchTimeout);
	private static readonly Regex SdkTestRegex = new(@"Sdk=""[^""]*\.Test[/""]", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexDefaults.MatchTimeout);
	private static readonly Regex TargetFrameworkRegex = new(@"<TargetFrameworks?>\s*([^<]+?)\s*</TargetFrameworks?>", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexDefaults.MatchTimeout);
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
			DotNetCli,
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
	public async Task BuildAsync(string workingDirectory, string configuration = DefaultConfiguration, string? additionalArgs = null, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		logger.WriteStepHeader("Building Solution");

		string args = $"build --configuration {configuration} {QuietLogger} --no-incremental --no-restore";
		if (!string.IsNullOrEmpty(additionalArgs))
		{
			args += $" {additionalArgs}";
		}

		int exitCode = await processRunner.RunWithCallbackAsync(
			DotNetCli,
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
				DotNetCli,
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
	public async Task TestAsync(string workingDirectory, string configuration = DefaultConfiguration, string? coverageOutputPath = null, bool hostRuntimeOnly = false, string? solutionFilter = null, CancellationToken cancellationToken = default)
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

		await RunTestsAsync(target: string.Empty, workingDirectory, configuration, coverageOutputPath, noBuild: false, runtimeIdentifierPin: false, ktsuHostRuntimeOnly: hostRuntimeOnly, solutionFilter, cancellationToken).ConfigureAwait(false);
	}

	// Shared by TestAsync, which tests everything the host can build in one invocation, and
	// TestProjectAsync, which tests one project. `target` is the project path, or empty to let
	// `dotnet test` discover. The two runtime options are deliberately separate and mutually
	// exclusive in practice: `runtimeIdentifierPin` is only ever true from TestProjectAsync, where a
	// single project makes a global `RuntimeIdentifier` property legal. `ktsuHostRuntimeOnly` is only
	// ever true from TestAsync, where the run spans the whole workspace and a global
	// `RuntimeIdentifier` would fail with NETSDK1134, so pinning goes through the Sdk's opt-in
	// property instead. See the property's own comment below for why the two cannot be swapped.
	private async Task RunTestsAsync(string target, string workingDirectory, string configuration, string? coverageOutputPath, bool noBuild, bool runtimeIdentifierPin, bool ktsuHostRuntimeOnly, string? solutionFilter, CancellationToken cancellationToken)
	{
		string resultsPath = ResolveAgainst(workingDirectory, coverageOutputPath ?? "coverage");
		string testResultsPath = Path.Combine(resultsPath, "TestResults");

		// Reports left behind by an earlier run would be merged into this one's, so the run starts
		// from an empty directory. Everything here is regenerated by the run that follows.
		if (Directory.Exists(testResultsPath))
		{
			Directory.Delete(testResultsPath, recursive: true);
		}

		Directory.CreateDirectory(testResultsPath);

		// `--project`, not a positional path. `dotnet test` silently ignores a positional path it
		// cannot resolve and falls back to testing the current directory, so a mistyped or
		// unresolved path runs the whole solution and reports "total: 0" with an error per
		// assembly, which reads as a test failure rather than as a bad argument. `--project`
		// fails loudly instead. An empty target is the whole-workspace case and passes neither.
		// A solution filter narrows a workspace-wide run to a subset of projects while keeping it a
		// single invocation. `--solution` is used rather than a positional path for the same reason
		// `--project` is: dotnet test silently ignores a positional path it cannot resolve and falls
		// back to the current directory, which reads as a test failure rather than a bad argument.
		string scope = !string.IsNullOrEmpty(target)
			? $"--project \"{target}\" "
			: !string.IsNullOrEmpty(solutionFilter)
				? $"--solution \"{solutionFilter}\" "
				: string.Empty;
		// No --coverage-output and no --report-trx-filename. One `dotnet test` invocation runs every
		// test project in the workspace and hands each the same arguments, so a fixed filename made
		// every project write over the one before it. What survived was one project's coverage and
		// one project's test results, presented as the whole repository's. Left to name its own
		// file, each project writes its own report, and the coverage reports are merged afterwards.
		string args = $"test {scope}--configuration {configuration} --coverage --coverage-output-format xml " +
			$"--results-directory \"{testResultsPath}\" --report-trx";
		if (noBuild)
		{
			args += " --no-build";
		}

		// A test run needs only the host's native assets. Setting the runtime identifier alone would
		// make the build self-contained and copy the whole framework into the output, so the two
		// properties are always set together, never one without the other.
		if (runtimeIdentifierPin)
		{
			args += $" -p:RuntimeIdentifier={RuntimeInformation.RuntimeIdentifier} -p:SelfContained=false";
		}

		// Do not pass -p:RuntimeIdentifier here. This branch runs across the whole workspace, and
		// MSBuild rejects a global RuntimeIdentifier on a solution build with NETSDK1134 ("Building a
		// solution with a specific RuntimeIdentifier is not supported"). KtsuHostRuntimeOnly is the
		// property ktsu.Sdk added instead: it gives each project its own runtime identifier, which is
		// legal, rather than passing one identifier as a global property to every project at once,
		// which is not. This is the whole reason test all stopped looping over TestProjectAsync one
		// project at a time (that per-project loop paid for the pin with fourteen test host startups
		// and still ran slower than an unpinned single invocation).
		if (ktsuHostRuntimeOnly)
		{
			args += " -p:KtsuHostRuntimeOnly=true";
		}

		// The Microsoft.CodeCoverage collector intermittently drops its instrumentation IPC pipe during
		// teardown when several test assemblies run, which Microsoft.Testing.Platform surfaces as exit
		// code 7 ("error: 1") even though every test passed. Genuine test failures surface as exit code 2
		// (with a non-zero failed count), so retrying exit code 7 — and only exit code 7 — recovers this
		// infrastructure flake without ever masking a real test failure.
		const int coverageFlakeExitCode = 7;
		const int maxAttempts = 3;
		int exitCode = 0;
		for (int attempt = 1; attempt <= maxAttempts; attempt++)
		{
			exitCode = await processRunner.RunWithCallbackAsync(
				DotNetCli,
				args,
				workingDirectory,
				logger.WriteInfo,
				logger.WriteError,
				cancellationToken).ConfigureAwait(false);
			if (exitCode != coverageFlakeExitCode)
			{
				break;
			}

			if (attempt < maxAttempts)
			{
				logger.WriteWarning($"Test run exited with code {coverageFlakeExitCode} (known code-coverage " +
					$"collector pipe flake — all tests passed). Retrying ({attempt}/{maxAttempts - 1})...");
			}
		}

		if (exitCode != 0)
		{
			throw new InvalidOperationException($"Tests failed with exit code {exitCode}");
		}

		await CollectCoverageAsync(workingDirectory, resultsPath, testResultsPath, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task TestProjectAsync(string projectPath, string workingDirectory, string configuration = DefaultConfiguration, string? coverageOutputPath = null, bool noBuild = false, bool hostRuntimeOnly = false, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(projectPath);
		Ensure.NotNull(workingDirectory);
		if (string.IsNullOrWhiteSpace(projectPath))
		{
			throw new ArgumentException("A project path is required. An empty value would run every test project in the workspace.", nameof(projectPath));
		}

		logger.WriteStepHeader($"Running Tests with Coverage: {Path.GetFileNameWithoutExtension(projectPath)}");

		await RunTestsAsync(projectPath, workingDirectory, configuration, coverageOutputPath, noBuild, runtimeIdentifierPin: hostRuntimeOnly, ktsuHostRuntimeOnly: false, solutionFilter: null, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task PackAsync(string workingDirectory, string outputPath, string configuration = DefaultConfiguration, string? releaseNotesFile = null, CancellationToken cancellationToken = default)
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

		// Packing each project individually (not via the solution) means MSBuild does not populate
		// $(SolutionDir)/$(SolutionName). ktsu.Sdk derives package metadata (license, readme, version,
		// PackageId) from the solution directory, so without it pack fails NU5030 ("LICENSE.md does
		// not exist in the package") and falls back to a wrong directory for nested projects.
		// Reconstruct the solution context from the workspace; discover both .slnx and .sln.
		string solutionDir = workingDirectory.TrimEnd('/', '\\') + "/";
		string solutionContextArgs = $" -p:SolutionDir=\"{solutionDir}\"";
		string? solutionFile = Directory.EnumerateFiles(workingDirectory, "*.slnx")
			.Concat(Directory.EnumerateFiles(workingDirectory, "*.sln"))
			.FirstOrDefault();
		if (solutionFile is not null)
		{
			solutionContextArgs += $" -p:SolutionName=\"{Path.GetFileNameWithoutExtension(solutionFile)}\"";
		}

		// Pack each non-test project individually
		foreach (string project in packableProjects)
		{
			string projectName = Path.GetFileNameWithoutExtension(project);
			logger.WriteInfo($"Packing {projectName}...");

			string args = $"pack \"{project}\" --configuration {configuration} {QuietLogger} --no-build --output \"{outputPath}\"{solutionContextArgs}{releaseNotesArg}";

			int exitCode = await processRunner.RunWithCallbackAsync(
				DotNetCli,
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
	public async Task PublishAsync(PublishOptions options, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(options);
		Directory.CreateDirectory(options.OutputPath);

		string args = $"publish \"{options.ProjectPath}\" --configuration {options.Configuration} --runtime {options.Runtime} " +
			$"--self-contained {options.SelfContained.ToString().ToLowerInvariant()} --output \"{options.OutputPath}\" " +
			$"-p:PublishSingleFile={options.SingleFile.ToString().ToLowerInvariant()} " +
			$"-p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false {QuietLogger}";

		int exitCode = await processRunner.RunWithCallbackAsync(
			DotNetCli,
			args,
			options.WorkingDirectory,
			logger.WriteInfo,
			logger.WriteError,
			cancellationToken).ConfigureAwait(false);

		if (exitCode != 0)
		{
			throw new InvalidOperationException($"Publish failed for {options.ProjectPath} ({options.Runtime}) with exit code {exitCode}");
		}
	}

	/// <inheritdoc/>
	public async Task BuildIosAsync(
		string workingDirectory,
		string projectPath,
		string runtimeIdentifier,
		string configuration = DefaultConfiguration,
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
			DotNetCli,
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
	public IReadOnlyList<TestProjectInfo> GetTestProjects(string workingDirectory)
	{
		Ensure.NotNull(workingDirectory);
		return [.. GetProjectFiles(workingDirectory)
			.Where(IsTestProject)
			.Select(p => new TestProjectInfo(p, GetProjectPlatform(p)))];
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

	/// <summary>
	/// Resolves a possibly relative path against the workspace rather than the process directory.
	/// </summary>
	/// <param name="workingDirectory">The workspace the run is scoped to.</param>
	/// <param name="path">An absolute path, or one relative to the workspace.</param>
	/// <returns>An absolute path.</returns>
	/// <remarks>
	/// <c>dotnet test</c> resolves <c>--results-directory</c> against its own working directory,
	/// which is the workspace. Resolving the same way here keeps the directory this code reads and
	/// the directory the test platform writes to the same one, even when the tool was invoked from
	/// somewhere other than the workspace.
	/// </remarks>
	private static string ResolveAgainst(string workingDirectory, string path)
		=> Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(workingDirectory, path));

	/// <summary>
	/// Produces one coverage report for the whole run, at <c>coverage.xml</c> in the results root.
	/// </summary>
	/// <param name="workingDirectory">The workspace the run is scoped to.</param>
	/// <param name="resultsRoot">The directory the combined report is written to.</param>
	/// <param name="testResultsRoot">The directory the test platform wrote its reports to.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <remarks>
	/// Each project's report lists every source file in the workspace and marks as covered only what
	/// that project executed, so any one report understates everything the others cover. Merging is
	/// what makes the number describe the repository instead of whichever project finished last.
	/// </remarks>
	private async Task CollectCoverageAsync(string workingDirectory, string resultsRoot, string testResultsRoot, CancellationToken cancellationToken)
	{
		// Only the top level. The test platform keeps a byte-identical copy of each report in a
		// nested attachment directory, and merging both copies would do the same work twice.
		string[] reports = Directory.Exists(testResultsRoot)
			? [.. Directory.GetFiles(testResultsRoot, "*.xml", SearchOption.TopDirectoryOnly)
				.Where(IsCoverageReport)
				.OrderBy(f => f, StringComparer.Ordinal)]
			: [];

		string targetPath = Path.Combine(resultsRoot, "coverage.xml");

		if (reports.Length == 0)
		{
			logger.WriteWarning("No coverage file found");
			return;
		}

		Directory.CreateDirectory(resultsRoot);

		if (reports.Length == 1)
		{
			File.Copy(reports[0], targetPath, overwrite: true);
			logger.WriteInfo($"Coverage file copied to: {targetPath}");
			return;
		}

		logger.WriteInfo($"Merging {reports.Length} coverage reports into {targetPath}...");
		string mergeTool = await EnsureCoverageMergeToolAsync(workingDirectory, cancellationToken).ConfigureAwait(false);

		string arguments = $"merge --nologo --output \"{targetPath}\" --output-format xml " +
			string.Join(" ", reports.Select(r => $"\"{r}\""));

		int exitCode = await processRunner.RunWithCallbackAsync(
			mergeTool,
			arguments,
			workingDirectory,
			logger.WriteVerbose,
			logger.WriteError,
			cancellationToken).ConfigureAwait(false);

		// A failed merge does not fall back to one project's report. That fallback is the defect this
		// replaced: it reported a number that looked plausible and was wrong.
		if (exitCode != 0)
		{
			throw new InvalidOperationException(
				$"Failed to merge {reports.Length} coverage reports. dotnet-coverage exited with code {exitCode}.");
		}

		logger.WriteInfo($"Coverage file written to: {targetPath}");
	}

	/// <summary>
	/// Decides whether a file is a coverage report rather than some other XML in the same directory.
	/// </summary>
	/// <param name="path">The file to inspect.</param>
	/// <returns>True when the root element a coverage report opens with appears in the first block.</returns>
	private static bool IsCoverageReport(string path)
	{
		try
		{
			using StreamReader reader = new(path);
			char[] buffer = new char[512];
			int read = reader.ReadBlock(buffer, 0, buffer.Length);
			return new string(buffer, 0, read).Contains("<results", StringComparison.Ordinal);
		}
		catch (IOException)
		{
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
	}

	/// <summary>
	/// Installs the coverage merge tool if it is not already present, and returns the path to it.
	/// </summary>
	/// <param name="workingDirectory">The workspace the run is scoped to.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The path to the <c>dotnet-coverage</c> executable.</returns>
	/// <remarks>
	/// Installed to a fixed tool path rather than globally, and invoked by that absolute path,
	/// because a tool installed into the global tools directory mid-run is not necessarily on the
	/// PATH this process inherited. The directory survives between runs, so an agent pays for the
	/// install once.
	/// </remarks>
	private async Task<string> EnsureCoverageMergeToolAsync(string workingDirectory, CancellationToken cancellationToken)
	{
		string toolRoot = toolsDirectory ?? Path.Combine(Path.GetTempPath(), "ktsubuild", "tools");
		string executable = Path.Combine(
			toolRoot,
			RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet-coverage.exe" : "dotnet-coverage");

		if (File.Exists(executable))
		{
			return executable;
		}

		logger.WriteInfo("Installing dotnet-coverage to merge the coverage reports...");
		Directory.CreateDirectory(toolRoot);

		int exitCode = await processRunner.RunWithCallbackAsync(
			DotNetCli,
			$"tool install dotnet-coverage --tool-path \"{toolRoot}\"",
			workingDirectory,
			logger.WriteVerbose,
			logger.WriteVerbose,
			cancellationToken).ConfigureAwait(false);

		// An install into a directory that already holds the tool reports failure, which is success
		// for this caller, so the file rather than the exit code decides.
		if (!File.Exists(executable))
		{
			throw new InvalidOperationException(
				$"Failed to install dotnet-coverage, which is needed to merge the coverage reports. The install exited with code {exitCode}.");
		}

		return executable;
	}
}
