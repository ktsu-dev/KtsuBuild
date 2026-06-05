// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Ios;

/// <summary>
/// Options for a signed iOS packaging run (the <c>ios package</c> path). This carries
/// the signing material, so its values must never be logged. Only
/// <see cref="SigningAvailable"/> is safe to surface, and only as a boolean.
/// </summary>
public class IosPackageOptions
{
	/// <summary>
	/// Gets or sets the working directory (the consumer workspace to search for iOS heads).
	/// </summary>
	public required string WorkingDirectory { get; set; }

	/// <summary>
	/// Gets or sets the build configuration (Debug/Release).
	/// </summary>
	public string Configuration { get; set; } = "Release";

	/// <summary>
	/// Gets or sets a specific iOS head project to package. When null or empty, all iOS
	/// heads in the working directory are auto-detected and packaged.
	/// </summary>
	public string? Project { get; set; }

	/// <summary>
	/// Gets or sets the device runtime identifier to archive for. iOS archives always
	/// target a device runtime; the default is <c>ios-arm64</c>.
	/// </summary>
	public string Runtime { get; set; } = "ios-arm64";

	/// <summary>
	/// Gets or sets the iOS target framework passed to the archive publish (<c>-f</c>).
	/// When null or empty it is resolved from the head's project file, falling back to
	/// omitting the flag for a single-targeted head.
	/// </summary>
	public string? Framework { get; set; }

	/// <summary>
	/// Gets or sets the path to the head's <c>Info.plist</c> for version stamping. When
	/// null or empty it is resolved next to the head's project file. Stamping is skipped
	/// when no <c>Info.plist</c> is found.
	/// </summary>
	public string? InfoPlistPath { get; set; }

	/// <summary>
	/// Gets or sets the marketing version stamped into <c>CFBundleShortVersionString</c>.
	/// This is KtsuBuild's computed version.
	/// </summary>
	public required string ShortVersion { get; set; }

	/// <summary>
	/// Gets or sets the monotonic build number stamped into <c>CFBundleVersion</c>. App
	/// Store Connect rejects an upload whose build number is not higher than the previous
	/// one, so this must increase on every release (a CI run number is the usual source).
	/// </summary>
	public string BuildNumber { get; set; } = "1";

	/// <summary>
	/// Gets or sets whether the signing material is available. When false the whole
	/// packaging step no-ops, so the command is safe to call unconditionally from a
	/// consumer workflow (forks and contributors without secrets still get a green run).
	/// </summary>
	public bool SigningAvailable { get; set; }

	/// <summary>
	/// Gets or sets the distribution certificate common name (the <c>CodesignKey</c>
	/// MSBuild property). Secret-adjacent: never log it.
	/// </summary>
	public string CodesignKey { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the provisioning profile name (the <c>CodesignProvision</c> MSBuild
	/// property). Secret-adjacent: never log it.
	/// </summary>
	public string ProvisionName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the base64-encoded distribution certificate (<c>.p12</c>). Secret:
	/// never log it. Decoded material is wiped after import.
	/// </summary>
	public string CertificateP12Base64 { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the password protecting the distribution certificate. Secret.
	/// </summary>
	public string CertificatePassword { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the password used for the temporary signing keychain. Secret.
	/// </summary>
	public string KeychainPassword { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the base64-encoded provisioning profile (<c>.mobileprovision</c>).
	/// Secret: never log it.
	/// </summary>
	public string ProvisioningProfileBase64 { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the pinned Xcode version to select before building (for example
	/// <c>26.3</c>). When empty, Xcode selection is skipped and the host default is used.
	/// </summary>
	public string XcodeVersion { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the pinned iOS workload version installed via a rollback file (for
	/// example <c>26.2.10233/10.0.100</c>). When empty, workload installation is skipped.
	/// </summary>
	public string WorkloadVersion { get; set; } = string.Empty;
}
