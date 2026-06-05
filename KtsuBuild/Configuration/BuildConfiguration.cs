// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Configuration;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Represents the complete build configuration.
/// </summary>
public class BuildConfiguration
{
	/// <summary>
	/// Gets or sets whether this is an official repository (not a fork and owned by expected owner).
	/// </summary>
	public bool IsOfficial { get; set; }

	/// <summary>
	/// Gets or sets whether this is the main branch.
	/// </summary>
	public bool IsMain { get; set; }

	/// <summary>
	/// Gets or sets whether the current commit is already tagged.
	/// </summary>
	public bool IsTagged { get; set; }

	/// <summary>
	/// Gets or sets whether a release should be created.
	/// </summary>
	public bool ShouldRelease { get; set; }

	/// <summary>
	/// Gets or sets whether dotnet-script is needed.
	/// </summary>
	public bool UseDotnetScript { get; set; }

	/// <summary>
	/// Gets or sets the output path for published applications.
	/// </summary>
	public string OutputPath { get; set; } = "output";

	/// <summary>
	/// Gets or sets the staging path for packages.
	/// </summary>
	public string StagingPath { get; set; } = "staging";

	/// <summary>
	/// Gets or sets the package pattern for NuGet packages.
	/// </summary>
	public string PackagePattern { get; set; } = "staging/*.nupkg";

	/// <summary>
	/// Gets or sets the symbols pattern for symbol packages.
	/// </summary>
	public string SymbolsPattern { get; set; } = "staging/*.snupkg";

	/// <summary>
	/// Gets or sets the application pattern for published apps.
	/// </summary>
	public string ApplicationPattern { get; set; } = "staging/*.zip";

	/// <summary>
	/// Gets or sets additional build arguments.
	/// </summary>
	public string BuildArgs { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the workspace path.
	/// </summary>
	public string WorkspacePath { get; set; } = ".";

	/// <summary>
	/// Gets or sets the GitHub server URL.
	/// </summary>
	[SuppressMessage("Design", "CA1056:URI properties should not be strings", Justification = "String URLs are simpler for CLI tool configuration")]
	public string ServerUrl { get; set; } = "https://github.com";

	/// <summary>
	/// Gets or sets the Git reference.
	/// </summary>
	public string GitRef { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the Git commit SHA.
	/// </summary>
	public string GitSha { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the GitHub owner/organization.
	/// </summary>
	public string GitHubOwner { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the GitHub repository name.
	/// </summary>
	public string GitHubRepo { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the GitHub token.
	/// </summary>
	public string GithubToken { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the NuGet API key.
	/// </summary>
	public string NuGetApiKey { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the Ktsu package key.
	/// </summary>
	public string KtsuPackageKey { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the expected owner for official builds.
	/// </summary>
	public string ExpectedOwner { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the current version.
	/// </summary>
	public string Version { get; set; } = "1.0.0-pre.0";

	/// <summary>
	/// Gets or sets the release commit hash.
	/// </summary>
	public string ReleaseHash { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the changelog file path.
	/// </summary>
	public string ChangelogFile { get; set; } = "CHANGELOG.md";

	/// <summary>
	/// Gets or sets the latest changelog file path.
	/// </summary>
	public string LatestChangelogFile { get; set; } = "LATEST_CHANGELOG.md";

	/// <summary>
	/// Gets or sets the asset patterns for release.
	/// </summary>
	public IReadOnlyList<string> AssetPatterns { get; set; } = [];

	/// <summary>
	/// Gets or sets the build configuration name (Debug/Release).
	/// </summary>
	public string Configuration { get; set; } = "Release";

	/// <summary>
	/// Gets or sets whether the iOS signing material is available. This is the only iOS
	/// signing input that should ever surface in output, and only as a boolean. It gates
	/// the iOS package and upload paths so forks and contributors without secrets still
	/// get a clean run.
	/// </summary>
	public bool IosSigningAvailable { get; set; }

	/// <summary>
	/// Gets or sets the iOS distribution certificate common name (the <c>CodesignKey</c>
	/// MSBuild property). Secret-adjacent: never log it.
	/// </summary>
	public string IosCodesignKey { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the iOS provisioning profile name (the <c>CodesignProvision</c>
	/// MSBuild property). Secret-adjacent: never log it.
	/// </summary>
	public string IosProvisionName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the base64-encoded iOS distribution certificate (<c>.p12</c>). Secret.
	/// </summary>
	public string IosCertP12Base64 { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the password protecting the iOS distribution certificate. Secret.
	/// </summary>
	public string IosCertP12Password { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the password used for the temporary iOS signing keychain. Secret.
	/// </summary>
	public string IosKeychainPassword { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the base64-encoded iOS provisioning profile (<c>.mobileprovision</c>).
	/// Secret.
	/// </summary>
	public string IosProvisioningProfileBase64 { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the pinned Xcode version for iOS builds (for example <c>26.3</c>).
	/// Empty leaves the host default in place.
	/// </summary>
	public string XcodeVersion { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the pinned iOS workload version installed via a rollback file (for
	/// example <c>26.2.10233/10.0.100</c>). Empty skips workload installation.
	/// </summary>
	public string IosWorkloadVersion { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the base64-encoded App Store Connect API key (<c>.p8</c>) used to
	/// authenticate the TestFlight upload. Secret: never log it. Decoded material is
	/// wiped after the upload.
	/// </summary>
	public string AppStoreConnectKeyBase64 { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the App Store Connect API key identifier (the <c>--apiKey</c> value,
	/// which also names the decoded <c>AuthKey_{id}.p8</c> file). An identifier rather
	/// than a secret, but not surfaced in output.
	/// </summary>
	public string AppStoreConnectKeyId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the App Store Connect API issuer identifier (the <c>--apiIssuer</c>
	/// value). An identifier rather than a secret, but not surfaced in output.
	/// </summary>
	public string AppStoreConnectIssuerId { get; set; } = string.Empty;
}
