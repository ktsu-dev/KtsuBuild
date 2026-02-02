// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.DotNet;

using System.Text.RegularExpressions;
using KtsuBuild.Abstractions;
using static Polyfill;

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
	private static readonly Regex SdkTestRegex = new(@"Sdk=""[^""]*\.Test[/""]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
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
				throw new InvalidOperationException($"Build failed with exit code {exitCode}");
			}
		}
	}

	/// <inheritdoc/>
	public async Task TestAsync(string workingDirectory, string configuration = "Release", string? coverageOutputPath = null, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		logger.WriteStepHeader("Running Tests with Coverage");

		// Check for test projects
		List<string> testProjects = [.. GetProjectFiles(workingDirectory).Where(IsTestProject)];
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

		IReadOnlyList<string> projectFiles = GetProjectFiles(workingDirectory);
		if (projectFiles.Count == 0)
		{
			logger.WriteInfo("No .NET library projects found to package");
			return;
		}

		string args = $"pack --configuration {configuration} {QuietLogger} --no-build --output \"{outputPath}\"";

		if (!string.IsNullOrEmpty(releaseNotesFile) && File.Exists(releaseNotesFile))
		{
			string absolutePath = Path.GetFullPath(releaseNotesFile);
			logger.WriteInfo($"Using release notes from file: {absolutePath}");
			args += $" -p:PackageReleaseNotesFile=\"{absolutePath}\"";
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
			throw new InvalidOperationException($"Pack failed with exit code {exitCode}");
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
		bool singleFile = true,
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
			$"-p:PublishTrimmed=false -p:EnableCompressionInSingleFile=true " +
			$"-p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false {QuietLogger}";

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
	public IReadOnlyList<string> GetProjectFiles(string workingDirectory)
	{
		Ensure.NotNull(workingDirectory);
		return [.. Directory.GetFiles(workingDirectory, "*.csproj", SearchOption.AllDirectories)];
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
			   SdkAppRegex.IsMatch(content);
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
