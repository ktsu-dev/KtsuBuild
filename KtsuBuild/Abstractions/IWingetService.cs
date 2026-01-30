// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Abstractions;

using KtsuBuild.Winget;

/// <summary>
/// Interface for Winget manifest operations.
/// </summary>
public interface IWingetService
{
	/// <summary>
	/// Generates Winget manifest files for a release.
	/// </summary>
	/// <param name="options">The options for manifest generation.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The generated manifest file paths.</returns>
	public Task<WingetManifestResult> GenerateManifestsAsync(WingetOptions options, CancellationToken cancellationToken = default);

	/// <summary>
	/// Uploads Winget manifests to a GitHub release.
	/// </summary>
	/// <param name="version">The release version.</param>
	/// <param name="manifestDirectory">The directory containing manifest files.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task UploadManifestsAsync(string version, string manifestDirectory, CancellationToken cancellationToken = default);
}
