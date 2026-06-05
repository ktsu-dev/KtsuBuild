// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Abstractions;

using KtsuBuild.Ios;

/// <summary>
/// Interface for the unsigned iOS build path (pull-request validation).
/// </summary>
public interface IIosBuildService
{
	/// <summary>
	/// Builds the iOS head(s) described by the options for the simulator and device
	/// runtimes (unsigned), and verifies the device bundle and any required native
	/// frameworks.
	/// </summary>
	/// <param name="options">The build options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>True when every build and verification succeeded; false when a device bundle is missing or a required framework is absent.</returns>
	public Task<bool> BuildAsync(IosBuildOptions options, CancellationToken cancellationToken = default);
}
