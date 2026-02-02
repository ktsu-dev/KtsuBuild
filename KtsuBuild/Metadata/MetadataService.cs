// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Metadata;

using KtsuBuild.Abstractions;
using KtsuBuild.Git;
using KtsuBuild.Utilities;
using static Polyfill;

/// <summary>
/// Service for managing project metadata files.
/// </summary>
/// <param name="gitService">The Git service.</param>
/// <param name="logger">The build logger.</param>
public class MetadataService(IGitService gitService, IBuildLogger logger) : IMetadataService
{
	private readonly VersionCalculator _versionCalculator = new(gitService, logger);
	private readonly ChangelogGenerator _changelogGenerator = new(gitService, logger);

	/// <inheritdoc/>
	public async Task<MetadataUpdateResult> UpdateAllAsync(MetadataUpdateOptions options, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(options);
		Configuration.BuildConfiguration config = options.BuildConfiguration;

		try
		{
			string lineEnding = await gitService.GetLineEndingAsync(config.WorkspacePath, cancellationToken).ConfigureAwait(false);

			// Generate version
			logger.WriteInfo("Generating version information...");
			VersionInfo versionInfo = await _versionCalculator.GetVersionInfoAsync(config.WorkspacePath, config.ReleaseHash, cancellationToken: cancellationToken).ConfigureAwait(false);
			string version = versionInfo.Version;
			logger.WriteInfo($"Version: {version}");

			// Write version file
			await WriteVersionFileAsync(version, config.WorkspacePath, lineEnding, cancellationToken).ConfigureAwait(false);

			// Write license files
			logger.WriteInfo("Generating license...");
			await WriteLicenseFilesAsync(config.ServerUrl, config.GitHubOwner, config.GitHubRepo, config.WorkspacePath, lineEnding, cancellationToken).ConfigureAwait(false);

			// Write changelog files
			logger.WriteInfo("Generating changelog...");
			await WriteChangelogFilesAsync(version, config.ReleaseHash, config.WorkspacePath, config.WorkspacePath, lineEnding, config.LatestChangelogFile, cancellationToken).ConfigureAwait(false);

			// Write URL files
			await WriteUrlFilesAsync(config.ServerUrl, config.GitHubOwner, config.GitHubRepo, config.WorkspacePath, lineEnding, cancellationToken).ConfigureAwait(false);

			// Stage and commit if requested
			string releaseHash = config.ReleaseHash;
			bool hasChanges = false;

			if (options.CommitChanges)
			{
				List<string> filesToAdd =
				[
					"VERSION.md",
					"LICENSE.md",
					"AUTHORS.md",
					"CHANGELOG.md",
					"COPYRIGHT.md",
					"PROJECT_URL.url",
					"AUTHORS.url",
				];

				if (File.Exists(Path.Combine(config.WorkspacePath, config.LatestChangelogFile)))
				{
					filesToAdd.Add(config.LatestChangelogFile);
				}

				logger.WriteInfo($"Adding files to git: {string.Join(", ", filesToAdd)}");
				await gitService.StageFilesAsync(config.WorkspacePath, filesToAdd, cancellationToken).ConfigureAwait(false);

				hasChanges = await gitService.HasUncommittedChangesAsync(config.WorkspacePath, cancellationToken).ConfigureAwait(false);

				if (hasChanges)
				{
					await gitService.SetIdentityAsync(config.WorkspacePath, "Github Actions", "actions@users.noreply.github.com", cancellationToken).ConfigureAwait(false);
					logger.WriteInfo("Committing changes...");
					releaseHash = await gitService.CommitAsync(config.WorkspacePath, options.CommitMessage, cancellationToken).ConfigureAwait(false);
					logger.WriteInfo("Pushing changes...");
					await gitService.PushAsync(config.WorkspacePath, cancellationToken).ConfigureAwait(false);
					logger.WriteInfo($"Metadata committed as {releaseHash}");
				}
				else
				{
					logger.WriteInfo("No changes to commit");
					releaseHash = await gitService.GetCurrentCommitHashAsync(config.WorkspacePath, cancellationToken).ConfigureAwait(false);
				}
			}

			return new MetadataUpdateResult
			{
				Success = true,
				Version = version,
				ReleaseHash = releaseHash,
				HasChanges = hasChanges,
			};
		}
#pragma warning disable CA1031 // This is a top-level operation that returns a result object; catching all exceptions is intentional
		catch (Exception ex)
#pragma warning restore CA1031
		{
			logger.WriteError($"Failed to update metadata: {ex.Message}");
			return new MetadataUpdateResult
			{
				Success = false,
				Error = ex.Message,
			};
		}
	}

	/// <inheritdoc/>
	public async Task WriteVersionFileAsync(string version, string outputPath, string lineEnding, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(version);
		Ensure.NotNull(outputPath);
		Ensure.NotNull(lineEnding);
		await VersionFileWriter.WriteAsync(version, outputPath, lineEnding, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task WriteLicenseFilesAsync(string serverUrl, string owner, string repository, string outputPath, string lineEnding, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(serverUrl);
		Ensure.NotNull(owner);
		Ensure.NotNull(repository);
		Ensure.NotNull(outputPath);
		Ensure.NotNull(lineEnding);
		await LicenseGenerator.GenerateAsync(serverUrl, owner, repository, outputPath, lineEnding, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task WriteChangelogFilesAsync(
		string version,
		string commitHash,
		string workingDirectory,
		string outputPath,
		string lineEnding,
		string latestChangelogFileName = "LATEST_CHANGELOG.md",
		CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(version);
		Ensure.NotNull(commitHash);
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(outputPath);
		Ensure.NotNull(lineEnding);
		await _changelogGenerator.GenerateAsync(version, commitHash, workingDirectory, outputPath, lineEnding, latestChangelogFileName, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task WriteUrlFilesAsync(string serverUrl, string owner, string repository, string outputPath, string lineEnding, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(serverUrl);
		Ensure.NotNull(owner);
		Ensure.NotNull(repository);
		Ensure.NotNull(outputPath);
		Ensure.NotNull(lineEnding);

		// AUTHORS.url
		string authorsUrl = $"[InternetShortcut]{lineEnding}URL={serverUrl}/{owner}";
		string authorsPath = Path.Combine(outputPath, "AUTHORS.url");
		await LineEndingHelper.WriteFileAsync(authorsPath, authorsUrl, lineEnding, cancellationToken).ConfigureAwait(false);

		// PROJECT_URL.url
		string projectUrl = $"[InternetShortcut]{lineEnding}URL={serverUrl}/{repository}";
		string projectPath = Path.Combine(outputPath, "PROJECT_URL.url");
		await LineEndingHelper.WriteFileAsync(projectPath, projectUrl, lineEnding, cancellationToken).ConfigureAwait(false);
	}
}
