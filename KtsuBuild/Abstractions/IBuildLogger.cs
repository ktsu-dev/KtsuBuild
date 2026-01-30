// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Abstractions;

/// <summary>
/// Interface for build logging with colored step headers and formatted output.
/// </summary>
public interface IBuildLogger
{
	/// <summary>
	/// Writes a step header to the console.
	/// </summary>
	/// <param name="message">The header message.</param>
	public void WriteStepHeader(string message);

	/// <summary>
	/// Writes an informational message.
	/// </summary>
	/// <param name="message">The message to write.</param>
	public void WriteInfo(string message);

	/// <summary>
	/// Writes a warning message.
	/// </summary>
	/// <param name="message">The warning message.</param>
	public void WriteWarning(string message);

	/// <summary>
	/// Writes an error message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public void WriteError(string message);

	/// <summary>
	/// Writes a success message.
	/// </summary>
	/// <param name="message">The success message.</param>
	public void WriteSuccess(string message);

	/// <summary>
	/// Writes a verbose/debug message.
	/// </summary>
	/// <param name="message">The verbose message.</param>
	public void WriteVerbose(string message);

	/// <summary>
	/// Gets or sets whether verbose logging is enabled.
	/// </summary>
	public bool VerboseEnabled { get; set; }
}
