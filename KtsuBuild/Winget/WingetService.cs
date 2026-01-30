// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Winget;

using System.Net.Http;
using System.Security.Cryptography;
using KtsuBuild.Abstractions;
using static Polyfill;

/// <summary>
/// Service for Winget manifest operations.
/// </summary>
/// <param name="processRunner">The process runner.</param>
/// <param name="logger">The build logger.</param>
public class WingetService(IProcessRunner processRunner, IBuildLogger logger) : IWingetService
{
	private readonly ProjectDetector _projectDetector = new();

	private static readonly string[] WindowsArchitectures = ["win-x64", "win-x86", "win-arm64"];

	/// <inheritdoc/>
	public async Task<WingetManifestResult> GenerateManifestsAsync(WingetOptions options, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(options);
		logger.WriteStepHeader("Generating Winget Manifests");

		// Detect project info
		var projectInfo = _projectDetector.Detect(options.RootDirectory);
		logger.WriteInfo($"Detected project: {projectInfo.Name} (Type: {projectInfo.Type})");

		// Check if library-only project
		if (_projectDetector.IsLibraryOnlyProject(options.RootDirectory, projectInfo))
		{
			logger.WriteWarning("Detected library project - no executable artifacts expected.");
			logger.WriteWarning("Skipping winget manifest generation as this appears to be a library/NuGet package.");
			return new WingetManifestResult
			{
				Success = true,
				IsLibraryOnly = true,
			};
		}

		// Resolve GitHub repo
		string? gitHubRepo = options.GitHubRepo ?? await DetectGitHubRepoAsync(options.RootDirectory, cancellationToken).ConfigureAwait(false);
		if (string.IsNullOrEmpty(gitHubRepo))
		{
			return new WingetManifestResult
			{
				Success = false,
				Error = "Could not detect GitHub repository. Please specify it using the GitHubRepo option.",
			};
		}

		string[] parts = gitHubRepo.Split('/');
		string owner = parts[0];
		string repoName = parts[1];

		// Build configuration
		string packageId = options.PackageId ?? $"{owner}.{repoName}";
		string artifactPattern = options.ArtifactNamePattern ?? $"{repoName}-{{version}}-{{arch}}.zip";
		string executableName = options.ExecutableName ?? projectInfo.ExecutableName;
		string commandAlias = options.CommandAlias ?? repoName.ToLowerInvariant();
		string packageName = projectInfo.Name.StartsWith($"{owner}.", StringComparison.OrdinalIgnoreCase)
			? projectInfo.Name[(owner.Length + 1)..]
			: projectInfo.Name;

		logger.WriteInfo($"Configuration:");
		logger.WriteInfo($"  Package ID: {packageId}");
		logger.WriteInfo($"  GitHub Repo: {gitHubRepo}");
		logger.WriteInfo($"  Artifact Pattern: {artifactPattern}");
		logger.WriteInfo($"  Executable: {executableName}");
		logger.WriteInfo($"  Command Alias: {commandAlias}");

		// Get SHA256 hashes
		var sha256Hashes = await GetHashesAsync(options, gitHubRepo, repoName, artifactPattern, cancellationToken).ConfigureAwait(false);

		if (sha256Hashes.Count == 0)
		{
			if (_projectDetector.IsLibraryOnlyProject(options.RootDirectory, projectInfo))
			{
				logger.WriteWarning("No hashes found, but this appears to be a library-only project.");
				return new WingetManifestResult
				{
					Success = true,
					IsLibraryOnly = true,
				};
			}

			return new WingetManifestResult
			{
				Success = false,
				Error = "Could not obtain any SHA256 hashes. Please check that the artifact name pattern matches your release files.",
			};
		}

		// Generate manifests
		var config = new ManifestConfig
		{
			PackageId = packageId,
			Version = options.Version,
			GitHubRepo = gitHubRepo,
			Owner = owner,
			RepoName = repoName,
			Publisher = projectInfo.Publisher.Length > 0 ? projectInfo.Publisher : owner,
			PackageName = packageName,
			ShortDescription = projectInfo.ShortDescription.Length > 0 ? projectInfo.ShortDescription : $"A {projectInfo.Type} application",
			ArtifactNamePattern = artifactPattern,
			ExecutableName = executableName,
			CommandAlias = commandAlias,
		};

		Directory.CreateDirectory(options.OutputDirectory);
		var manifestFiles = await ManifestGenerator.GenerateAsync(config, projectInfo, sha256Hashes, options.OutputDirectory, cancellationToken).ConfigureAwait(false);

		logger.WriteSuccess("Winget manifests generated successfully!");
		foreach (string file in manifestFiles)
		{
			logger.WriteInfo($"  - {file}");
		}

		return new WingetManifestResult
		{
			Success = true,
			ManifestFiles = manifestFiles,
			PackageId = packageId,
		};
	}

