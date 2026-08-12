// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Publishing;

using KtsuBuild.Abstractions;
using KtsuBuild.Configuration;
using KtsuBuild.DotNet;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Service that executes the release workflow: pack, publish applications, publish NuGet, create GitHub release.
/// </summary>
/// <param name="dotNetService">The .NET SDK service.</param>
/// <param name="nuGetPublisher">The NuGet publisher.</param>
/// <param name="gitHubService">The GitHub service.</param>
/// <param name="logger">The build logger.</param>
public class ReleaseService(IDotNetService dotNetService, INuGetPublisher nuGetPublisher, IGitHubService gitHubService, IBuildLogger logger) : IReleaseService
{
	/// <summary>
	/// The runtimes every executable project is published for.
	/// </summary>
	private static readonly string[] PublishRuntimes = ["win-x64", "win-x86", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"];

	/// <inheritdoc/>
	public async Task ExecuteReleaseAsync(BuildConfiguration config, string workspace, string configuration, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(config);
		Ensure.NotNull(workspace);
		Ensure.NotNull(configuration);

		// Pack NuGet packages
		await dotNetService.PackAsync(workspace, config.StagingPath, configuration, config.LatestChangelogFile, cancellationToken).ConfigureAwait(false);

		await PublishApplicationsAsync(config, workspace, configuration, cancellationToken).ConfigureAwait(false);
		await WriteArchiveHashesAsync(config, cancellationToken).ConfigureAwait(false);
		await PublishPackagesAsync(config, cancellationToken).ConfigureAwait(false);
		await CreateGitHubReleaseAsync(config, workspace, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Publishes every executable project for each supported runtime and archives the output.
	/// </summary>
	private async Task PublishApplicationsAsync(BuildConfiguration config, string workspace, string configuration, CancellationToken cancellationToken)
	{
		IReadOnlyList<string> projectFiles = dotNetService.GetProjectFiles(workspace);
		foreach (string project in projectFiles.Where(dotNetService.IsExecutableProject))
		{
			string projectName = Path.GetFileNameWithoutExtension(project);

			foreach (string runtime in PublishRuntimes)
			{
				string outputDir = Path.Combine(config.OutputPath, $"{projectName}-{runtime}");
				PublishOptions publishOptions = new()
				{
					WorkingDirectory = workspace,
					ProjectPath = project,
					OutputPath = outputDir,
					Runtime = runtime,
					Configuration = configuration,
				};

				await dotNetService.PublishAsync(publishOptions, cancellationToken).ConfigureAwait(false);
				await CreateArchiveAsync(config, projectName, runtime, outputDir, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Creates the zip archive for a single published runtime folder, replacing any existing archive.
	/// </summary>
	private async Task CreateArchiveAsync(BuildConfiguration config, string projectName, string runtime, string outputDir, CancellationToken cancellationToken)
	{
		if (!Directory.Exists(outputDir))
		{
			return;
		}

		string zipPath = Path.Combine(config.StagingPath, $"{projectName}-{config.Version}-{runtime}.zip");
		if (File.Exists(zipPath))
		{
			File.Delete(zipPath);
		}

		await System.IO.Compression.ZipFile.CreateFromDirectoryAsync(outputDir, zipPath, cancellationToken).ConfigureAwait(false);
		logger.WriteInfo($"Created: {zipPath}");
	}

	/// <summary>
	/// Writes a SHA256 hash entry for every zip archive in the staging directory.
	/// </summary>
	private async Task WriteArchiveHashesAsync(BuildConfiguration config, CancellationToken cancellationToken)
	{
		string[] zipFiles = Directory.Exists(config.StagingPath)
			? Directory.GetFiles(config.StagingPath, "*.zip")
			: [];

		if (zipFiles.Length == 0)
		{
			return;
		}

		string hashesPath = Path.Combine(config.StagingPath, "hashes.txt");
		List<string> hashEntries = [];
		foreach (string zipFile in zipFiles)
		{
			byte[] fileBytes = await File.ReadAllBytesAsync(zipFile, cancellationToken).ConfigureAwait(false);
			byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(fileBytes);
			string hash = Convert.ToHexString(hashBytes);
			string fileName = Path.GetFileName(zipFile);
			hashEntries.Add($"{fileName}={hash}");
			logger.WriteInfo($"SHA256: {fileName} = {hash}");
		}

		await File.WriteAllLinesAsync(hashesPath, hashEntries, cancellationToken).ConfigureAwait(false);
		logger.WriteInfo($"Hashes written to: {hashesPath}");
	}

	/// <summary>
	/// Publishes the packed NuGet packages to every feed that has credentials configured.
	/// </summary>
	private async Task PublishPackagesAsync(BuildConfiguration config, CancellationToken cancellationToken)
	{
		string[] packages = Directory.Exists(config.StagingPath)
			? Directory.GetFiles(config.StagingPath, "*.nupkg")
			: [];

		if (packages.Length == 0 || string.IsNullOrEmpty(config.GithubToken))
		{
			return;
		}

		await nuGetPublisher.PublishToGitHubAsync(config.PackagePattern, config.GitHubOwner, config.GithubToken, cancellationToken).ConfigureAwait(false);

		if (!string.IsNullOrEmpty(config.NuGetApiKey))
		{
			await nuGetPublisher.PublishToNuGetOrgAsync(config.PackagePattern, config.NuGetApiKey, cancellationToken).ConfigureAwait(false);
		}

		if (!string.IsNullOrEmpty(config.KtsuPackageKey))
		{
			await nuGetPublisher.PublishToSourceAsync(config.PackagePattern, "https://packages.ktsu.dev/v3/index.json", config.KtsuPackageKey, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Creates the GitHub release for the packed and published artifacts.
	/// </summary>
	private async Task CreateGitHubReleaseAsync(BuildConfiguration config, string workspace, CancellationToken cancellationToken)
	{
		ReleaseOptions releaseOptions = new()
		{
			Version = config.Version,
			CommitHash = config.ReleaseHash,
			GithubToken = config.GithubToken,
			ChangelogFile = config.ChangelogFile,
			LatestChangelogFile = config.LatestChangelogFile,
			AssetPaths = config.AssetPatterns,
			IsPrerelease = config.Version.Contains("-pre", StringComparison.OrdinalIgnoreCase)
				|| config.Version.Contains("-alpha", StringComparison.OrdinalIgnoreCase)
				|| config.Version.Contains("-beta", StringComparison.OrdinalIgnoreCase),
			WorkingDirectory = workspace,
		};

		await gitHubService.CreateReleaseAsync(releaseOptions, cancellationToken).ConfigureAwait(false);
	}
}
