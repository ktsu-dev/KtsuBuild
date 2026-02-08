// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Utilities;

using System.Diagnostics;
using System.Text;
using KtsuBuild.Abstractions;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Implementation of process runner for executing external commands.
/// </summary>
public class ProcessRunner : IProcessRunner
{
	/// <inheritdoc/>
	public async Task<ProcessResult> RunAsync(
		string fileName,
		string arguments,
		string? workingDirectory = null,
		CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(fileName);
		Ensure.NotNull(arguments);

		using Process process = new();
		process.StartInfo = new ProcessStartInfo
		{
			FileName = fileName,
			Arguments = arguments,
			WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		StringBuilder outputBuilder = new();
		StringBuilder errorBuilder = new();

		process.OutputDataReceived += (sender, e) =>
		{
			if (e.Data is not null)
			{
				outputBuilder.AppendLine(e.Data);
			}
		};

		process.ErrorDataReceived += (sender, e) =>
		{
			if (e.Data is not null)
			{
				errorBuilder.AppendLine(e.Data);
			}
		};

		process.Start();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();

		await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

		return new ProcessResult
		{
			ExitCode = process.ExitCode,
			StandardOutput = outputBuilder.ToString().TrimEnd(),
			StandardError = errorBuilder.ToString().TrimEnd(),
		};
	}

	/// <inheritdoc/>
	public async Task<int> RunWithCallbackAsync(
		string fileName,
		string arguments,
		string? workingDirectory = null,
		Action<string>? outputCallback = null,
		Action<string>? errorCallback = null,
		CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(fileName);
		Ensure.NotNull(arguments);

		using Process process = new();
		process.StartInfo = new ProcessStartInfo
		{
			FileName = fileName,
			Arguments = arguments,
			WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		process.OutputDataReceived += (sender, e) =>
		{
			if (e.Data is not null)
			{
				outputCallback?.Invoke(e.Data);
			}
		};

		process.ErrorDataReceived += (sender, e) =>
		{
			if (e.Data is not null)
			{
				errorCallback?.Invoke(e.Data);
			}
		};

		process.Start();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();

		await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

		return process.ExitCode;
	}
}
