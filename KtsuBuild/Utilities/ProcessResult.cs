// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Utilities;

/// <summary>
/// Represents the result of a process execution.
/// </summary>
public record ProcessResult
{
	/// <summary>
	/// Gets the exit code of the process.
	/// </summary>
	public required int ExitCode { get; init; }

	/// <summary>
	/// Gets the standard output.
	/// </summary>
	public required string StandardOutput { get; init; }

	/// <summary>
	/// Gets the standard error.
	/// </summary>
	public required string StandardError { get; init; }

	/// <summary>
	/// Gets whether the process was successful (exit code 0).
	/// </summary>
	public bool Success => ExitCode == 0;

	/// <summary>
	/// Gets the combined output (stdout + stderr).
	/// </summary>
	public string CombinedOutput => string.IsNullOrEmpty(StandardError)
		? StandardOutput
		: string.IsNullOrEmpty(StandardOutput)
			? StandardError
			: $"{StandardOutput}\n{StandardError}";
}
