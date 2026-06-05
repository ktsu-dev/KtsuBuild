// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Abstractions;

using KtsuBuild.Ios;

/// <summary>
/// Interface for the signed iOS packaging path (toolchain provisioning, version
/// stamping, signing-material setup, and <c>.ipa</c> archiving). This is the release
/// path and no-ops cleanly when signing material is unavailable, so it is safe to
/// call unconditionally from a consumer workflow.
/// </summary>
public interface IIosService
{
	/// <summary>
	/// Provisions the toolchain, stamps the version, sets up signing material, and
	/// archives a signed <c>.ipa</c> for the iOS head(s) described by the options.
	/// </summary>
	/// <param name="options">The packaging options, including the signing material.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The packaging result, including the produced <c>.ipa</c> paths or a skip reason.</returns>
	public Task<IosPackageResult> PackageAsync(IosPackageOptions options, CancellationToken cancellationToken = default);
}
