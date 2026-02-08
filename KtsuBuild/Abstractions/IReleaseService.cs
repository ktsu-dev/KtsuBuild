// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Abstractions;

using KtsuBuild.Configuration;

/// <summary>
/// Interface for executing the release workflow (pack, publish apps, publish NuGet, create GitHub release).
/// </summary>
public interface IReleaseService
{
	/// <summary>
	/// Executes the full release workflow.
	/// </summary>
	/// <param name="config">The build configuration.</param>
	/// <param name="workspace">The workspace directory.</param>
	/// <param name="configuration">The build configuration name (Debug/Release).</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task ExecuteReleaseAsync(BuildConfiguration config, string workspace, string configuration, CancellationToken cancellationToken = default);
}
