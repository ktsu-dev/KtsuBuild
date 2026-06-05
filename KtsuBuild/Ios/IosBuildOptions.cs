// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

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
}
