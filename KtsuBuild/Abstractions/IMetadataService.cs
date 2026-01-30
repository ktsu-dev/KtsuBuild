// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Abstractions;

using KtsuBuild.Metadata;

/// <summary>
/// Interface for project metadata generation and management.
/// </summary>
public interface IMetadataService
{
	/// <summary>
	/// Updates all project metadata files.
	/// </summary>
	/// <param name="options">The metadata update options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The result of the metadata update operation.</returns>
	public Task<MetadataUpdateResult> UpdateAllAsync(MetadataUpdateOptions options, CancellationToken cancellationToken = default);

	/// <summary>
	/// Generates the VERSION.md file.
	/// </summary>
	/// <param name="version">The version string.</param>
	/// <param name="outputPath">The output directory.</param>
	/// <param name="lineEnding">The line ending to use.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task WriteVersionFileAsync(string version, string outputPath, string lineEnding, CancellationToken cancellationToken = default);

	/// <summary>
	/// Generates the LICENSE.md and COPYRIGHT.md files.
	/// </summary>
	/// <param name="serverUrl">The GitHub server URL.</param>
	/// <param name="owner">The repository owner.</param>
	/// <param name="repository">The repository name.</param>
	/// <param name="outputPath">The output directory.</param>
	/// <param name="lineEnding">The line ending to use.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task WriteLicenseFilesAsync(string serverUrl, string owner, string repository, string outputPath, string lineEnding, CancellationToken cancellationToken = default);

	/// <summary>
	/// Generates the CHANGELOG.md and LATEST_CHANGELOG.md files.
	/// </summary>
	/// <param name="version">The current version.</param>
	/// <param name="commitHash">The current commit hash.</param>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="outputPath">The output directory.</param>
	/// <param name="lineEnding">The line ending to use.</param>
	/// <param name="latestChangelogFileName">The filename for the latest changelog.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task WriteChangelogFilesAsync(
		string version,
		string commitHash,
		string workingDirectory,
		string outputPath,
		string lineEnding,
		string latestChangelogFileName = "LATEST_CHANGELOG.md",
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Generates the AUTHORS.url and PROJECT_URL.url files.
	/// </summary>
	/// <param name="serverUrl">The GitHub server URL.</param>
	/// <param name="owner">The repository owner.</param>
	/// <param name="repository">The repository name.</param>
	/// <param name="outputPath">The output directory.</param>
	/// <param name="lineEnding">The line ending to use.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task WriteUrlFilesAsync(string serverUrl, string owner, string repository, string outputPath, string lineEnding, CancellationToken cancellationToken = default);
}
