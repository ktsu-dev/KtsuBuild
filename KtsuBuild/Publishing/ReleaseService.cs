// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Publishing;

using KtsuBuild.Abstractions;
using KtsuBuild.Configuration;
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
	/// <inheritdoc/>
	public async Task ExecuteReleaseAsync(BuildConfiguration config, string workspace, string configuration, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(config);
		Ensure.NotNull(workspace);
		Ensure.NotNull(configuration);

		// Pack NuGet packages
		await dotNetService.PackAsync(workspace, config.StagingPath, configuration, config.LatestChangelogFile, cancellationToken).ConfigureAwait(false);

		// Publish executable applications
		IReadOnlyList<string> projectFiles = dotNetService.GetProjectFiles(workspace);
		foreach (string project in projectFiles.Where(dotNetService.IsExecutableProject))
		{
			string projectName = Path.GetFileNameWithoutExtension(project);
			string[] runtimes = ["win-x64", "win-x86", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"];

			foreach (string runtime in runtimes)
			{
				string outputDir = Path.Combine(config.OutputPath, $"{projectName}-{runtime}");
				await dotNetService.PublishAsync(workspace, project, outputDir, runtime, configuration, cancellationToken: cancellationToken).ConfigureAwait(false);

				// Create zip archive
				string zipPath = Path.Combine(config.StagingPath, $"{projectName}-{config.Version}-{runtime}.zip");
				if (Directory.Exists(outputDir))
				{
					if (File.Exists(zipPath))
					{
						File.Delete(zipPath);
					}

					await System.IO.Compression.ZipFile.CreateFromDirectoryAsync(outputDir, zipPath, cancellationToken).ConfigureAwait(false);
					logger.WriteInfo($"Created: {zipPath}");
				}
			}
		}

		// Generate SHA256 hashes for all zip archives
		string[] zipFiles = Directory.Exists(config.StagingPath)
			? Directory.GetFiles(config.StagingPath, "*.zip")
			: [];

		if (zipFiles.Length > 0)
		{
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

		// Publish NuGet packages
		string[] packages = Directory.Exists(config.StagingPath)
			? Directory.GetFiles(config.StagingPath, "*.nupkg")
			: [];

		if (packages.Length > 0 && !string.IsNullOrEmpty(config.GithubToken))
		{
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

		// Create GitHub release
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
