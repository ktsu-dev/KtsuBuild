// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Publishing;

using System.Text.Json;
using KtsuBuild.Abstractions;
using static Polyfill;

/// <summary>
/// Implementation of GitHub operations using the gh CLI.
/// </summary>
/// <param name="processRunner">The process runner.</param>
/// <param name="gitService">The Git service.</param>
/// <param name="logger">The build logger.</param>
public class GitHubService(IProcessRunner processRunner, IGitService gitService, IBuildLogger logger) : IGitHubService
{
	/// <inheritdoc/>
	public async Task CreateReleaseAsync(ReleaseOptions options, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(options);
		logger.WriteStepHeader($"Creating GitHub Release v{options.Version}");

		// Set GitHub token
		Environment.SetEnvironmentVariable("GH_TOKEN", options.GithubToken);

		// Configure git identity
		await gitService.SetIdentityAsync(options.WorkingDirectory, "Github Actions", "actions@users.noreply.github.com", cancellationToken).ConfigureAwait(false);

		// Create and push tag
		string tagName = $"v{options.Version}";
		logger.WriteInfo($"Creating and pushing tag {tagName}...");
		await gitService.CreateAndPushTagAsync(
			options.WorkingDirectory,
			tagName,
			options.CommitHash,
			$"Release {tagName}",
			cancellationToken).ConfigureAwait(false);

		// Build release command
		var args = new List<string>
		{
			"release",
			"create",
			tagName,
			"--target",
			options.CommitHash,
		};

		if (options.GenerateNotes)
		{
			args.Add("--generate-notes");
		}

		// Add release notes file
		string? notesFile = null;
		if (!string.IsNullOrEmpty(options.LatestChangelogFile) && File.Exists(options.LatestChangelogFile))
		{
			notesFile = options.LatestChangelogFile;
			logger.WriteInfo($"Using latest version changelog from {notesFile}");
		}
		else if (!string.IsNullOrEmpty(options.ChangelogFile) && File.Exists(options.ChangelogFile))
		{
			notesFile = options.ChangelogFile;
			logger.WriteInfo($"Using full changelog from {notesFile}");
		}

		if (notesFile is not null)
		{
			args.Add("--notes-file");
			args.Add(notesFile);
		}

		if (options.IsPrerelease)
		{
			args.Add("--prerelease");
		}

		// Add asset files
		foreach (string assetPath in options.AssetPaths)
		{
			if (File.Exists(assetPath))
			{
				args.Add(assetPath);
			}
			else
			{
				// Handle glob patterns
				string? directory = Path.GetDirectoryName(assetPath);
				string pattern = Path.GetFileName(assetPath);
				if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
				{
					foreach (string file in Directory.GetFiles(directory, pattern))
					{
						args.Add(file);
					}
				}
			}
		}

		string argsString = string.Join(' ', args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));

		int exitCode = await processRunner.RunWithCallbackAsync(
			"gh",
			argsString,
			options.WorkingDirectory,
			line => logger.WriteInfo(line),
			line => logger.WriteError(line),
			cancellationToken).ConfigureAwait(false);

		if (exitCode != 0)
		{
			throw new InvalidOperationException($"Failed to create GitHub release with exit code {exitCode}");
		}
	}

	/// <inheritdoc/>
	public async Task UploadReleaseAssetsAsync(string version, IEnumerable<string> assetPaths, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(version);
		Ensure.NotNull(assetPaths);
		string tagName = $"v{version}";
		var assets = assetPaths.ToList();

		if (assets.Count == 0)
		{
			logger.WriteInfo("No assets to upload");
			return;
		}

		logger.WriteInfo($"Uploading {assets.Count} assets to release {tagName}");

		foreach (string assetPath in assets)
		{
			if (!File.Exists(assetPath))
			{
				logger.WriteWarning($"Asset file not found: {assetPath}");
				continue;
			}

			string args = $"release upload {tagName} \"{assetPath}\"";

			int exitCode = await processRunner.RunWithCallbackAsync(
				"gh",
				args,
				null,
				line => logger.WriteInfo(line),
				line => logger.WriteError(line),
				cancellationToken).ConfigureAwait(false);

			if (exitCode != 0)
			{
				logger.WriteWarning($"Failed to upload asset: {assetPath}");
			}
		}
	}

	/// <inheritdoc/>
	public async Task<RepositoryInfo?> GetRepositoryInfoAsync(string workingDirectory, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		var result = await processRunner.RunAsync("gh", "repo view --json owner,nameWithOwner,isFork", workingDirectory, cancellationToken).ConfigureAwait(false);

		if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
		{
			return null;
		}

		try
		{
			using var doc = JsonDocument.Parse(result.StandardOutput);
			var root = doc.RootElement;

			string owner = root.GetProperty("owner").GetProperty("login").GetString() ?? string.Empty;
			string nameWithOwner = root.GetProperty("nameWithOwner").GetString() ?? string.Empty;
			bool isFork = root.GetProperty("isFork").GetBoolean();

			string name = nameWithOwner.Contains('/')
				? nameWithOwner.Split('/')[1]
				: nameWithOwner;

			return new RepositoryInfo(owner, name, isFork);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	/// <inheritdoc/>
	public async Task<bool> IsOfficialRepositoryAsync(string workingDirectory, string expectedOwner, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(expectedOwner);
		var repoInfo = await GetRepositoryInfoAsync(workingDirectory, cancellationToken).ConfigureAwait(false);

		if (repoInfo is null)
		{
			logger.WriteInfo("Could not retrieve repository information. Assuming unofficial build.");
			return false;
		}

		logger.WriteInfo($"Repository: {repoInfo.Owner}/{repoInfo.Name}, Is Fork: {repoInfo.IsFork}");

		bool isOfficial = !repoInfo.IsFork && string.Equals(repoInfo.Owner, expectedOwner, StringComparison.OrdinalIgnoreCase);
		logger.WriteInfo($"Is Official: {isOfficial}");

		return isOfficial;
	}
}
