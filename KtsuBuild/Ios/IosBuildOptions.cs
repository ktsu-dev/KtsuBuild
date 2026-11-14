// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Ios;

/// <summary>
/// Options for an unsigned iOS build run.
/// </summary>
public class IosBuildOptions
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
	/// Gets or sets a specific iOS head project to build. When null or empty, all iOS
	/// heads in the working directory are auto-detected and built.
	/// </summary>
	public string? Project { get; set; }

	/// <summary>
	/// Gets or sets a specific iOS runtime identifier to build. When null or empty, both
	/// the simulator and device runtimes are built.
	/// </summary>
	public string? Runtime { get; set; }

	/// <summary>
	/// Gets or sets the native frameworks that must be embedded in the device bundle. The
	/// device build fails when any of these is missing, guarding the asset-resolution
	/// launch-crash class.
	/// </summary>
	public IReadOnlyList<string> RequiredFrameworks { get; set; } = [];

	/// <summary>
	/// Gets or sets the Xcode version to select before building (from <c>IOS_XCODE_VERSION</c>).
	/// When empty, the Xcode already selected on the host is used.
	/// </summary>
	public string XcodeVersion { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the iOS workload version to install before building (from
	/// <c>IOS_WORKLOAD_VERSION</c>). When empty, the workload already installed is used.
	/// </summary>
	public string WorkloadVersion { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether to report the heads and runtimes that would be
	/// built, without provisioning the toolchain, building anything or starting any process.
	/// </summary>
	public bool DryRun { get; set; }

	/// <summary>
	/// Sets <see cref="XcodeVersion"/> and <see cref="WorkloadVersion"/> from the pins in the
	/// environment, the same ones <c>ios package</c> applies.
	/// </summary>
	/// <returns>These options, for chaining.</returns>
	public IosBuildOptions WithToolchainPinsFromEnvironment()
	{
		(XcodeVersion, WorkloadVersion) = KtsuBuild.Configuration.BuildConfigurationProvider.ReadIosToolchainPins();
		return this;
	}
}
