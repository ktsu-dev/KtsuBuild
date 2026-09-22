// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Abstractions;

using System.Collections.Generic;
using KtsuBuild.Utilities;

/// <summary>
/// Interface for running external processes.
/// </summary>
/// <remarks>
/// Each operation comes in two shapes. The <see cref="IReadOnlyList{T}"/> overloads take each
/// argument as its own value and are the ones to reach for: nothing has to be quoted, so nothing can
/// be quoted wrongly. The <see cref="string"/> overloads take the one pre-joined command line that
/// callers have always passed, and split it back apart on the caller's behalf; they remain because
/// most call sites still build their arguments that way.
/// </remarks>
public interface IProcessRunner
{
	/// <summary>
	/// Runs a process and returns the result.
	/// </summary>
	/// <param name="fileName">The file name of the process to run.</param>
	/// <param name="arguments">The arguments to pass to the process, as one pre-joined command line.</param>
	/// <param name="workingDirectory">The working directory for the process.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The result of the process execution.</returns>
	public Task<ProcessResult> RunAsync(string fileName, string arguments, string? workingDirectory = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs a process and returns the result, taking each argument as its own value.
	/// </summary>
	/// <param name="fileName">The file name of the process to run.</param>
	/// <param name="arguments">The arguments to pass to the process, each unquoted.</param>
	/// <param name="workingDirectory">The working directory for the process.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The result of the process execution.</returns>
	public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs a process and streams output in real-time.
	/// </summary>
	/// <param name="fileName">The file name of the process to run.</param>
	/// <param name="arguments">The arguments to pass to the process, as one pre-joined command line.</param>
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

	/// <summary>
	/// Runs a process and streams output in real-time, taking each argument as its own value.
	/// </summary>
	/// <param name="fileName">The file name of the process to run.</param>
	/// <param name="arguments">The arguments to pass to the process, each unquoted.</param>
	/// <param name="workingDirectory">The working directory for the process.</param>
	/// <param name="outputCallback">Callback for standard output lines.</param>
	/// <param name="errorCallback">Callback for standard error lines.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The exit code of the process.</returns>
	public Task<int> RunWithCallbackAsync(
		string fileName,
		IReadOnlyList<string> arguments,
		string? workingDirectory = null,
		Action<string>? outputCallback = null,
		Action<string>? errorCallback = null,
		CancellationToken cancellationToken = default);
}
