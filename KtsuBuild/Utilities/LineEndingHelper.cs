// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Utilities;

using KtsuBuild.Abstractions;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Helper for handling line endings based on Git configuration.
/// </summary>
public class LineEndingHelper
{
	private readonly IProcessRunner _processRunner;

	/// <summary>
	/// Initializes a new instance of the <see cref="LineEndingHelper"/> class.
	/// </summary>
	/// <param name="processRunner">The process runner.</param>
	public LineEndingHelper(IProcessRunner processRunner)
	{
		Ensure.NotNull(processRunner);
		_processRunner = processRunner;
	}

	/// <summary>
	/// Gets the appropriate line ending based on Git configuration.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The line ending string ("\n" or "\r\n").</returns>
	public async Task<string> GetLineEndingAsync(string workingDirectory, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);

		// Check core.eol first
		ProcessResult eolResult = await _processRunner.RunAsync("git", "config --get core.eol", workingDirectory, cancellationToken).ConfigureAwait(false);
		if (eolResult.Success && !string.IsNullOrWhiteSpace(eolResult.StandardOutput))
		{
			string eol = eolResult.StandardOutput.Trim().ToLowerInvariant();
			return eol switch
			{
				"lf" => "\n",
				"crlf" => "\r\n",
				_ => Environment.NewLine,
			};
		}

		// Fall back to core.autocrlf
		ProcessResult autocrlfResult = await _processRunner.RunAsync("git", "config --get core.autocrlf", workingDirectory, cancellationToken).ConfigureAwait(false);
		if (autocrlfResult.Success && !string.IsNullOrWhiteSpace(autocrlfResult.StandardOutput))
		{
			string autocrlf = autocrlfResult.StandardOutput.Trim().ToLowerInvariant();
			return autocrlf switch
			{
				"true" => "\n", // Git will convert to CRLF on checkout
				"input" => "\n", // Always use LF
				"false" => Environment.NewLine, // Use OS default
				_ => Environment.NewLine,
			};
		}

		// Default to OS line ending
		return Environment.NewLine;
	}

	/// <summary>
	/// Normalizes content to use the specified line ending.
	/// </summary>
	/// <param name="content">The content to normalize.</param>
	/// <param name="lineEnding">The target line ending.</param>
	/// <returns>The normalized content.</returns>
	public static string NormalizeLineEndings(string content, string lineEnding)
	{
		Ensure.NotNull(content);
		Ensure.NotNull(lineEnding);

		// First normalize all line endings to LF
		string normalized = content
			.Replace("\r\n", "\n")
			.Replace("\r", "\n");

		// Then convert to target line ending if not LF
		if (lineEnding != "\n")
		{
			normalized = normalized.Replace("\n", lineEnding);
		}

		return normalized;
	}

	/// <summary>
	/// Writes content to a file with proper encoding and line endings.
	/// </summary>
	/// <param name="filePath">The file path.</param>
	/// <param name="content">The content to write.</param>
	/// <param name="lineEnding">The line ending to use.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public static async Task WriteFileAsync(string filePath, string content, string lineEnding, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(filePath);
		Ensure.NotNull(content);
		Ensure.NotNull(lineEnding);

		string normalizedContent = NormalizeLineEndings(content, lineEnding);

		// Write without BOM (UTF-8)
		await File.WriteAllTextAsync(filePath, normalizedContent, new System.Text.UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
	}
}
