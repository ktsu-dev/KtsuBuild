// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.CLI.Commands;

using System.CommandLine;

/// <summary>
/// Global options shared across all commands.
/// </summary>
public static class GlobalOptions
{
	/// <summary>
	/// Gets the workspace option.
	/// </summary>
	public static Option<string> Workspace { get; } = new("--workspace", "-w")
	{
		Description = "The workspace/repository path",
		DefaultValueFactory = _ => Directory.GetCurrentDirectory(),
	};

	/// <summary>
	/// Gets the configuration option.
	/// </summary>
	public static Option<string> Configuration { get; } = new("--configuration", "-c")
	{
		Description = "The build configuration (Debug/Release)",
		DefaultValueFactory = _ => "Release",
	};

	/// <summary>
	/// Gets the verbose option.
	/// </summary>
	public static Option<bool> Verbose { get; } = new("--verbose", "-v")
	{
		Description = "Enable verbose output",
		DefaultValueFactory = _ => false,
	};

	/// <summary>
	/// Gets the dry-run option.
	/// </summary>
	public static Option<bool> DryRun { get; } = new("--dry-run")
	{
		Description = "Preview actions without executing them",
		DefaultValueFactory = _ => false,
	};

	/// <summary>
	/// Gets the version-bump option.
	/// </summary>
	public static Option<string> VersionBump { get; } = new("--version-bump")
	{
		Description = "Force a specific version bump type (auto, patch, minor, major)",
		DefaultValueFactory = _ => "auto",
	};
}
