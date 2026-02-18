// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Helpers;

using KtsuBuild.Utilities;

/// <summary>
/// Shared test utility methods.
/// </summary>
public static class TestHelpers
{
	/// <summary>
	/// Creates a successful ProcessResult.
	/// </summary>
	public static ProcessResult SuccessResult(string stdout = "") => new()
	{
		ExitCode = 0,
		StandardOutput = stdout,
		StandardError = string.Empty,
	};

	/// <summary>
	/// Creates a failed ProcessResult.
	/// </summary>
	public static ProcessResult FailureResult(string stderr = "error", int exitCode = 1) => new()
	{
		ExitCode = exitCode,
		StandardOutput = string.Empty,
		StandardError = stderr,
	};

	/// <summary>
	/// Creates a temporary directory with a unique name.
	/// </summary>
	public static string CreateTempDir(string prefix)
	{
		string dir = Path.Combine(Path.GetTempPath(), $"{prefix}_{Guid.NewGuid():N}");
		Directory.CreateDirectory(dir);
		return dir;
	}
}
