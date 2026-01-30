// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Abstractions;

using KtsuBuild.Configuration;

/// <summary>
/// Interface for providing build configuration.
/// </summary>
public interface IBuildConfigurationProvider
{
	/// <summary>
	/// Creates a build configuration from the provided options.
	/// </summary>
	/// <param name="options">The build configuration options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The build configuration.</returns>
	public Task<BuildConfiguration> CreateAsync(BuildConfigurationOptions options, CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates a build configuration from environment variables and git status.
	/// </summary>
	/// <param name="workspacePath">The workspace/repository path.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The build configuration.</returns>
	public Task<BuildConfiguration> CreateFromEnvironmentAsync(string workspacePath, CancellationToken cancellationToken = default);
}
