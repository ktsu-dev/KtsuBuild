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
	public static Option<string> Workspace { get; } = new(
		["--workspace", "-w"],
		() => Directory.GetCurrentDirectory(),
		"The workspace/repository path");

	/// <summary>
	/// Gets the configuration option.
	/// </summary>
	public static Option<string> Configuration { get; } = new(
		["--configuration", "-c"],
		() => "Release",
		"The build configuration (Debug/Release)");

	/// <summary>
	/// Gets the verbose option.
	/// </summary>
	public static Option<bool> Verbose { get; } = new(
		["--verbose", "-v"],
		() => false,
		"Enable verbose output");

	/// <summary>
	/// Gets the dry-run option.
	/// </summary>
	public static Option<bool> DryRun { get; } = new(
		["--dry-run"],
		() => false,
		"Preview actions without executing them");
}
