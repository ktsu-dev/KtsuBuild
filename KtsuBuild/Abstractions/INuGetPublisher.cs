// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Abstractions;

/// <summary>
/// Interface for NuGet package publishing operations.
/// </summary>
public interface INuGetPublisher
{
	/// <summary>
	/// Publishes packages to GitHub Packages.
	/// </summary>
	/// <param name="packagePattern">The pattern to match package files.</param>
	/// <param name="owner">The GitHub owner/organization.</param>
	/// <param name="token">The GitHub token.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task PublishToGitHubAsync(string packagePattern, string owner, string token, CancellationToken cancellationToken = default);

	/// <summary>
	/// Publishes packages to NuGet.org.
	/// </summary>
	/// <param name="packagePattern">The pattern to match package files.</param>
	/// <param name="apiKey">The NuGet API key.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task PublishToNuGetOrgAsync(string packagePattern, string apiKey, CancellationToken cancellationToken = default);

	/// <summary>
	/// Publishes packages to a custom NuGet source.
	/// </summary>
	/// <param name="packagePattern">The pattern to match package files.</param>
	/// <param name="source">The NuGet source URL.</param>
	/// <param name="apiKey">The API key for the source.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task PublishToSourceAsync(string packagePattern, string source, string apiKey, CancellationToken cancellationToken = default);
}
