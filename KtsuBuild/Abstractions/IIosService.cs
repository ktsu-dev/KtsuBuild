// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Abstractions;

using KtsuBuild.Ios;

/// <summary>
/// Interface for the signed iOS release path: toolchain provisioning, version
/// stamping, signing-material setup, <c>.ipa</c> archiving, and TestFlight upload.
/// Both steps no-op cleanly when signing material is unavailable, so they are safe to
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

	/// <summary>
	/// Uploads the signed <c>.ipa</c> for the iOS head(s) described by the options to
	/// TestFlight, using an App Store Connect API key.
	/// </summary>
	/// <param name="options">The upload options, including the App Store Connect API key.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The upload result, including the uploaded <c>.ipa</c> paths or a skip reason.</returns>
	public Task<IosUploadResult> UploadAsync(IosUploadOptions options, CancellationToken cancellationToken = default);
}
