// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Ios;

/// <summary>
/// Options for archiving a signed <c>.ipa</c> for a single iOS head. The signing values
/// are passed straight to MSBuild and must never be logged.
/// </summary>
public class IosArchiveOptions
{
	/// <summary>
	/// Gets or sets the working directory the archive publish runs from.
	/// </summary>
	public required string WorkingDirectory { get; set; }

	/// <summary>
	/// Gets or sets the iOS head project file to archive.
	/// </summary>
	public required string ProjectPath { get; set; }

	/// <summary>
	/// Gets or sets the device runtime identifier (for example <c>ios-arm64</c>).
	/// </summary>
	public required string RuntimeIdentifier { get; set; }

	/// <summary>
	/// Gets or sets the build configuration (Debug/Release).
	/// </summary>
	public string Configuration { get; set; } = "Release";

	/// <summary>
	/// Gets or sets the iOS target framework passed as <c>-f</c>. When null or empty the
	/// flag is omitted, which is correct for a single-targeted head.
	/// </summary>
	public string? Framework { get; set; }

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
}
