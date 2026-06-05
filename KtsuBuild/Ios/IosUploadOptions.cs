// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Ios;

/// <summary>
/// Options for a TestFlight upload run (the <c>ios upload</c> path). This carries the
/// App Store Connect API key, so its values must never be logged. Only
/// <see cref="SigningAvailable"/> is safe to surface, and only as a boolean.
/// </summary>
public class IosUploadOptions
{
	/// <summary>
	/// Gets or sets the working directory (the consumer workspace to search for iOS heads
	/// and their produced <c>.ipa</c> archives).
	/// </summary>
	public required string WorkingDirectory { get; set; }

	/// <summary>
	/// Gets or sets the build configuration (Debug/Release). Used only to narrow the
	/// <c>.ipa</c> search; the archive is located recursively under the head's <c>bin</c>.
	/// </summary>
	public string Configuration { get; set; } = "Release";

	/// <summary>
	/// Gets or sets a specific iOS head project whose archive to upload. When null or
	/// empty, all iOS heads in the working directory are auto-detected.
	/// </summary>
	public string? Project { get; set; }

	/// <summary>
	/// Gets or sets an explicit <c>.ipa</c> path to upload. When null or empty the archive
	/// is located under the head's <c>bin</c> directory, matching what <c>ios package</c>
	/// produced in the same workspace.
	/// </summary>
	public string? IpaPath { get; set; }

	/// <summary>
	/// Gets or sets whether the signing material is available. When false the whole upload
	/// step no-ops, so the command is safe to call unconditionally from a consumer workflow
	/// (forks and contributors without secrets still get a green run).
	/// </summary>
	public bool SigningAvailable { get; set; }

	/// <summary>
	/// Gets or sets the base64-encoded App Store Connect API key (<c>.p8</c>). Secret:
	/// never log it. Decoded material is wiped after the upload.
	/// </summary>
	public string AppStoreConnectKeyBase64 { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the App Store Connect API key identifier (the <c>--apiKey</c> value,
	/// which also names the decoded <c>AuthKey_{id}.p8</c> file).
	/// </summary>
	public string AppStoreConnectKeyId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the App Store Connect API issuer identifier (the <c>--apiIssuer</c>
	/// value).
	/// </summary>
	public string AppStoreConnectIssuerId { get; set; } = string.Empty;
}
