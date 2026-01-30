// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Abstractions;

using KtsuBuild.Utilities;

/// <summary>
/// Interface for running external processes.
/// </summary>
public interface IProcessRunner
{
	/// <summary>
	/// Runs a process and returns the result.
	/// </summary>
	/// <param name="fileName">The file name of the process to run.</param>
	/// <param name="arguments">The arguments to pass to the process.</param>
	/// <param name="workingDirectory">The working directory for the process.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The result of the process execution.</returns>
	public Task<ProcessResult> RunAsync(string fileName, string arguments, string? workingDirectory = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs a process and streams output in real-time.
	/// </summary>
	/// <param name="fileName">The file name of the process to run.</param>
	/// <param name="arguments">The arguments to pass to the process.</param>
	/// <param name="workingDirectory">The working directory for the process.</param>
	/// <param name="outputCallback">Callback for standard output lines.</param>
	/// <param name="errorCallback">Callback for standard error lines.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The exit code of the process.</returns>
	public Task<int> RunWithCallbackAsync(
		string fileName,
		string arguments,
		string? workingDirectory = null,
		Action<string>? outputCallback = null,
		Action<string>? errorCallback = null,
		CancellationToken cancellationToken = default);
}