	/// <inheritdoc/>
	public async Task UploadManifestsAsync(string version, string manifestDirectory, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(version);
		Ensure.NotNull(manifestDirectory);
		string[] manifestFiles = Directory.GetFiles(manifestDirectory, "*.yaml");
		if (manifestFiles.Length == 0)
		{
			logger.WriteWarning("No manifest files found to upload");
			return;
		}

		logger.WriteInfo($"Uploading {manifestFiles.Length} manifest files to release v{version}...");

		string fileArgs = string.Join(" ", manifestFiles.Select(f => $"\"{f}\""));
		string args = $"release upload v{version} {fileArgs}";

		int exitCode = await processRunner.RunWithCallbackAsync(
			"gh",
			args,
			null,
			line => logger.WriteInfo(line),
			line => logger.WriteError(line),
			cancellationToken).ConfigureAwait(false);

		if (exitCode != 0)
		{
			logger.WriteWarning("Failed to upload manifest files to release");
		}
		else
		{
			logger.WriteSuccess("Manifest files uploaded to release");
		}
	}

	private async Task<string?> DetectGitHubRepoAsync(string rootDirectory, CancellationToken cancellationToken)
	{
		var result = await processRunner.RunAsync("git", "remote get-url origin", rootDirectory, cancellationToken).ConfigureAwait(false);
		if (!result.Success)
		{
			return null;
		}

		string url = result.StandardOutput.Trim();

		// Parse HTTPS or SSH URLs
		if (url.Contains("github.com"))
		{
			// SSH: git@github.com:owner/repo.git
			// HTTPS: https://github.com/owner/repo.git
			int startIndex = url.IndexOf("github.com", StringComparison.OrdinalIgnoreCase) + 11;
			if (url[startIndex] == ':')
			{
				startIndex++; // SSH format
			}
			else if (url[startIndex] == '/')
			{
				startIndex++; // HTTPS format
			}

			string ownerRepo = url[startIndex..].TrimEnd('/');
			if (ownerRepo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
			{
				ownerRepo = ownerRepo[..^4];
			}
			return ownerRepo;
		}

		return null;
	}

	private async Task<Dictionary<string, string>> GetHashesAsync(
		WingetOptions options,
		string gitHubRepo,
		string repoName,
		string artifactPattern,
		CancellationToken cancellationToken)
	{
		var hashes = new Dictionary<string, string>();

		// First try to read from local hashes file (from recent build)
		if (!string.IsNullOrEmpty(options.StagingDirectory))
		{
			string hashesFile = Path.Combine(options.StagingDirectory, "hashes.txt");
			if (File.Exists(hashesFile))
			{
				logger.WriteInfo("Reading hashes from local build output...");
				foreach (string line in await File.ReadAllLinesAsync(hashesFile, cancellationToken).ConfigureAwait(false))
				{
					string[] parts = line.Split('=');
					if (parts.Length == 2)
					{
						string fileName = parts[0];
						string hash = parts[1];

						// Match to architecture
						foreach (string arch in WindowsArchitectures)
						{
							string expectedName = artifactPattern
								.Replace("{name}", repoName)
								.Replace("{version}", options.Version)
								.Replace("{arch}", arch);

							if (fileName == expectedName)
							{
								hashes[arch] = hash.ToUpperInvariant();
								logger.WriteInfo($"  {arch}: {hash} (from local build)");
								break;
							}
						}
					}
				}

				if (hashes.Count > 0)
				{
					return hashes;
				}
			}
		}

		// Fall back to downloading from GitHub release
		string downloadBaseUrl = $"https://github.com/{gitHubRepo}/releases/download/v{options.Version}";

		using var httpClient = new HttpClient();
		httpClient.DefaultRequestHeaders.Add("User-Agent", "KtsuBuild-WingetService");

		foreach (string arch in WindowsArchitectures)
		{
			string fileName = artifactPattern
				.Replace("{name}", repoName)
				.Replace("{version}", options.Version)
				.Replace("{arch}", arch);

			string downloadUrl = $"{downloadBaseUrl}/{fileName}";

			try
			{
				logger.WriteInfo($"Downloading {fileName} to calculate SHA256...");
				byte[] fileBytes = await httpClient.GetByteArrayAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
				byte[] hashBytes = SHA256.HashData(fileBytes);
				string hash = Convert.ToHexString(hashBytes);
				hashes[arch] = hash;
				logger.WriteInfo($"  {arch}: {hash}");
			}
			catch (HttpRequestException ex)
			{
				logger.WriteWarning($"Failed to download {fileName}: {ex.Message}");
			}
		}

		return hashes;
	}
}
