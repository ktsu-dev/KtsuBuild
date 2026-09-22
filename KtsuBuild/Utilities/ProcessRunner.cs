// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Utilities;

using System.Collections.Generic;
using System.Text;
using KtsuBuild.Abstractions;
using ktsu.RunCommand;
using ktsu.Semantics.Paths;
using ktsu.Semantics.Strings;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Implementation of process runner for executing external commands.
/// </summary>
/// <remarks>
/// A thin adapter over <see cref="RunCommand"/> rather than its own <c>Process</c> plumbing. That
/// library kills the whole process tree when the token is cancelled, which the hand-rolled version
/// did not: it only stopped awaiting, leaving the child — and anything the child had started —
/// running unsupervised. For a build tool, where the children are compilers and test hosts, an
/// abandoned process tree holds file locks and keeps consuming a runner.
/// </remarks>
public class ProcessRunner : IProcessRunner
{
	/// <inheritdoc/>
	public Task<ProcessResult> RunAsync(
		string fileName,
		string arguments,
		string? workingDirectory = null,
		CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(fileName);
		Ensure.NotNull(arguments);

		return RunAsync(fileName, CommandLineArguments.Split(arguments), workingDirectory, cancellationToken);
	}

	/// <inheritdoc/>
	public async Task<ProcessResult> RunAsync(
		string fileName,
		IReadOnlyList<string> arguments,
		string? workingDirectory = null,
		CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(fileName);
		Ensure.NotNull(arguments);

		StringBuilder outputBuilder = new();
		StringBuilder errorBuilder = new();

		int exitCode = await ExecuteAsync(
			fileName,
			arguments,
			workingDirectory,
			line => outputBuilder.AppendLine(line),
			line => errorBuilder.AppendLine(line),
			cancellationToken).ConfigureAwait(false);

		return new ProcessResult
		{
			ExitCode = exitCode,
			StandardOutput = outputBuilder.ToString().TrimEnd(),
			StandardError = errorBuilder.ToString().TrimEnd(),
		};
	}

	/// <inheritdoc/>
	public Task<int> RunWithCallbackAsync(
		string fileName,
		string arguments,
		string? workingDirectory = null,
		Action<string>? outputCallback = null,
		Action<string>? errorCallback = null,
		CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(fileName);
		Ensure.NotNull(arguments);

		return RunWithCallbackAsync(
			fileName,
			CommandLineArguments.Split(arguments),
			workingDirectory,
			outputCallback,
			errorCallback,
			cancellationToken);
	}

	/// <inheritdoc/>
	public Task<int> RunWithCallbackAsync(
		string fileName,
		IReadOnlyList<string> arguments,
		string? workingDirectory = null,
		Action<string>? outputCallback = null,
		Action<string>? errorCallback = null,
		CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(fileName);
		Ensure.NotNull(arguments);

		return ExecuteAsync(fileName, arguments, workingDirectory, outputCallback, errorCallback, cancellationToken);
	}

	/// <summary>
	/// Runs the process, which is the one place this type actually talks to <see cref="RunCommand"/>.
	/// </summary>
	/// <param name="fileName">The file name of the process to run.</param>
	/// <param name="arguments">The arguments to pass to the process, each unquoted.</param>
	/// <param name="workingDirectory">The working directory, or <see langword="null"/> for the current one.</param>
	/// <param name="outputCallback">Callback for standard output lines.</param>
	/// <param name="errorCallback">Callback for standard error lines.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The exit code of the process.</returns>
	private static async Task<int> ExecuteAsync(
		string fileName,
		IReadOnlyList<string> arguments,
		string? workingDirectory,
		Action<string>? outputCallback,
		Action<string>? errorCallback,
		CancellationToken cancellationToken)
	{
		// The raw OutputHandler rather than RunCommand's LineOutputHandler, because that one drops a
		// final line the process did not terminate with a newline. LineAssembler does the splitting
		// and flushes the remainder, so callers still get whole lines and lose nothing.
		LineAssembler output = new(outputCallback);
		LineAssembler error = new(errorCallback);
		OutputHandler handler = new(output.Append, error.Append);

		CommandOptions options = new()
		{
			WorkingDirectory = ResolveWorkingDirectory(workingDirectory),
		};

		try
		{
			return await RunCommand.ExecuteAsync(fileName, arguments, handler, options, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			// Also on the cancelled and failed paths: whatever the process managed to say before it
			// stopped is exactly what a caller diagnosing the failure wants to read.
			output.Flush();
			error.Flush();
		}
	}

	/// <summary>
	/// Turns the caller's working directory into the absolute path <see cref="CommandOptions"/> wants.
	/// </summary>
	/// <param name="workingDirectory">The caller's working directory, which may be relative or absent.</param>
	/// <returns>The absolute working directory to run in.</returns>
	private static AbsoluteDirectoryPath ResolveWorkingDirectory(string? workingDirectory) =>
		(string.IsNullOrEmpty(workingDirectory)
			? Directory.GetCurrentDirectory()
			: Path.GetFullPath(workingDirectory))
		.As<AbsoluteDirectoryPath>();
}
